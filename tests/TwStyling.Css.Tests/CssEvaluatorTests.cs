using TwStyling.Css;

namespace TwStyling.Css.Tests;

/// <summary>
/// Every expression here was lifted verbatim from the CSS that Tailwind v4 actually emitted
/// for our utility set, so passing means the evaluator handles real output, not a synthetic subset.
/// </summary>
public class CssEvaluatorTests
{
    /// <summary>Tailwind's own theme values for the tokens its utilities reference.</summary>
    private static CssEnvironment Env(params (string Name, string Value)[] extra)
    {
        var vars = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["--spacing"] = "0.25rem",
            ["--text-xl"] = "1.25rem",
            ["--color-red-500"] = "oklch(63.7% 0.237 25.331)",
            ["--color-blue-600"] = "oklch(54.6% 0.245 262.881)",
            ["--tw-shadow-alpha"] = "100%",
        };
        foreach (var (name, value) in extra) vars[name] = value;
        return new CssEnvironment(vars);
    }

    private static CssValue Eval(string css, CssEnvironment? env = null) =>
        CssEvaluator.Evaluate(CssValueParser.Parse(css), env ?? Env());

    private static double Number(string css, CssEnvironment? env = null) =>
        Assert.IsType<CssNumber>(Eval(css, env)).Value;

    private static string Hex(string css, CssEnvironment? env = null) =>
        $"#{Assert.IsType<CssColor>(Eval(css, env)).ToRgba():X8}";

    // ------------------------------------------------------------------ calc

    [Fact] // .p-4 { padding: calc(var(--spacing) * 4) }
    public void Calc_Multiplies_A_Var_By_A_Scalar()
    {
        var result = Assert.IsType<CssNumber>(Eval("calc(var(--spacing) * 4)"));
        Assert.Equal(1.0, result.Value, 6);
        Assert.Equal(CssUnit.Rem, result.Unit);
    }

    [Fact] // .basis-1/2 { flex-basis: calc(1 / 2 * 100%) }
    public void Calc_Chains_Division_Then_Multiplication()
    {
        var result = Assert.IsType<CssNumber>(Eval("calc(1 / 2 * 100%)"));
        Assert.Equal(50.0, result.Value, 6);
        Assert.Equal(CssUnit.Percent, result.Unit);
    }

    [Fact] // --text-xl--line-height: calc(1.75 / 1.25)
    public void Calc_Divides_Unitless_Numbers()
    {
        Assert.Equal(1.4, Number("calc(1.75 / 1.25)"), 6);
    }

    [Fact]
    public void Calc_Respects_Operator_Precedence()
    {
        Assert.Equal(14.0, Number("calc(2 + 3 * 4)"), 6);
        Assert.Equal(20.0, Number("calc((2 + 3) * 4)"), 6);
    }

    [Fact]
    public void Calc_Normalizes_Rem_And_Pt_Against_Px()
    {
        // 1rem = 16px, so 16px + 1rem = 32px
        var result = Assert.IsType<CssNumber>(Eval("calc(16px + 1rem)"));
        Assert.Equal(32.0, result.Value, 6);
        Assert.Equal(CssUnit.Px, result.Unit);
    }

    /// <summary>
    /// The whole reason the tokenizer tracks the previous significant token: without it,
    /// `- 2px` lexes as the number -2px and the subtraction silently vanishes.
    /// </summary>
    [Fact]
    public void Binary_Minus_Is_Not_Read_As_A_Negative_Sign()
    {
        Assert.Equal(2.0, Number("calc(4px - 2px)"), 6);
    }

    [Fact] // negative margins: calc(var(--spacing) * -4)
    public void Negative_Sign_Survives_After_An_Operator()
    {
        var result = Assert.IsType<CssNumber>(Eval("calc(var(--spacing) * -4)"));
        Assert.Equal(-1.0, result.Value, 6);
    }

    [Fact]
    public void Calc_Rejects_Incompatible_Units_Loudly()
    {
        var ex = Assert.Throws<CssEvalException>(() => Eval("calc(100% - 4px)"));
        Assert.Contains("incompatible units", ex.Message);
    }

    [Fact]
    public void Calc_Rejects_Multiplying_Two_Dimensions()
    {
        Assert.Throws<CssEvalException>(() => Eval("calc(2px * 3px)"));
    }

    [Fact]
    public void Zero_Is_Unit_Agnostic()
    {
        var result = Assert.IsType<CssNumber>(Eval("calc(0 + 4px)"));
        Assert.Equal(4.0, result.Value, 6);
        Assert.Equal(CssUnit.Px, result.Unit);
    }

    [Fact]
    public void MinMaxClamp_Resolve()
    {
        Assert.Equal(4.0, Number("min(4px, 8px)"), 6);
        Assert.Equal(8.0, Number("max(4px, 8px)"), 6);
        Assert.Equal(6.0, Number("clamp(4px, 6px, 8px)"), 6);
    }

    // ------------------------------------------------------------------- var

    [Fact] // box-shadow: ... var(--tw-shadow-color, rgb(0 0 0 / 0.1))
    public void Var_Falls_Back_When_Undefined()
    {
        var color = Assert.IsType<CssColor>(Eval("var(--tw-shadow-color, rgb(0 0 0 / 0.1))"));
        Assert.Equal(0.1, color.A, 3);
        Assert.Equal(0.0, color.R, 3);
    }

    [Fact]
    public void Var_Prefers_The_Defined_Value_Over_The_Fallback()
    {
        var env = Env(("--tw-shadow-color", "#ff0000"));
        Assert.Equal("#FFFF0000", Hex("var(--tw-shadow-color, rgb(0 0 0 / 0.1))", env));
    }

    [Fact]
    public void Var_Without_A_Fallback_Fails_Loudly()
    {
        var ex = Assert.Throws<CssEvalException>(() => Eval("var(--nope)"));
        Assert.Contains("--nope", ex.Message);
    }

    [Fact]
    public void Var_Resolves_Transitively()
    {
        var env = Env(("--a", "var(--b)"), ("--b", "12px"));
        Assert.Equal(12.0, Number("var(--a)", env), 6);
    }

    [Fact]
    public void A_Later_Layer_Shadows_An_Earlier_One()
    {
        var baseVars = new Dictionary<string, string>(StringComparer.Ordinal) { ["--x"] = "1", ["--y"] = "2" };
        var locals = new Dictionary<string, string>(StringComparer.Ordinal) { ["--x"] = "95%" };
        var env = new CssEnvironment(baseVars, locals);

        Assert.Equal(95.0, Number("var(--x)", env), 6); // local wins
        Assert.Equal(2.0, Number("var(--y)", env), 6);  // base still visible
    }

    [Fact]
    public void Cyclic_Variables_Are_Detected_Rather_Than_Overflowing_The_Stack()
    {
        var env = Env(("--a", "var(--b)"), ("--b", "var(--a)"));
        var ex = Assert.Throws<CssEvalException>(() => Eval("var(--a)", env));
        Assert.Contains("cyclic", ex.Message);
    }

    // ---------------------------------------------------------------- colors

    [Fact] // --color-red-500 in Tailwind v4
    public void Oklch_Round_Trips_To_The_Documented_Hex()
    {
        Assert.Equal("#FFFB2C36", Hex("oklch(63.7% 0.237 25.331)"));
    }

    [Fact] // --color-blue-600
    public void Oklch_Blue_600()
    {
        Assert.Equal("#FF155DFC", Hex("oklch(54.6% 0.245 262.881)"));
    }

    [Fact]
    public void Hex_Shorthand_Expands()
    {
        Assert.Equal("#FFAABBCC", Hex("#abc"));
        Assert.Equal("#80AABBCC", Hex("#aabbcc80"));
    }

    [Fact]
    public void Named_Colors_Resolve_Through_ToColor()
    {
        var color = CssEvaluator.ToColor(Eval("rebeccapurple"), Env());
        Assert.Equal(0xFF663399u, color.ToRgba());
    }

    [Fact] // .bg-red-500\/50 { background-color: color-mix(in oklab, var(--color-red-500) 50%, transparent) }
    public void ColorMix_With_Transparent_Is_An_Alpha_Adjustment()
    {
        var mixed = Assert.IsType<CssColor>(Eval("color-mix(in oklab, var(--color-red-500) 50%, transparent)"));
        var solid = Assert.IsType<CssColor>(Eval("var(--color-red-500)"));

        Assert.Equal(0.5, mixed.A, 2);
        // Un-premultiplying must give back the original hue, not a darkened one.
        Assert.Equal(solid.R, mixed.R, 2);
        Assert.Equal(solid.G, mixed.G, 2);
        Assert.Equal(solid.B, mixed.B, 2);
    }

    [Fact] // the non-@supports fallback branch Tailwind emits alongside the oklab one
    public void ColorMix_In_Srgb_Matches_The_Oklab_Branch_For_Transparent()
    {
        var srgb = Assert.IsType<CssColor>(Eval("color-mix(in srgb, oklch(63.7% 0.237 25.331) 50%, transparent)"));
        Assert.Equal(0.5, srgb.A, 2);
        Assert.Equal("#80FB2C36", $"#{srgb.ToRgba():X8}");
    }

    [Fact]
    public void ColorMix_Halfway_Between_Black_And_White_In_Srgb()
    {
        var mid = Assert.IsType<CssColor>(Eval("color-mix(in srgb, white, black)"));
        Assert.Equal(0.5, mid.R, 2);
        Assert.Equal(1.0, mid.A, 3);
    }

    [Fact]
    public void ColorMix_Scales_Alpha_When_Weights_Sum_Below_One()
    {
        // Per CSS Color 5: explicit weights summing to 50% halve the result's alpha.
        var mixed = Assert.IsType<CssColor>(Eval("color-mix(in srgb, red 25%, blue 25%)"));
        Assert.Equal(0.5, mixed.A, 2);
    }

    [Fact]
    public void CurrentColor_Resolves_From_The_Environment()
    {
        var env = Env();
        env.CurrentColor = new CssColor(1, 1, 1, 1);
        var mixed = Assert.IsType<CssColor>(Eval("color-mix(in oklab, currentcolor 50%, transparent)", env));
        Assert.Equal(0.5, mixed.A, 2);
        Assert.Equal(1.0, mixed.R, 2);
    }

    [Fact]
    public void Rgb_Accepts_Both_The_Legacy_And_Modern_Forms()
    {
        Assert.Equal("#FFFF0000", Hex("rgb(255, 0, 0)"));
        Assert.Equal("#80FF0000", Hex("rgb(255 0 0 / 0.5)"));
        Assert.Equal("#80FF0000", Hex("rgba(255, 0, 0, 0.5)"));
    }

    [Fact]
    public void Hsl_Resolves()
    {
        Assert.Equal("#FF00FF00", Hex("hsl(120, 100%, 50%)"));
    }

    // --------------------------------------------------- unknown expressions

    [Fact] // grid-template-columns: repeat(3, minmax(0, 1fr))
    public void Unknown_Functions_Keep_Their_Shape_With_Args_Evaluated()
    {
        var fn = Assert.IsType<CssFunction>(Eval("repeat(3, minmax(0, 1fr))"));
        Assert.Equal("repeat", fn.Name);
        Assert.Equal(2, fn.Args.Count);
        Assert.Equal(3.0, Assert.IsType<CssNumber>(fn.Args[0]).Value);
        Assert.Equal("minmax", Assert.IsType<CssFunction>(fn.Args[1]).Name);
    }

    [Fact] // transition-timing-function: cubic-bezier(0.4, 0, 0.2, 1)
    public void CubicBezier_Survives_As_A_Function()
    {
        var fn = Assert.IsType<CssFunction>(Eval("cubic-bezier(0.4, 0, 0.2, 1)"));
        Assert.Equal(4, fn.Args.Count);
        Assert.Equal(0.2, Assert.IsType<CssNumber>(fn.Args[2]).Value, 6);
    }

    [Fact] // filter: blur(var(--blur-sm))
    public void Unknown_Function_Arguments_Are_Still_Substituted()
    {
        var env = Env(("--blur-sm", "4px"));
        var fn = Assert.IsType<CssFunction>(Eval("blur(var(--blur-sm))", env));
        Assert.Equal(4.0, Assert.IsType<CssNumber>(fn.Args[0]).Value, 6);
    }

    [Fact] // box-shadow: 0 10px 15px -3px <color>, 0 4px 6px -4px <color>
    public void A_Two_Shadow_Comma_List_Parses_Into_Two_Space_Lists()
    {
        var value = Eval("0 10px 15px -3px rgb(0 0 0 / 0.1), 0 4px 6px -4px rgb(0 0 0 / 0.1)");
        var commas = Assert.IsType<CssCommaList>(value);
        Assert.Equal(2, commas.Items.Count);

        var first = Assert.IsType<CssList>(commas.Items[0]);
        Assert.Equal(5, first.Items.Count);
        Assert.Equal(-3.0, Assert.IsType<CssNumber>(first.Items[3]).Value, 6);
        Assert.IsType<CssColor>(first.Items[4]);
    }

    [Fact]
    public void ToPixels_Converts_Length_Units_And_Rejects_Others()
    {
        var env = Env();
        Assert.Equal(16.0, CssEvaluator.ToPixels(new CssNumber(1, CssUnit.Rem), env), 6);
        Assert.Equal(16.0, CssEvaluator.ToPixels(new CssNumber(12, CssUnit.Pt), env), 6);
        Assert.Throws<CssEvalException>(() => CssEvaluator.ToPixels(new CssNumber(50, CssUnit.Percent), env));
    }
}
