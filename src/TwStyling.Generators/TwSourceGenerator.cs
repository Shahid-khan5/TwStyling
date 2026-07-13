using System.Collections.Immutable;
using System.Text;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using TwStyling;
using TwStyling.Css;

namespace TwStyling.Generators;

/// <summary>
/// Lowers Tailwind's compiled stylesheet into style plans at BUILD TIME, so the runtime does no
/// compilation at all:
/// - every literal <c>.Tw("...")</c> call in C# and every <c>tw:Tw.Class</c> / <c>Tw.ActiveClass</c>
///   literal in XAML becomes a <see cref="StylePlan"/>, preloaded into the engine cache by a
///   <c>[ModuleInitializer]</c>. Applying that class at runtime is a dictionary hit.
/// - invalid XAML class strings become build errors (TWG001).
/// - class strings that cannot be known at build time — interpolated strings, and <c>idiom:</c>
///   variants that depend on the device — are resolved at runtime against the same stylesheet,
///   which the build embeds into the app assembly. There is only ever one vocabulary.
///
/// C# and XAML share the SAME preload path — there is no C#-only interceptor fast lane.
/// (Interceptors were removed: they optimized only C#, couldn't help XAML, and forced a
/// second codegen path to stay byte-for-byte in sync with the runtime lowering.)
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class TwSourceGenerator : IIncrementalGenerator
{
    private static readonly DiagnosticDescriptor XamlInvalidClass = new(
        id: "TWG001",
        title: "Invalid Tw utility class in XAML",
        messageFormat: "{0}: '{1}' in \"{2}\": {3}",
        category: "TwStyling",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Harvest the class string from every literal .Tw("...") call so it can be
        // precompiled and preloaded — same destination as XAML strings.
        var callStrings = context.SyntaxProvider.CreateSyntaxProvider(
                predicate: static (node, _) =>
                    node is InvocationExpressionSyntax { ArgumentList.Arguments.Count: 1 } invocation
                    && invocation.Expression is MemberAccessExpressionSyntax { Name.Identifier.Text: "Tw" }
                    && invocation.ArgumentList.Arguments[0].Expression is LiteralExpressionSyntax literal
                    && literal.IsKind(SyntaxKind.StringLiteralExpression),
                transform: static (ctx, ct) =>
                {
                    var invocation = (InvocationExpressionSyntax)ctx.Node;
                    if (ctx.SemanticModel.GetSymbolInfo(invocation, ct).Symbol is not IMethodSymbol method
                        || method.ContainingType?.ToDisplayString() != "TwStyling.Maui.TwExtensions")
                        return null;
                    return ((LiteralExpressionSyntax)invocation.ArgumentList.Arguments[0].Expression).Token.ValueText;
                })
            .Where(static classes => classes is not null)
            .Collect();

        var xamlFiles = context.AdditionalTextsProvider
            .Where(static file => file.Path.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase))
            .Select(static (file, ct) => (file.Path, Strings: ExtractXamlClassStrings(file.GetText(ct)?.ToString())))
            .Collect();

        // The stylesheet the real Tailwind CLI produced for this project (see TwStyling.Maui.targets).
        // It is the only source of truth for what a utility means; with no stylesheet there is no
        // vocabulary, and the generator emits nothing.
        //
        // Matched against the TwGeneratedCss build property, never by filename: the MAUI SDK globs
        // *.css as MauiCss and adds the project's *entry* stylesheet to AdditionalFiles by itself.
        // That file holds only @import/@custom-variant and no rules, so picking it up by name would
        // silently make every utility look unknown.
        var tailwindCss = context.AdditionalTextsProvider
            .Combine(context.AnalyzerConfigOptionsProvider)
            .Where(static pair =>
                pair.Right.GlobalOptions.TryGetValue("build_property.TwGeneratedCss", out var expected)
                && !string.IsNullOrEmpty(expected)
                && IsSameFile(pair.Left.Path, expected))
            .Select(static (pair, ct) => pair.Left.GetText(ct)?.ToString())
            .Where(static css => !string.IsNullOrEmpty(css))
            .Collect();

        var hasTwMaui = context.CompilationProvider.Select(static (compilation, _) =>
            compilation.GetTypeByMetadataName("TwStyling.Maui.TwRuntime") is not null);

        // MAUI compiles one head per platform (net10.0-ios, -android, -windows…), so the
        // target platform is fixed and known at build time. Read it from the TargetFramework
        // moniker; this lets us statically resolve platform: variants (ios:/android:/…) for
        // THIS head instead of leaving them to the runtime.
        var platform = context.AnalyzerConfigOptionsProvider.Select(static (provider, _) =>
            provider.GlobalOptions.TryGetValue("build_property.TargetFramework", out var tfm)
                ? DetectPlatform(tfm)
                : (TwPlatforms?)null);

        context.RegisterSourceOutput(
            callStrings.Combine(xamlFiles).Combine(hasTwMaui).Combine(platform).Combine(tailwindCss),
            static (spc, input) =>
            {
                var ((((csharp, xaml), twMauiReferenced), targetPlatform), css) = input;
                if (!twMauiReferenced)
                    return;
                Execute(spc, csharp, xaml, targetPlatform, css);
            });
    }

    /// <summary>
    /// Whether an AdditionalFile is the stylesheet named by $(TwGeneratedCss). The property is
    /// project-relative while the AdditionalFile path is absolute, so compare on the tail rather
    /// than resolving a working directory the generator does not have.
    /// </summary>
    private static bool IsSameFile(string additionalFilePath, string expected)
    {
        static string Normalize(string path) => path.Replace('\\', '/').TrimStart('.', '/');

        var left = Normalize(additionalFilePath);
        var right = Normalize(expected);

        return left.EndsWith(right, StringComparison.OrdinalIgnoreCase)
            || right.EndsWith(left, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The platform this compilation targets, parsed from the TargetFramework moniker
    /// (e.g. "net10.0-ios" → Ios, "net10.0-windows10.0.19041.0" → Windows). Null for a
    /// platform-neutral head (plain "net10.0"), where platform: variants stay dynamic.
    /// </summary>
    private static TwPlatforms? DetectPlatform(string targetFramework)
    {
        int dash = targetFramework.IndexOf('-');
        if (dash < 0)
            return null;

        // Take the platform identifier (leading letters) after the dash, dropping any
        // trailing OS version — "windows10.0.19041.0" → "windows".
        var rest = targetFramework.Substring(dash + 1);
        int end = 0;
        while (end < rest.Length && char.IsLetter(rest[end]))
            end++;

        return rest.Substring(0, end).ToLowerInvariant() switch
        {
            "windows" => TwPlatforms.Windows,
            "ios" => TwPlatforms.Ios,
            "android" => TwPlatforms.Android,
            "maccatalyst" or "macos" => TwPlatforms.Mac,
            "tizen" => TwPlatforms.Tizen,
            _ => null,
        };
    }

    // ----------------------------------------------------------------- xaml scan

    /// <summary>All Tw.Class/Tw.ActiveClass literals in one XAML file (with line info), plus base+active combos.</summary>
    private static ImmutableArray<(string Classes, int Line, int Column)> ExtractXamlClassStrings(string? xaml)
    {
        if (string.IsNullOrEmpty(xaml))
            return ImmutableArray<(string, int, int)>.Empty;

        var strings = ImmutableArray.CreateBuilder<(string, int, int)>();
        try
        {
            var doc = XDocument.Parse(xaml, LoadOptions.SetLineInfo);
            foreach (var element in doc.Descendants())
            {
                string? classes = null, active = null;
                int line = 1, column = 1;
                foreach (var attribute in element.Attributes())
                {
                    if (attribute.Name.LocalName is not ("Tw.Class" or "Tw.ActiveClass"))
                        continue;
                    if (attribute is System.Xml.IXmlLineInfo info && info.HasLineInfo())
                    {
                        line = info.LineNumber;
                        column = info.LinePosition;
                    }
                    if (attribute.Name.LocalName == "Tw.Class") classes = attribute.Value;
                    else active = attribute.Value;
                }
                if (classes is { Length: > 0 }) strings.Add((classes, line, column));
                if (active is { Length: > 0 }) strings.Add((active, line, column));
                if (classes is { Length: > 0 } && active is { Length: > 0 })
                    strings.Add(($"{classes} {active}", line, column)); // Tw.EffectiveClass composition
            }
        }
        catch
        {
            // Malformed XAML surfaces through the XAML compiler; nothing for us to do.
        }
        return strings.ToImmutable();
    }

    // ----------------------------------------------------------------- generation

    private static void Execute(
        SourceProductionContext spc,
        ImmutableArray<string?> csharpStrings,
        ImmutableArray<(string Path, ImmutableArray<(string Classes, int Line, int Column)> Strings)> xaml,
        TwPlatforms? targetPlatform,
        ImmutableArray<string?> tailwindCss)
    {
        // With the platform known, resolve platform: variants for it; leave Idioms open
        // (a phone and a tablet share one build head, so idiom: variants stay dynamic).
        var environment = new TwEnvironment(targetPlatform ?? TwPlatforms.Any, TwIdioms.Any);

        // The compiled stylesheet defines the whole vocabulary — arbitrary values, @theme tokens,
        // @utility, @custom-variant. There is nothing else to consult.
        TwCssPlanCompiler? cssCompiler = null;
        foreach (var css in tailwindCss)
        {
            if (string.IsNullOrEmpty(css)) continue;
            try
            {
                cssCompiler = TwCssPlanCompiler.FromCss(css!);
                break;
            }
            catch
            {
                // A malformed stylesheet must not take the build down: fall back to the parser.
            }
        }

        // No stylesheet, no vocabulary: the project opted out of the CSS pipeline
        // (TwUseCssPipeline=false), so there is nothing to precompile.
        if (cssCompiler is null)
            return;

        // Compile every unique literal whose plan is fully knowable at build time.
        var plans = new Dictionary<string, StylePlan>(StringComparer.Ordinal);

        void TryCompile(string classes, string? xamlPath, int line = 1, int column = 1)
        {
            if (plans.ContainsKey(classes) || classes.Contains('{'))
                return;

            // The idiom is a device fact, not a build fact — one iOS head serves iPhone and iPad —
            // so those strings are left for the runtime to resolve against the same stylesheet.
            if (cssCompiler.HasIdiomVariant(classes))
                return;

            var plan = cssCompiler.Compile(classes, environment);

            if (plan.Diagnostics.Length > 0)
            {
                if (xamlPath is not null)
                {
                    var position = new LinePosition(Math.Max(0, line - 1), Math.Max(0, column - 1));
                    var location = Location.Create(xamlPath, default, new LinePositionSpan(position, position));
                    foreach (var d in plan.Diagnostics)
                        spc.ReportDiagnostic(Diagnostic.Create(XamlInvalidClass, location,
                            System.IO.Path.GetFileName(xamlPath), d.Token, d.ClassString, d.Message));
                }
                return; // invalid strings stay on the runtime path (C# ones are TW0001 warnings already)
            }
            plans[classes] = plan;
        }

        foreach (var (path, strings) in xaml)
            foreach (var (classes, line, column) in strings)
                TryCompile(classes, path, line, column);
        foreach (var classes in csharpStrings)
            if (classes is not null)
                TryCompile(classes, null);

        if (plans.Count == 0)
            return;

        var fields = new Dictionary<string, string>(StringComparer.Ordinal);
        int index = 0;
        foreach (var kv in plans)
            fields[kv.Key] = $"P{index++}";

        EmitPlans(spc, plans, fields);
    }


    private static void EmitPlans(
        SourceProductionContext spc, Dictionary<string, StylePlan> plans, Dictionary<string, string> fields)
    {
        var sb = new StringBuilder();
        sb.Append("// <auto-generated by TwStyling.Generators — build-time-compiled style plans />\n");
        sb.Append("namespace Tw.Generated;\n\n");
        sb.Append("internal static class TwPreloadedPlans\n{\n");

        foreach (var kv in plans)
        {
            sb.Append($"    // {SymbolDisplay.FormatLiteral(kv.Key, quote: false)}\n");
            sb.Append($"    internal static readonly global::TwStyling.StylePlan {fields[kv.Key]} = {PlanEmitter.Emit(kv.Value)};\n\n");
        }

        sb.Append("    [global::System.Runtime.CompilerServices.ModuleInitializer]\n");
        sb.Append("    internal static void Preload()\n    {\n");
        foreach (var kv in plans)
            sb.Append($"        global::TwStyling.Maui.TwRuntime.Preload({SymbolDisplay.FormatLiteral(kv.Key, quote: true)}, {fields[kv.Key]});\n");
        sb.Append("    }\n}\n");

        spc.AddSource("TwPlans.g.cs", SourceText.From(sb.ToString(), Encoding.UTF8));
    }
}
