using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Tw.Core;

namespace Tw.Analyzers;

/// <summary>
/// Validates Tailwind class-string literals at build time using the same parser
/// the runtime uses. A typo'd utility becomes a build diagnostic with the
/// engine's suggestion instead of a silent runtime no-op — this matters double
/// for AI-generated code, which occasionally emits web-only utilities.
/// Covers C# (`.Tw("...")` / `Tw.SetClass(el, "...")`); XAML validation needs a
/// build task and is planned for v1.
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
        context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
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
        if (ns is not ("Tw.Maui" or "Tw.Wpf"))
            return;

        foreach (var argument in invocation.ArgumentList.Arguments)
        {
            if (argument.Expression is not LiteralExpressionSyntax literal
                || !literal.IsKind(SyntaxKind.StringLiteralExpression))
                continue;

            var classes = literal.Token.ValueText;
            foreach (var diagnostic in TwEngine.Validate(classes))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    InvalidClass, literal.GetLocation(), diagnostic.Token, diagnostic.Message));
            }
        }
    }
}
