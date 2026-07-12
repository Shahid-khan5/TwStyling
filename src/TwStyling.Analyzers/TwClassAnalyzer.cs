using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using TwStyling;

namespace TwStyling.Analyzers;

/// <summary>
/// Validates Tailwind class-string literals in C# (<c>.Tw("...")</c> / <c>Tw.SetClass(el, "...")</c>)
/// against the stylesheet Tailwind compiled for the project — the same source of truth the source
/// generator lowers. A typo becomes a build warning instead of a silent runtime no-op, which matters
/// double for AI-generated code, and anything Tailwind resolves (arbitrary values, <c>@theme</c>
/// tokens, plugins) is accepted because Tailwind itself accepted it.
///
/// XAML literals are validated by the generator (TWG001), which already reads the same stylesheet.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class TwClassAnalyzer : DiagnosticAnalyzer
{
    public static readonly DiagnosticDescriptor InvalidClass = new(
        id: "TW0001",
        title: "Invalid Tw utility class",
        messageFormat: "'{0}': {1}",
        category: "TwStyling",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "The class string contains a utility the Tw engine cannot resolve. At runtime this is reported through the diagnostic sink and the utility is skipped.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [InvalidClass];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        // Parse the stylesheet once per compilation, not once per call site.
        context.RegisterCompilationStartAction(start =>
        {
            var stylesheet = LoadStylesheet(start.Options);
            if (stylesheet is null)
                return; // No compiled stylesheet: we have no vocabulary to validate against.

            start.RegisterSyntaxNodeAction(
                node => AnalyzeInvocation(node, stylesheet),
                SyntaxKind.InvocationExpression);
        });
    }

    /// <summary>
    /// The stylesheet the Tailwind CLI produced for this project, handed to us as an AdditionalFile
    /// and identified by $(TwGeneratedCss) — never by filename, because the MAUI SDK also pushes the
    /// project's *entry* stylesheet into AdditionalFiles and that one holds no rules.
    /// </summary>
    private static TwCssPlanCompiler? LoadStylesheet(AnalyzerOptions options)
    {
        if (!options.AnalyzerConfigOptionsProvider.GlobalOptions
                .TryGetValue("build_property.TwGeneratedCss", out var expected)
            || string.IsNullOrEmpty(expected))
            return null;

        foreach (var file in options.AdditionalFiles)
        {
            if (!IsSameFile(file.Path, expected))
                continue;

            var text = file.GetText()?.ToString();
            if (string.IsNullOrEmpty(text))
                return null;

            try
            {
                return TwCssPlanCompiler.FromCss(text!);
            }
            catch
            {
                return null; // A malformed stylesheet must not spray diagnostics across the project.
            }
        }
        return null;
    }

    /// <summary>$(TwGeneratedCss) is project-relative; the AdditionalFile path is absolute.</summary>
    private static bool IsSameFile(string additionalFilePath, string expected)
    {
        static string Normalize(string path) => path.Replace('\\', '/').TrimStart('.', '/');

        var left = Normalize(additionalFilePath);
        var right = Normalize(expected);

        return left.EndsWith(right, System.StringComparison.OrdinalIgnoreCase)
            || right.EndsWith(left, System.StringComparison.OrdinalIgnoreCase);
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context, TwCssPlanCompiler stylesheet)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        var name = invocation.Expression switch
        {
            MemberAccessExpressionSyntax member => member.Name.Identifier.Text,
            IdentifierNameSyntax identifier => identifier.Identifier.Text,
            _ => null,
        };
        if (name is not ("Tw" or "SetClass"))
            return;

        // Cheap name filter first, then confirm it's really our API.
        if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol
            is not IMethodSymbol method)
            return;
        var ns = method.ContainingType?.ContainingNamespace?.ToDisplayString();
        if (ns is not ("TwStyling.Maui" or "TwStyling.Wpf"))
            return;

        foreach (var argument in invocation.ArgumentList.Arguments)
        {
            if (argument.Expression is not LiteralExpressionSyntax literal
                || !literal.IsKind(SyntaxKind.StringLiteralExpression))
                continue;

            // Every platform and idiom applies: the analyzer reports for the whole solution, not
            // for the head that happens to be compiling.
            var environment = new TwEnvironment(TwPlatforms.Any, TwIdioms.Any);

            foreach (var diagnostic in stylesheet.Compile(literal.Token.ValueText, environment).Diagnostics)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    InvalidClass, literal.GetLocation(), diagnostic.Token, diagnostic.Message));
            }
        }
    }
}
