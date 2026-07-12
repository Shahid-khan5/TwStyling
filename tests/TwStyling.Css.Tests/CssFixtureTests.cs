using TwStyling.Css;

namespace TwStyling.Css.Tests;

/// <summary>
/// Evaluates every declaration in real Tailwind v4 output, through the same parser and the same
/// scoping rules the compiler uses. An earlier version of this file used a private ad-hoc scanner
/// and asserted only that nothing threw; that let a rule-local `--tw-*` resolution bug pass. It now
/// checks resolved *values*, not just the absence of exceptions.
/// </summary>
public class CssFixtureTests
{
    private static CssStylesheet Fixture() =>
        CssStylesheetParser.Parse(File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "tailwind-v4.css")));

    /// <summary>Mirrors the compiler: a rule's own custom properties shadow the registered defaults.</summary>
    private static CssEnvironment ScopeFor(CssStylesheet sheet, CssRule rule)
    {
        Dictionary<string, string>? locals = null;
        foreach (var d in rule.Declarations)
            if (d.Property.StartsWith("--", StringComparison.Ordinal))
                (locals ??= new Dictionary<string, string>(StringComparer.Ordinal))[d.Property] = d.Value;

        return locals is null ? sheet.Environment() : new CssEnvironment(sheet.Variables, locals);
    }

    /// <summary>
    /// Every declaration must resolve against its own rule's scope — with one principled exception.
    /// The gradient utilities are a cooperative set: `.bg-linear-to-r` declares
    /// `--tw-gradient-position` while `.from-blue-500` reads it, so neither resolves alone. That
    /// composition is the plan compiler's job (see TwCssPlanCompilerTests), not this level's. Any
    /// *other* unresolved variable is a genuine failure.
    /// </summary>
    [Fact]
    public void Every_Utility_Declaration_In_Real_Tailwind_Output_Evaluates()
    {
        var sheet = Fixture();
        Assert.True(sheet.Rules.Count > 100, $"fixture looks wrong: only {sheet.Rules.Count} rules");

        var failures = new List<string>();
        var crossRule = new List<string>();
        int resolved = 0;

        foreach (var rule in sheet.Rules)
        {
            var scope = ScopeFor(sheet, rule);
            foreach (var d in rule.Declarations)
            {
                try
                {
                    var value = CssEvaluator.Evaluate(CssValueParser.Parse(d.Value), scope);
                    Assert.NotNull(value);
                    resolved++;
                }
                catch (CssEvalException ex) when (ex.Message.Contains("'--tw-gradient-"))
                {
                    crossRule.Add($".{rule.ClassName}");
                }
                catch (CssEvalException ex)
                {
                    failures.Add($".{rule.ClassName} {{ {d.Property}: {d.Value} }}  --  {ex.Message}");
                }
            }
        }

        Assert.True(
            failures.Count == 0,
            $"{resolved} declarations resolved; {failures.Count} failed:{Environment.NewLine}"
                + string.Join(Environment.NewLine, failures.Take(25)));

        // Guard the exception itself: it must stay confined to the gradient family.
        Assert.All(crossRule, name => Assert.True(
            name.Contains("gradient") || name.Contains("linear-to") || name.StartsWith(".from-")
                || name.StartsWith(".via-") || name.StartsWith(".to-"),
            $"{name} deferred a variable across rules but is not a gradient utility"));
    }

    [Fact]
    public void Theme_Colors_Resolve_To_Concrete_Rgba()
    {
        var env = Fixture().Environment();

        Assert.True(env.TryGetVariable("--color-red-500", out var red));
        Assert.Equal(0xFFFB2C36u, Assert.IsType<CssColor>(red).ToRgba());

        Assert.True(env.TryGetVariable("--color-green-400", out var green));
        Assert.Equal(0xFF05DF72u, Assert.IsType<CssColor>(green).ToRgba());
    }

    /// <summary>`@theme { --color-brand-600: oklch(0.55 0.2 260) }` — a token we never hardcoded.</summary>
    [Fact]
    public void The_Custom_Theme_Token_Survives_Into_A_Utility()
    {
        var env = Fixture().Environment();

        Assert.True(env.TryGetVariable("--color-brand-600", out var brand));
        var color = Assert.IsType<CssColor>(brand);
        Assert.Equal(1.0, color.A, 3);
        Assert.True(color.B > color.R, "brand-600 should be blue-dominant");
    }

    [Fact]
    public void Spacing_Scale_Drives_Padding()
    {
        var sheet = Fixture();
        var env = sheet.Environment();

        var rule = sheet.Rules.Single(r => r.ClassName == "p-4");
        var value = Assert.IsType<CssNumber>(CssEvaluator.Evaluate(CssValueParser.Parse(rule.Declarations[0].Value), env));

        // --spacing is 0.25rem, so p-4 is 1rem is 16px.
        Assert.Equal(16.0, CssEvaluator.ToPixels(value, env), 6);
    }

    /// <summary>
    /// The regression this file previously missed: `.pressed\:scale-95` declares `--tw-scale-x: 95%`
    /// and reads it back in the same block. Resolving against the registered default yields 1.
    /// </summary>
    [Fact]
    public void A_Rule_Local_Custom_Property_Wins_Over_Its_Registered_Default()
    {
        var sheet = Fixture();
        var rule = sheet.Rules.Single(r => r.ClassName == "pressed:scale-95");
        var scope = ScopeFor(sheet, rule);

        var scale = rule.Declarations.Single(d => d.Property == "scale");
        var value = Assert.IsType<CssList>(CssEvaluator.Evaluate(CssValueParser.Parse(scale.Value), scope));

        foreach (var component in value.Items)
            Assert.Equal(95.0, Assert.IsType<CssNumber>(component).Value, 3);

        // And without the local layer it would silently resolve to the @property initial value.
        var bare = Assert.IsType<CssList>(CssEvaluator.Evaluate(CssValueParser.Parse(scale.Value), sheet.Environment()));
        Assert.Equal(1.0, Assert.IsType<CssNumber>(bare.Items[0]).Value, 3);
    }
}
