using TwStyling.Css;

namespace TwStyling;

/// <summary>
/// Compiles a class string into a <see cref="StylePlan"/> using a Tailwind-generated stylesheet as
/// the source of truth, instead of re-deriving Tailwind's utility grammar.
///
/// The class name is now an opaque key: everything about what it means lives in the CSS.
/// </summary>
public sealed class TwCssPlanCompiler
{
    private readonly Dictionary<string, List<CssRule>> _byClass;
    private readonly CssStylesheet _sheet;

    /// <summary>
    /// Builds a compiler from the CSS the Tailwind CLI produced. This is the whole public seam:
    /// callers hand over text and get plans back, and the CSS syntax tree stays an internal detail.
    /// </summary>
    public static TwCssPlanCompiler FromCss(string css) => new(CssStylesheetParser.Parse(css));

    internal TwCssPlanCompiler(CssStylesheet sheet)
    {
        _sheet = sheet;
        _byClass = new Dictionary<string, List<CssRule>>(StringComparer.Ordinal);

        foreach (var rule in sheet.Rules)
        {
            if (!_byClass.TryGetValue(rule.ClassName, out var list))
                _byClass[rule.ClassName] = list = new List<CssRule>(2);
            list.Add(rule);
        }
    }

    /// <summary>True when the stylesheet emitted no rule for this candidate — i.e. an unknown utility.</summary>
    public bool IsKnown(string className) => _byClass.ContainsKey(className);

    /// <summary>
    /// True when any utility in the class string is qualified by an idiom (<c>phone:</c>,
    /// <c>tablet:</c>, …). Unlike a platform, the idiom is not fixed at build time — one iOS head
    /// serves both iPhone and iPad — so such a string cannot be precompiled and must be resolved
    /// against the real device at runtime.
    /// </summary>
    public bool HasIdiomVariant(string classes)
    {
        foreach (var candidate in classes.Split([' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            if (!_byClass.TryGetValue(candidate, out var rules)) continue;
            foreach (var rule in rules)
                if (rule.Context.Platform is { } name && IdiomOf(name) is not null)
                    return true;
        }
        return false;
    }

    public StylePlan Compile(string classes, in TwEnvironment env)
    {
        if (string.IsNullOrWhiteSpace(classes)) return StylePlan.Empty;

        var diagnostics = new List<TwDiagnostic>();
        var items = new List<ParsedItem>();
        var lowered = new List<TwDeclaration>(4);

        var matched = Match(classes, env, diagnostics);
        var scopes = BuildScopes(matched, env);

        foreach (var (candidate, rule, variants) in matched)
        {
            var scope = scopes[BucketOf(variants)];

            foreach (var declaration in rule.Declarations)
            {
                // Custom properties were already folded into the scope; they are never applied.
                if (declaration.Property.StartsWith("--", StringComparison.Ordinal)) continue;

                lowered.Clear();
                CssValue value;
                try
                {
                    value = CssEvaluator.Evaluate(CssValueParser.Parse(declaration.Value), scope);
                }
                catch (CssEvalException ex)
                {
                    diagnostics.Add(new TwDiagnostic(classes, candidate, $"{declaration.Property}: {ex.Message}"));
                    continue;
                }

                if (!TwCssLowering.TryLower(declaration.Property, value, scope, lowered, out var error))
                {
                    diagnostics.Add(new TwDiagnostic(classes, candidate, error ?? "not supported"));
                    continue;
                }

                foreach (var d in lowered)
                    items.Add(new ParsedItem(variants, d));
            }
        }

        return StylePlanCompiler.FromItems(items, env, classes, diagnostics);
    }

    /// <summary>
    /// The (theme, state, breakpoint) triple that <see cref="StylePlanCompiler"/> merges into one
    /// declaration set. A value tuple, not a record struct: this file is compiled into the analyzer
    /// and generator, which target netstandard2.0 and have no <c>IsExternalInit</c>.
    /// </summary>
    private static (TwTheme, TwInteractiveState, float) BucketOf(TwVariantSet v) =>
        (v.Theme, v.State, v.BreakpointMinWidth);

    private static readonly (TwTheme, TwInteractiveState, float) Base = (TwTheme.Any, TwInteractiveState.None, 0f);

    /// <summary>Resolves each candidate to the rules that apply in this build environment.</summary>
    private List<(string Candidate, CssRule Rule, TwVariantSet Variants)> Match(
        string classes, TwEnvironment env, List<TwDiagnostic> diagnostics)
    {
        var matched = new List<(string, CssRule, TwVariantSet)>();

        foreach (var candidate in classes.Split([' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            if (!_byClass.TryGetValue(candidate, out var rules))
            {
                // Tailwind itself produced no rule for this candidate, so it is not a utility at all:
                // a typo, a variant that was never declared, or something the standard scale rejects
                // (`-p-2`). Say where it can be defined rather than just that it is unknown.
                diagnostics.Add(new TwDiagnostic(classes, candidate,
                    "unknown utility — Tailwind generated no rule for it. Check the spelling, or define it in tw.css with @theme, @utility or @custom-variant."));
                continue;
            }

            foreach (var rule in rules)
            {
                if (!TryVariants(rule.Context, out var variants, out var reason))
                {
                    diagnostics.Add(new TwDiagnostic(classes, candidate, reason));
                    continue;
                }
                // A utility for another platform contributes nothing — not even its custom properties.
                if (!variants.AppliesTo(env)) continue;

                matched.Add((candidate, rule, variants));
            }
        }
        return matched;
    }

    /// <summary>
    /// Accumulates custom properties the way a browser does: every matching rule contributes to the
    /// element's computed style, and only then does <c>var()</c> resolve. Gradients depend on this —
    /// <c>background-image</c> lives on <c>.bg-linear-to-r</c> but reads <c>--tw-gradient-from</c>,
    /// which only <c>.from-blue-500</c> declares. A per-rule scope can never see it.
    ///
    /// Buckets keep variants honest: <c>hover:from-red-500</c> must only contribute under hover, so a
    /// bucket's scope layers its own properties over the unqualified ones.
    /// </summary>
    private Dictionary<(TwTheme, TwInteractiveState, float), CssEnvironment> BuildScopes(
        List<(string Candidate, CssRule Rule, TwVariantSet Variants)> matched, TwEnvironment env)
    {
        var properties = new Dictionary<(TwTheme, TwInteractiveState, float), Dictionary<string, string>>();

        foreach (var (_, rule, variants) in matched)
        {
            foreach (var d in rule.Declarations)
            {
                if (!d.Property.StartsWith("--", StringComparison.Ordinal)) continue;

                var bucket = BucketOf(variants);
                if (!properties.TryGetValue(bucket, out var bag))
                    properties[bucket] = bag = new Dictionary<string, string>(StringComparer.Ordinal);
                bag[d.Property] = d.Value; // source order: later wins
            }
        }

        properties.TryGetValue(Base, out var baseProperties);
        var empty = new Dictionary<string, string>(StringComparer.Ordinal);

        var scopes = new Dictionary<(TwTheme, TwInteractiveState, float), CssEnvironment>();
        foreach (var (_, _, variants) in matched)
        {
            var bucket = BucketOf(variants);
            if (scopes.ContainsKey(bucket)) continue;

            properties.TryGetValue(bucket, out var own);
            scopes[bucket] = bucket.Equals(Base)
                ? new CssEnvironment(_sheet.Variables, own ?? empty)
                : new CssEnvironment(_sheet.Variables, baseProperties ?? empty, own ?? empty);
        }

        if (!scopes.ContainsKey(Base))
            scopes[Base] = new CssEnvironment(_sheet.Variables, baseProperties ?? empty);

        return scopes;
    }

    /// <summary>Projects a CSS rule's static conditions onto the variant model.</summary>
    private static bool TryVariants(CssContext context, out TwVariantSet variants, out string reason)
    {
        variants = TwVariantSet.Default;
        reason = "";

        var state = context.Pseudo switch
        {
            CssPseudo.Hover => TwInteractiveState.Hover,
            CssPseudo.Focus => TwInteractiveState.Focus,
            CssPseudo.Active => TwInteractiveState.Pressed,
            CssPseudo.Disabled => TwInteractiveState.Disabled,
            _ => TwInteractiveState.None,
        };

        var theme = context.ColorScheme switch
        {
            "dark" => TwTheme.Dark,
            "light" => TwTheme.Light,
            _ => TwTheme.Any,
        };

        if (!double.IsPositiveInfinity(context.MaxWidth))
        {
            reason = "max-width breakpoints (max-sm:/…) are not modeled yet — the utility was ignored";
            return false;
        }

        variants = TwVariantSet.Default.With(theme).With(state).WithBreakpoint((float)context.MinWidth);

        if (context.Platform is { } platform)
        {
            if (PlatformOf(platform) is { } p) variants = variants.With(p);
            else if (IdiomOf(platform) is { } i) variants = variants.With(i);
            else
            {
                reason = $"unknown custom variant '{platform}' — declare it with @custom-variant and map it to a platform or idiom";
                return false;
            }
        }

        return true;
    }

    private static TwPlatforms? PlatformOf(string name) => name switch
    {
        "android" => TwPlatforms.Android,
        "ios" => TwPlatforms.Ios,
        "mac" or "maccatalyst" or "macos" => TwPlatforms.Mac,
        "windows" => TwPlatforms.Windows,
        "tizen" => TwPlatforms.Tizen,
        _ => null,
    };

    private static TwIdioms? IdiomOf(string name) => name switch
    {
        "phone" => TwIdioms.Phone,
        "tablet" => TwIdioms.Tablet,
        "desktop" => TwIdioms.Desktop,
        "tv" => TwIdioms.Tv,
        "watch" => TwIdioms.Watch,
        _ => null,
    };
}
