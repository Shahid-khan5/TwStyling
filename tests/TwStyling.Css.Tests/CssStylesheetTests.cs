using TwStyling.Css;

namespace TwStyling.Css.Tests;

public class CssStylesheetTests
{
    private static CssStylesheet Fixture() =>
        CssStylesheetParser.Parse(File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "tailwind-v4.css")));

    private static IReadOnlyList<CssRule> RulesFor(CssStylesheet sheet, string className) =>
        sheet.Rules.Where(r => r.ClassName == className).ToList();

    // ------------------------------------------------------------- selectors

    [Theory]
    [InlineData(".p-4", "p-4")]
    [InlineData(".hover\\:bg-blue-600", "hover:bg-blue-600")]
    [InlineData(".w-\\[137px\\]", "w-[137px]")]
    [InlineData(".bg-blue-500\\/50", "bg-blue-500/50")]
    [InlineData(".dark\\:bg-slate-900", "dark:bg-slate-900")]
    public void Class_Names_Are_Unescaped_Back_To_The_Authored_Candidate(string selector, string expected)
    {
        Assert.True(CssStylesheetParser.TryClassName(selector, out var name));
        Assert.Equal(expected, name);
    }

    [Fact]
    public void Non_Class_Selectors_Are_Rejected()
    {
        Assert.False(CssStylesheetParser.TryClassName("stacklayout > label", out _));
        Assert.False(CssStylesheetParser.TryClassName(":root, :host", out _));
    }

    // ----------------------------------------------------------------- rules

    [Fact]
    public void A_Plain_Utility_Yields_One_Unconditional_Rule()
    {
        var rule = Assert.Single(RulesFor(Fixture(), "p-4"));
        Assert.Equal(CssPseudo.None, rule.Context.Pseudo);
        Assert.Null(rule.Context.ColorScheme);
        Assert.Equal(0, rule.Context.MinWidth);

        var decl = Assert.Single(rule.Declarations);
        Assert.Equal("padding", decl.Property);
    }

    [Fact]
    public void A_Dark_Variant_Carries_Its_Color_Scheme()
    {
        var rule = Assert.Single(RulesFor(Fixture(), "dark:bg-slate-900"));
        Assert.Equal("dark", rule.Context.ColorScheme);
        Assert.Equal("background-color", Assert.Single(rule.Declarations).Property);
    }

    /// <summary>
    /// `.hover\:bg-blue-600 { &amp;:hover { @media (hover: hover) { ... } } }` — the capability gate
    /// must be treated as satisfied, or hover styles would be dropped entirely on native.
    /// </summary>
    [Fact]
    public void A_Hover_Variant_Survives_Its_Capability_Media_Gate()
    {
        var rule = Assert.Single(RulesFor(Fixture(), "hover:bg-blue-600"));
        Assert.Equal(CssPseudo.Hover, rule.Context.Pseudo);
        Assert.Equal("background-color", Assert.Single(rule.Declarations).Property);
    }

    [Fact]
    public void A_Custom_Variant_Becomes_A_Platform_Tag()
    {
        var rule = Assert.Single(RulesFor(Fixture(), "windows:p-5"));
        Assert.Equal("windows", Assert.Single(rule.Context.MediaTypes));
        Assert.Equal("padding", Assert.Single(rule.Declarations).Property);

        var phone = Assert.Single(RulesFor(Fixture(), "phone:p-1"));
        Assert.Equal("phone", Assert.Single(phone.Context.MediaTypes));
    }

    [Fact]
    public void A_Pressed_Variant_Maps_To_The_Active_Pseudo_Class()
    {
        var rules = RulesFor(Fixture(), "pressed:scale-95");
        var rule = Assert.Single(rules);
        Assert.Equal(CssPseudo.Active, rule.Context.Pseudo);
        // --tw-scale-x/y are set alongside the `scale` shorthand.
        Assert.Contains(rule.Declarations, d => d.Property == "scale");
    }

    [Fact]
    public void Multi_Declaration_Utilities_Keep_Every_Declaration()
    {
        var rule = Assert.Single(RulesFor(Fixture(), "border-b-4"));
        Assert.Contains(rule.Declarations, d => d.Property == "border-bottom-width");
        Assert.Contains(rule.Declarations, d => d.Property == "border-bottom-style");
    }

    [Fact]
    public void Every_Scanned_Candidate_Produces_At_Least_One_Rule()
    {
        var sheet = Fixture();
        foreach (var expected in new[]
        {
            "p-4", "w-[137px]", "text-[13px]", "bg-red-500/50", "grid-cols-3",
            "shadow-lg", "rounded-lg", "line-clamp-2", "animate-spin", "order-2",
            "sm:p-2", "disabled:opacity-50", "focus:border-blue-500", "bg-[#abc123]",
        })
            Assert.True(sheet.Rules.Any(r => r.ClassName == expected), $"no rule emitted for '{expected}'");
    }

    /// <summary>
    /// Tailwind emits a legacy `color-mix(in srgb, ...)` then overrides it inside
    /// `@supports`. Both rules target the same class and property, so the modern oklab value
    /// must come later in source order for last-wins to select it.
    /// </summary>
    [Fact]
    public void A_Supports_Override_Is_Emitted_After_The_Fallback_It_Overrides()
    {
        var rules = RulesFor(Fixture(), "bg-red-500/50");
        Assert.Equal(2, rules.Count);

        Assert.Contains("in srgb", rules[0].Declarations[0].Value);
        Assert.Contains("in oklab", rules[1].Declarations[0].Value);
    }

    [Fact]
    public void A_Breakpoint_Variant_Carries_Its_Min_Width()
    {
        var rule = Assert.Single(RulesFor(Fixture(), "sm:p-2"));
        Assert.Equal(640, rule.Context.MinWidth);
        Assert.Equal("padding", Assert.Single(rule.Declarations).Property);
    }

    [Fact]
    public void A_Disabled_Variant_Maps_To_The_Disabled_Pseudo_Class()
    {
        var rule = Assert.Single(RulesFor(Fixture(), "disabled:opacity-50"));
        Assert.Equal(CssPseudo.Disabled, rule.Context.Pseudo);
    }

    // ------------------------------------------------------------- variables

    [Fact]
    public void Theme_Tokens_And_Registered_Property_Defaults_Both_Land_In_The_Environment()
    {
        var sheet = Fixture();

        Assert.True(sheet.Variables.ContainsKey("--spacing"));
        Assert.True(sheet.Variables.ContainsKey("--color-red-500"));
        // `@property --tw-border-style { initial-value: solid }`
        Assert.Equal("solid", sheet.Variables["--tw-border-style"]);
    }

    [Fact]
    public void The_Environment_Resolves_A_Utility_Value_End_To_End()
    {
        var sheet = Fixture();
        var env = sheet.Environment();

        var rule = Assert.Single(RulesFor(sheet, "p-4"));
        var value = CssEvaluator.Evaluate(CssValueParser.Parse(rule.Declarations[0].Value), env);

        Assert.Equal(16.0, CssEvaluator.ToPixels(Assert.IsType<CssNumber>(value), env), 6);
    }

    // ----------------------------------------------------------- media query

    [Fact]
    public void Breakpoint_Range_Syntax_Resolves_Rem_To_Pixels()
    {
        Assert.True(CssMediaQuery.TryApply("@media (width >= 40rem)", CssContext.Root, out var ctx));
        Assert.Equal(640, ctx.MinWidth);
        Assert.Equal(double.PositiveInfinity, ctx.MaxWidth);
    }

    [Fact]
    public void Legacy_MinWidth_Syntax_Also_Resolves()
    {
        Assert.True(CssMediaQuery.TryApply("@media (min-width: 768px)", CssContext.Root, out var ctx));
        Assert.Equal(768, ctx.MinWidth);
    }

    [Fact]
    public void Stacked_Conditions_Narrow_The_Range()
    {
        Assert.True(CssMediaQuery.TryApply("@media (width >= 40rem) and (width < 48rem)", CssContext.Root, out var ctx));
        Assert.Equal(640, ctx.MinWidth);
        Assert.Equal(767, ctx.MaxWidth);
    }

    [Fact]
    public void Unsatisfiable_Queries_Are_Reported_So_The_Block_Can_Be_Skipped()
    {
        Assert.False(CssMediaQuery.TryApply("@media not all and (width >= 40rem)", CssContext.Root, out _));
        Assert.False(CssMediaQuery.TryApply("@media print", CssContext.Root, out _));
        Assert.False(CssMediaQuery.TryApply("@media (orientation: landscape)", CssContext.Root, out _));
    }

    [Fact]
    public void Capability_Gates_Are_Treated_As_Satisfied()
    {
        Assert.True(CssMediaQuery.TryApply("@media (hover: hover)", CssContext.Root, out var ctx));
        Assert.Equal(CssContext.Root.MinWidth, ctx.MinWidth);
        Assert.Null(ctx.ColorScheme);
    }
}
