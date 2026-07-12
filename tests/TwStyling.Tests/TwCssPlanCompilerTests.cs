using TwStyling;
using TwStyling.Css;

namespace TwStyling.Tests;

/// <summary>
/// The CSS front end must produce the same <see cref="StylePlan"/> shape the class-name parser does,
/// while additionally handling things the parser never could: arbitrary values, custom theme tokens,
/// and user-declared variants.
/// </summary>
public class TwCssPlanCompilerTests
{
    private static readonly Lazy<CssStylesheet> Sheet = new(() =>
        CssStylesheetParser.Parse(File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "tailwind-v4.css"))));

    private static readonly TwEnvironment Windows = new(TwPlatforms.Windows, TwIdioms.Desktop);

    private static StylePlan Compile(string classes, TwEnvironment? env = null) =>
        new TwCssPlanCompiler(Sheet.Value).Compile(classes, env ?? Windows);

    private static TwValue ValueOf(TwDeclaration[] set, TwPropertyId id) =>
        set.Single(d => d.Property == id).Value;

    private static bool Has(TwDeclaration[] set, TwPropertyId id) => set.Any(d => d.Property == id);

    // -------------------------------------------------------------- basics

    [Fact]
    public void Spacing_Lowers_Through_The_Theme_Scale()
    {
        // --spacing: 0.25rem → p-4 = 1rem = 16px
        var padding = ValueOf(Compile("p-4").Light, TwPropertyId.Padding);
        Assert.Equal(TwValueKind.Edges, padding.Kind);
        Assert.Equal(16f, padding.X);
        Assert.Equal(16f, padding.W);
    }

    [Fact]
    public void A_Palette_Color_Lowers_To_Rgba()
    {
        var background = ValueOf(Compile("bg-red-500").Light, TwPropertyId.Background);
        Assert.Equal(TwValueKind.Color, background.Kind);
        Assert.Equal(0xFFFB2C36u, background.Rgba);
    }

    [Fact]
    public void An_Opacity_Modifier_Lands_In_The_Alpha_Channel()
    {
        var background = ValueOf(Compile("bg-red-500/50").Light, TwPropertyId.Background);
        Assert.Equal(0x80u, background.Rgba >> 24);
        Assert.Equal(0xFB2C36u, background.Rgba & 0x00FFFFFF);
    }

    // ------------------------------------- things TwParser could never do

    [Fact]
    public void Arbitrary_Values_Just_Work()
    {
        Assert.Equal(137f, ValueOf(Compile("w-[137px]").Light, TwPropertyId.Width).X);
        Assert.Equal(13f, ValueOf(Compile("text-[13px]").Light, TwPropertyId.FontSize).X);
        Assert.Equal(7f, ValueOf(Compile("rounded-[7px]").Light, TwPropertyId.CornerRadius).X);
    }

    [Fact]
    public void An_Arbitrary_Hex_Color_Resolves()
    {
        var background = ValueOf(Compile("bg-[#abc123]").Light, TwPropertyId.Background);
        Assert.Equal(0xFFABC123u, background.Rgba);
    }

    /// <summary>`@theme { --color-brand-600: oklch(0.55 0.2 260) }` — a token we never hardcoded.</summary>
    [Fact]
    public void A_User_Defined_Theme_Token_Resolves()
    {
        var color = ValueOf(Compile("text-brand-600").Light, TwPropertyId.TextColor);
        Assert.Equal(TwValueKind.Color, color.Kind);
        Assert.Equal(0xFFu, color.Rgba >> 24);

        uint r = (color.Rgba >> 16) & 0xFF, b = color.Rgba & 0xFF;
        Assert.True(b > r, "brand-600 should be blue-dominant");
    }

    /// <summary>
    /// A rule that declares `--tw-scale-x: 95%` and then reads `scale: var(--tw-scale-x) …` must see
    /// its own local, not the `@property` initial value of 1. Same shape as `--tw-shadow` below.
    /// </summary>
    [Fact]
    public void Rule_Local_Custom_Properties_Shadow_Their_Registered_Defaults()
    {
        var state = Assert.Single(Compile("pressed:scale-95").States);
        Assert.Equal(0.95f, ValueOf(state.Light, TwPropertyId.Scale).X, 3);
    }

    [Fact]
    public void A_Shadow_Reads_Back_The_Local_It_Declared()
    {
        var shadow = ValueOf(Compile("shadow-lg").Light, TwPropertyId.Shadow);
        Assert.Equal(TwValueKind.Shadow, shadow.Kind);

        // shadow-lg is `0 10px 15px -3px <color>`: a real offset and blur, not the `0 0 #0000` default.
        Assert.Equal(0f, shadow.X);
        Assert.Equal(10f, shadow.Y);
        Assert.Equal(15f, shadow.Z);
        Assert.NotEqual(0u, shadow.Rgba >> 24);
    }

    // ---------------------------------------------------------- gradients

    /// <summary>
    /// Gradients are the one feature whose state spans several class rules communicating through
    /// custom properties: `background-image` sits on `.bg-linear-to-r` but reads `--tw-gradient-from`,
    /// declared only by `.from-blue-500`. Scope must accumulate across the whole class string.
    /// </summary>
    [Fact]
    public void A_Gradient_Composes_Across_Several_Class_Rules()
    {
        var plan = Compile("bg-linear-to-r from-blue-500 to-pink-500");
        Assert.Empty(plan.Diagnostics);

        Assert.Equal((byte)TwGradientDirection.Right, (byte)ValueOf(plan.Light, TwPropertyId.GradientDirection).X);
        Assert.Equal(0xFF2B7FFFu, ValueOf(plan.Light, TwPropertyId.GradientFrom).Rgba);
        Assert.False(Has(plan.Light, TwPropertyId.GradientVia));
        Assert.NotEqual(0u, ValueOf(plan.Light, TwPropertyId.GradientTo).Rgba);
    }

    [Fact]
    public void A_Via_Stop_Adds_The_Middle_Colour()
    {
        var plan = Compile("bg-linear-to-br from-blue-500 via-purple-500 to-pink-500");
        Assert.Empty(plan.Diagnostics);

        Assert.Equal((byte)TwGradientDirection.BottomRight, (byte)ValueOf(plan.Light, TwPropertyId.GradientDirection).X);
        Assert.True(Has(plan.Light, TwPropertyId.GradientVia));

        var via = ValueOf(plan.Light, TwPropertyId.GradientVia).Rgba;
        Assert.NotEqual(ValueOf(plan.Light, TwPropertyId.GradientFrom).Rgba, via);
        Assert.NotEqual(ValueOf(plan.Light, TwPropertyId.GradientTo).Rgba, via);
    }

    [Fact]
    public void A_Gradient_Stop_Honours_An_Opacity_Modifier()
    {
        var plan = Compile("bg-linear-to-r from-blue-500 to-black/50");
        Assert.Equal(0x80u, ValueOf(plan.Light, TwPropertyId.GradientTo).Rgba >> 24);
    }

    /// <summary>`--tw-gradient-stops` has no registered default, so a gradient with no stops fails loudly.</summary>
    [Fact]
    public void A_Gradient_Without_Stops_Reports_Rather_Than_Rendering_Blank()
    {
        var plan = Compile("bg-linear-to-r");
        Assert.Contains(plan.Diagnostics, d => d.Message.Contains("--tw-gradient-stops"));
        Assert.False(Has(plan.Light, TwPropertyId.GradientDirection));
    }

    // ------------------------------------------------------------ variants

    [Fact]
    public void A_Dark_Variant_Only_Affects_The_Dark_Bucket()
    {
        var plan = Compile("bg-red-500 dark:bg-slate-900");

        Assert.True(plan.DiffersByTheme);
        Assert.Equal(0xFFFB2C36u, ValueOf(plan.Light, TwPropertyId.Background).Rgba);
        Assert.NotEqual(0xFFFB2C36u, ValueOf(plan.Dark, TwPropertyId.Background).Rgba);
    }

    [Fact]
    public void A_Hover_Variant_Becomes_A_State_Plan()
    {
        var plan = Compile("hover:bg-blue-600");
        var state = Assert.Single(plan.States);
        Assert.Equal(TwInteractiveState.Hover, state.State);
        Assert.True(Has(state.Light, TwPropertyId.Background));
    }

    [Fact]
    public void A_Custom_Pressed_Variant_Becomes_The_Pressed_State()
    {
        var plan = Compile("pressed:scale-95");
        var state = Assert.Single(plan.States);
        Assert.Equal(TwInteractiveState.Pressed, state.State);
        Assert.Equal(0.95f, ValueOf(state.Light, TwPropertyId.Scale).X, 3);
    }

    [Fact]
    public void A_Disabled_Variant_Becomes_The_Disabled_State()
    {
        var state = Assert.Single(Compile("disabled:opacity-50").States);
        Assert.Equal(TwInteractiveState.Disabled, state.State);
        Assert.Equal(0.5f, ValueOf(state.Light, TwPropertyId.Opacity).X, 3);
    }

    [Fact]
    public void A_Breakpoint_Variant_Becomes_A_Responsive_Overlay()
    {
        var plan = Compile("p-4 sm:p-2");
        var overlay = Assert.Single(plan.Breakpoints);
        Assert.Equal(640f, overlay.MinWidth);
        Assert.Equal(8f, ValueOf(overlay.Light, TwPropertyId.Padding).X); // 0.25rem * 2
    }

    /// <summary>
    /// The whole point of routing platform variants through `@media windows`: they are resolved at
    /// build time and cost nothing at runtime. On a non-Windows head the utility must vanish.
    /// </summary>
    [Fact]
    public void Platform_Variants_Are_Filtered_By_The_Build_Environment()
    {
        var onWindows = Compile("windows:p-5", Windows);
        Assert.Equal(20f, ValueOf(onWindows.Light, TwPropertyId.Padding).X);

        var onAndroid = Compile("windows:p-5", new TwEnvironment(TwPlatforms.Android, TwIdioms.Phone));
        Assert.False(Has(onAndroid.Light, TwPropertyId.Padding));
    }

    [Fact]
    public void Idiom_Variants_Are_Filtered_Too()
    {
        var onPhone = Compile("phone:p-1", new TwEnvironment(TwPlatforms.Android, TwIdioms.Phone));
        Assert.Equal(4f, ValueOf(onPhone.Light, TwPropertyId.Padding).X);

        var onDesktop = Compile("phone:p-1", Windows);
        Assert.False(Has(onDesktop.Light, TwPropertyId.Padding));
    }

    // --------------------------------------------------------- diagnostics

    [Fact]
    public void An_Unknown_Utility_Is_Reported_Not_Silently_Dropped()
    {
        var plan = Compile("definitely-not-a-utility");
        var diagnostic = Assert.Single(plan.Diagnostics);
        Assert.Contains("unknown utility", diagnostic.Message);
    }

    [Fact]
    public void A_Web_Only_Property_Reports_A_Helpful_Diagnostic()
    {
        var plan = Compile("float-left");
        Assert.Contains(plan.Diagnostics, d => d.Message.Contains("no native analog"));
        Assert.False(Has(plan.Light, TwPropertyId.Background));
    }

    [Fact]
    public void Backdrop_Blur_Reports_Rather_Than_Rendering_Wrong()
    {
        Assert.Contains(Compile("backdrop-blur-sm").Diagnostics, d => d.Message.Contains("backdrop"));
    }

    // ------------------------------------------------------ one vocabulary

    /// <summary>
    /// The two-palette bug: an interpolated class string cannot be precompiled, so it reaches the
    /// engine at runtime. With a stylesheet attached it must resolve through Tailwind — the same
    /// vocabulary the literals used — instead of through the built-in parser's own tables.
    /// </summary>
    [Fact]
    public void A_Runtime_Class_String_Resolves_Through_The_Stylesheet_When_One_Is_Attached()
    {
        var engine = new TwEngine(Windows) { Stylesheet = new TwCssPlanCompiler(Sheet.Value) };

        // Exactly what `$"bg-{family}-{shade}"` produces at runtime.
        var background = ValueOf(engine.GetPlan("bg-red-500").Light, TwPropertyId.Background);

        Assert.Equal(0xFFFB2C36u, background.Rgba); // v4
    }

    /// <summary>
    /// A project with no tw.css falls back to the built-in class-name parser — and that parser must
    /// still speak the same Tailwind. Its palette tables are generated from Tailwind's own oklch
    /// theme (tools/gen-palette.mjs), so both front ends agree on every colour.
    /// </summary>
    [Fact]
    public void The_Fallback_Parser_Carries_The_Same_Palette_As_The_Stylesheet()
    {
        var withStylesheet = new TwEngine(Windows) { Stylesheet = new TwCssPlanCompiler(Sheet.Value) };
        var withoutStylesheet = new TwEngine(Windows);

        // The fixture stylesheet only carries the colours its candidate list used, so compare on one
        // the CSS definitely knows, then spot-check the table against Tailwind's published hexes.
        uint viaCss = ValueOf(withStylesheet.GetPlan("bg-red-500").Light, TwPropertyId.Background).Rgba;
        uint viaParser = ValueOf(withoutStylesheet.GetPlan("bg-red-500").Light, TwPropertyId.Background).Rgba;
        Assert.Equal(viaCss, viaParser);

        Assert.Equal(0xFFFB2C36u, viaParser);                                                          // red-500
        Assert.Equal(0xFF2B7FFFu, ValueOf(withoutStylesheet.GetPlan("bg-blue-500").Light, TwPropertyId.Background).Rgba);
        Assert.Equal(0xFF00BC7Du, ValueOf(withoutStylesheet.GetPlan("bg-emerald-500").Light, TwPropertyId.Background).Rgba);
    }

    /// <summary>
    /// The whole point: precompiled and runtime-resolved class strings must agree. Before the
    /// stylesheet was attached to the engine, these two differed — one app, two palettes.
    /// </summary>
    [Fact]
    public void Precompiled_And_Runtime_Paths_Agree_On_The_Same_Class_String()
    {
        var engine = new TwEngine(Windows) { Stylesheet = new TwCssPlanCompiler(Sheet.Value) };

        foreach (var utility in new[] { "bg-red-500", "text-brand-600", "bg-red-500/50", "p-4", "rounded-lg" })
        {
            var precompiled = Compile(utility);          // what the generator bakes in
            var atRuntime = engine.GetPlan(utility);     // what a dynamic string gets

            Assert.Equal(precompiled.Light.Length, atRuntime.Light.Length);
            for (int i = 0; i < precompiled.Light.Length; i++)
            {
                Assert.Equal(precompiled.Light[i].Property, atRuntime.Light[i].Property);
                Assert.Equal(precompiled.Light[i].Value.Rgba, atRuntime.Light[i].Value.Rgba);
                Assert.Equal(precompiled.Light[i].Value.X, atRuntime.Light[i].Value.X, 3);
            }
        }
    }

    // --------------------------------------------- differential vs TwParser

    /// <summary>Every utility must lower identically through both front ends — colours included.</summary>
    [Theory]
    [InlineData("bg-red-500", TwPropertyId.Background)]
    [InlineData("p-4", TwPropertyId.Padding)]
    [InlineData("m-4", TwPropertyId.Margin)]
    [InlineData("rounded-lg", TwPropertyId.CornerRadius)]
    [InlineData("text-xl", TwPropertyId.FontSize)]
    [InlineData("font-bold", TwPropertyId.FontWeight)]
    [InlineData("opacity-50", TwPropertyId.Opacity)]
    [InlineData("z-10", TwPropertyId.ZIndex)]
    [InlineData("gap-4", TwPropertyId.Gap)]
    [InlineData("order-2", TwPropertyId.Order)]
    [InlineData("grid-cols-3", TwPropertyId.GridColumns)]
    [InlineData("shrink-0", TwPropertyId.FlexShrink)]
    public void Both_Front_Ends_Agree_On_Non_Color_Utilities(string utility, TwPropertyId id)
    {
        var viaCss = ValueOf(Compile(utility).Light, id);

        var engine = new TwEngine(Windows);
        var viaParser = ValueOf(engine.GetPlan(utility).Light, id);

        Assert.Equal(viaParser.Kind, viaCss.Kind);
        Assert.Equal(viaParser.Rgba, viaCss.Rgba);
        AssertFloat(viaParser.X, viaCss.X);
        AssertFloat(viaParser.Y, viaCss.Y);
        AssertFloat(viaParser.Z, viaCss.Z);
        AssertFloat(viaParser.W, viaCss.W);

        static void AssertFloat(float expected, float actual)
        {
            if (float.IsNaN(expected)) Assert.True(float.IsNaN(actual), $"expected NaN, got {actual}");
            else Assert.Equal(expected, actual, 3);
        }
    }

    [Fact]
    public void A_Composed_Class_String_Produces_Every_Bucket()
    {
        var plan = Compile("p-4 bg-red-500 dark:bg-slate-900 hover:bg-blue-600 sm:p-2");

        Assert.True(Has(plan.Light, TwPropertyId.Padding));
        Assert.True(Has(plan.Light, TwPropertyId.Background));
        Assert.True(plan.DiffersByTheme);
        Assert.Single(plan.States);
        Assert.Single(plan.Breakpoints);
        Assert.Empty(plan.Diagnostics);
    }
}
