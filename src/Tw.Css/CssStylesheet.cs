namespace Tw.Css;

/// <summary>The interactive pseudo-class a rule is gated on.</summary>
public enum CssPseudo : byte { None, Hover, Focus, Active, Disabled }

/// <summary>
/// The conditions under which a rule applies, gathered from the enclosing at-rules and
/// pseudo-class selectors. Everything here is known statically from the stylesheet.
/// </summary>
public sealed class CssContext
{
    public CssContext(
        double minWidth = 0,
        double maxWidth = double.PositiveInfinity,
        string? colorScheme = null,
        string? platform = null,
        CssPseudo pseudo = CssPseudo.None)
    {
        MinWidth = minWidth;
        MaxWidth = maxWidth;
        ColorScheme = colorScheme;
        Platform = platform;
        Pseudo = pseudo;
    }

    /// <summary>Lower bound in px from <c>(width &gt;= N)</c> / <c>(min-width: N)</c>. 0 means unbounded.</summary>
    public double MinWidth { get; }

    /// <summary>Upper bound in px. <see cref="double.PositiveInfinity"/> means unbounded.</summary>
    public double MaxWidth { get; }

    /// <summary><c>dark</c> or <c>light</c> from <c>prefers-color-scheme</c>; null when theme-agnostic.</summary>
    public string? ColorScheme { get; }

    /// <summary>
    /// The bare media identifier from a custom variant, e.g. <c>@media windows</c>. Not valid CSS —
    /// Tailwind passes an unrecognized <c>@custom-variant</c> media query through verbatim, which is
    /// exactly the hook we use to carry platform variants through to build-time filtering.
    /// </summary>
    public string? Platform { get; }

    public CssPseudo Pseudo { get; }

    public static readonly CssContext Root = new();

    public CssContext With(CssPseudo pseudo) =>
        new(MinWidth, MaxWidth, ColorScheme, Platform, pseudo == CssPseudo.None ? Pseudo : pseudo);

    public override string ToString()
    {
        var parts = new List<string>();
        if (MinWidth > 0) parts.Add($"min-width:{MinWidth}");
        if (!double.IsPositiveInfinity(MaxWidth)) parts.Add($"max-width:{MaxWidth}");
        if (ColorScheme is not null) parts.Add(ColorScheme);
        if (Platform is not null) parts.Add(Platform);
        if (Pseudo != CssPseudo.None) parts.Add(Pseudo.ToString().ToLowerInvariant());
        return parts.Count == 0 ? "(root)" : string.Join(" ", parts);
    }
}

public readonly struct CssDeclaration
{
    public readonly string Property;
    public readonly string Value;
    public readonly bool Important;

    public CssDeclaration(string property, string value, bool important)
    { Property = property; Value = value; Important = important; }

    public override string ToString() => $"{Property}: {Value}{(Important ? " !important" : "")}";
}

/// <summary>One class rule: a class name, the conditions it applies under, and its declarations.</summary>
public sealed class CssRule
{
    public CssRule(string className, CssContext context, IReadOnlyList<CssDeclaration> declarations)
    {
        ClassName = className;
        Context = context;
        Declarations = declarations;
    }

    public string ClassName { get; }
    public CssContext Context { get; }
    public IReadOnlyList<CssDeclaration> Declarations { get; }

    public override string ToString() => $".{ClassName} [{Context}] {{ {string.Join("; ", Declarations)} }}";
}

/// <summary>The result of parsing a Tailwind stylesheet.</summary>
public sealed class CssStylesheet
{
    public CssStylesheet(
        IReadOnlyList<CssRule> rules,
        IReadOnlyDictionary<string, string> variables,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> scopedVariables)
    {
        Rules = rules;
        Variables = variables;
        ScopedVariables = scopedVariables;
    }

    /// <summary>Class rules from the utilities layer, in source order.</summary>
    public IReadOnlyList<CssRule> Rules { get; }

    /// <summary>Theme tokens from <c>:root</c> plus <c>@property</c> initial values.</summary>
    public IReadOnlyDictionary<string, string> Variables { get; }

    /// <summary>Theme tokens scoped to a color scheme, e.g. a <c>.dark</c> class or a dark media block.</summary>
    public IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> ScopedVariables { get; }

    public CssEnvironment Environment() => new(Variables);
}

/// <summary>
/// Parses the CSS that Tailwind emits into class rules plus a variable scope. This is a structural
/// parser: declaration values are kept as text and only evaluated on demand by <see cref="CssEvaluator"/>.
/// </summary>
public static class CssStylesheetParser
{
    public static CssStylesheet Parse(string css)
    {
        var rules = new List<CssRule>();
        var variables = new Dictionary<string, string>(StringComparer.Ordinal);
        var scoped = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);

        int i = 0;
        ParseBlock(css, ref i, CssContext.Root, className: null, rules, variables, scoped, inUtilities: false);

        return new CssStylesheet(
            rules,
            variables,
            scoped.ToDictionary(
                kv => kv.Key,
                kv => (IReadOnlyDictionary<string, string>)kv.Value,
                StringComparer.Ordinal));
    }

    /// <summary>Consumes rules until the matching <c>}</c> or end of input.</summary>
    private static void ParseBlock(
        string css, ref int i, CssContext context, string? className,
        List<CssRule> rules, Dictionary<string, string> variables,
        Dictionary<string, Dictionary<string, string>> scoped, bool inUtilities)
    {
        var declarations = new List<CssDeclaration>();

        // Declarations accumulated so far must be emitted *before* we descend into a nested block,
        // or source order inverts. Tailwind writes a legacy value then overrides it inside
        // `@supports`; emitting the outer rule last would make the fallback win.
        void Flush()
        {
            if (className is null || declarations.Count == 0) return;
            rules.Add(new CssRule(className, context, declarations.ToList()));
            declarations.Clear();
        }

        while (i < css.Length)
        {
            SkipTrivia(css, ref i);
            if (i >= css.Length) break;

            if (css[i] == '}') { i++; break; }

            // Read the prelude up to '{', ';' or '}'.
            int start = i;
            int paren = 0;
            while (i < css.Length)
            {
                char c = css[i];
                if (c == '(') paren++;
                else if (c == ')') paren--;
                else if (paren == 0 && (c == '{' || c == ';' || c == '}')) break;
                i++;
            }
            if (i >= css.Length) break;

            string prelude = css.Substring(start, i - start).Trim();
            char terminator = css[i];

            if (terminator == ';')
            {
                i++;
                AddDeclaration(prelude, declarations);
                continue;
            }

            if (terminator == '}')
            {
                // A trailing declaration without its semicolon.
                AddDeclaration(prelude, declarations);
                i++;
                break;
            }

            i++; // consume '{'
            Flush();
            DispatchBlock(css, ref i, prelude, context, className, rules, variables, scoped, inUtilities);
        }

        Flush();
    }

    private static void DispatchBlock(
        string css, ref int i, string prelude, CssContext context, string? className,
        List<CssRule> rules, Dictionary<string, string> variables,
        Dictionary<string, Dictionary<string, string>> scoped, bool inUtilities)
    {
        if (prelude.StartsWith("@", StringComparison.Ordinal))
        {
            if (prelude.StartsWith("@property", StringComparison.Ordinal))
            {
                CaptureRegisteredProperty(css, ref i, prelude, variables);
                return;
            }

            if (prelude.StartsWith("@keyframes", StringComparison.Ordinal))
            {
                SkipBlock(css, ref i);
                return;
            }

            if (prelude.StartsWith("@media", StringComparison.Ordinal))
            {
                if (!CssMediaQuery.TryApply(prelude, context, out var mediaContext))
                {
                    SkipBlock(css, ref i); // a query we cannot satisfy statically
                    return;
                }
                ParseBlock(css, ref i, mediaContext, className, rules, variables, scoped, inUtilities);
                return;
            }

            if (prelude.StartsWith("@layer", StringComparison.Ordinal))
            {
                bool utilities = inUtilities || prelude.Contains("utilities");
                ParseBlock(css, ref i, context, className, rules, variables, scoped, utilities);
                return;
            }

            if (prelude.StartsWith("@supports", StringComparison.Ordinal))
            {
                // Descend. Tailwind emits a legacy value first and the modern one inside @supports;
                // both target the same property, so later-wins naturally prefers the modern branch.
                ParseBlock(css, ref i, context, className, rules, variables, scoped, inUtilities);
                return;
            }

            SkipBlock(css, ref i);
            return;
        }

        // `:root, :host { --token: value }` — the theme scope.
        if (prelude.Contains(":root") || prelude.Contains(":host"))
        {
            CaptureVariables(css, ref i, context, variables, scoped);
            return;
        }

        // Nested pseudo-class: `&:hover`, `&:active`, `&:disabled`.
        if (prelude.StartsWith("&", StringComparison.Ordinal))
        {
            var pseudo = PseudoOf(prelude);
            ParseBlock(css, ref i, context.With(pseudo), className, rules, variables, scoped, inUtilities);
            return;
        }

        // A class selector introduces (or replaces) the current class.
        if (TryClassName(prelude, out string name))
        {
            var pseudo = PseudoOf(prelude);
            ParseBlock(css, ref i, pseudo == CssPseudo.None ? context : context.With(pseudo),
                name, rules, variables, scoped, inUtilities);
            return;
        }

        SkipBlock(css, ref i);
    }

    private static void AddDeclaration(string text, List<CssDeclaration> declarations)
    {
        if (text.Length == 0) return;

        int colon = text.IndexOf(':');
        if (colon <= 0) return; // an at-statement such as `@layer properties`

        string property = text.Substring(0, colon).Trim();
        string value = text.Substring(colon + 1).Trim();

        bool important = false;
        if (value.EndsWith("!important", StringComparison.OrdinalIgnoreCase))
        {
            important = true;
            value = value.Substring(0, value.Length - "!important".Length).TrimEnd();
        }

        declarations.Add(new CssDeclaration(property, value, important));
    }

    /// <summary><c>@property --x { initial-value: V }</c> supplies the value <c>var(--x)</c> reads with no fallback.</summary>
    private static void CaptureRegisteredProperty(string css, ref int i, string prelude, Dictionary<string, string> variables)
    {
        string name = prelude.Substring("@property".Length).Trim();
        int depth = 1;
        var declarations = new List<CssDeclaration>();
        var chunk = new System.Text.StringBuilder();

        while (i < css.Length && depth > 0)
        {
            char c = css[i++];
            if (c == '{') depth++;
            else if (c == '}') { depth--; if (depth == 0) break; }
            else if (c == ';') { AddDeclaration(chunk.ToString().Trim(), declarations); chunk.Clear(); continue; }
            chunk.Append(c);
        }

        if (!name.StartsWith("--", StringComparison.Ordinal)) return;
        foreach (var d in declarations)
            if (d.Property == "initial-value")
                variables[name] = d.Value;
    }

    private static void CaptureVariables(
        string css, ref int i, CssContext context,
        Dictionary<string, string> variables, Dictionary<string, Dictionary<string, string>> scoped)
    {
        var declarations = new List<CssDeclaration>();
        int depth = 1;
        var chunk = new System.Text.StringBuilder();
        int paren = 0;

        while (i < css.Length && depth > 0)
        {
            char c = css[i++];
            if (c == '(') paren++;
            else if (c == ')') paren--;

            if (paren == 0)
            {
                if (c == '{') { depth++; continue; }
                if (c == '}') { depth--; if (depth == 0) break; continue; }
                if (c == ';') { AddDeclaration(chunk.ToString().Trim(), declarations); chunk.Clear(); continue; }
            }
            chunk.Append(c);
        }
        AddDeclaration(chunk.ToString().Trim(), declarations);

        var target = context.ColorScheme is { } scheme
            ? (scoped.TryGetValue(scheme, out var existing) ? existing : scoped[scheme] = new(StringComparer.Ordinal))
            : variables;

        foreach (var d in declarations)
            if (d.Property.StartsWith("--", StringComparison.Ordinal))
                target[d.Property] = d.Value;
    }

    private static CssPseudo PseudoOf(string selector)
    {
        if (selector.IndexOf(":hover", StringComparison.Ordinal) >= 0) return CssPseudo.Hover;
        if (selector.IndexOf(":focus", StringComparison.Ordinal) >= 0) return CssPseudo.Focus;
        if (selector.IndexOf(":active", StringComparison.Ordinal) >= 0) return CssPseudo.Active;
        if (selector.IndexOf(":disabled", StringComparison.Ordinal) >= 0) return CssPseudo.Disabled;
        return CssPseudo.None;
    }

    /// <summary>
    /// Extracts the class name from a selector, undoing CSS escapes so that
    /// <c>.hover\:bg-blue-600</c> and <c>.w-\[137px\]</c> come back as the candidates the user wrote.
    /// </summary>
    internal static bool TryClassName(string selector, out string name)
    {
        name = "";
        int dot = selector.IndexOf('.');
        if (dot < 0) return false;

        // A leading combinator or element selector means this is not a bare utility rule.
        var before = selector.Substring(0, dot).Trim();
        if (before.Length > 0 && before != "&") return false;

        var sb = new System.Text.StringBuilder();
        for (int i = dot + 1; i < selector.Length; i++)
        {
            char c = selector[i];
            if (c == '\\')
            {
                if (i + 1 < selector.Length) sb.Append(selector[++i]);
                continue;
            }
            // Unescaped ':' begins a pseudo-class; ',' / whitespace / '>' end the selector.
            if (c == ':' || c == ',' || c == ' ' || c == '>' || c == '+' || c == '~' || c == '[') break;
            sb.Append(c);
        }

        name = sb.ToString();
        return name.Length > 0;
    }

    private static void SkipBlock(string css, ref int i)
    {
        int depth = 1;
        while (i < css.Length && depth > 0)
        {
            char c = css[i++];
            if (c == '{') depth++;
            else if (c == '}') depth--;
        }
    }

    private static void SkipTrivia(string css, ref int i)
    {
        while (i < css.Length)
        {
            if (char.IsWhiteSpace(css[i])) { i++; continue; }
            if (css[i] == '/' && i + 1 < css.Length && css[i + 1] == '*')
            {
                i += 2;
                while (i + 1 < css.Length && !(css[i] == '*' && css[i + 1] == '/')) i++;
                i = Math.Min(i + 2, css.Length);
                continue;
            }
            break;
        }
    }
}
