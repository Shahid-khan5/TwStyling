using Tw.Core;

namespace Tw.Core.Tests;

public class ParserTests
{
    private static readonly TwEnvironment Windows = new(TwPlatforms.Windows, TwIdioms.Desktop);
    private static readonly TwEnvironment AndroidPhone = new(TwPlatforms.Android, TwIdioms.Phone);

    private static TwEngine NewEngine(TwEnvironment? env = null) => new(env ?? Windows);

    private static TwDeclaration Single(StylePlan plan, TwPropertyId property)
    {
        var matches = plan.Light.Where(d => d.Property == property).ToArray();
        Assert.Single(matches);
        return matches[0];
    }

    // ---------------------------------------------------------------- colors

    [Fact]
    public void Background_palette_color_resolves_to_exact_hex()
    {
        var plan = NewEngine().GetPlan("bg-blue-500");
        var decl = Single(plan, TwPropertyId.Background);
        Assert.Equal(0xFF3B82F6u, decl.Value.Rgba);
    }

    [Fact]
    public void Color_opacity_modifier_scales_alpha()
    {
        var plan = NewEngine().GetPlan("bg-black/50");
        var decl = Single(plan, TwPropertyId.Background);
        Assert.Equal(0x80000000u, decl.Value.Rgba);
    }

    [Theory]
    [InlineData("bg-[#ff0000]", 0xFFFF0000u)]
    [InlineData("bg-[#f00]", 0xFFFF0000u)]
    [InlineData("bg-[#ff000080]", 0x80FF0000u)] // CSS alpha-last → stored alpha-first
    public void Arbitrary_hex_colors(string cls, uint expected)
    {
        var plan = NewEngine().GetPlan(cls);
        Assert.Equal(expected, Single(plan, TwPropertyId.Background).Value.Rgba);
    }

    [Fact]
    public void Text_color_and_text_size_disambiguate()
    {
        var plan = NewEngine().GetPlan("text-xl text-slate-900");
        Assert.Equal(20f, Single(plan, TwPropertyId.FontSize).Value.X);
        Assert.Equal(0xFF0F172Au, Single(plan, TwPropertyId.TextColor).Value.Rgba);
        Assert.Equal(28f / 20, Single(plan, TwPropertyId.LineHeight).Value.X, 3);
    }

    // ---------------------------------------------------------------- spacing

    [Fact]
    public void Padding_shorthand_axes_and_sides_merge()
    {
        var plan = NewEngine().GetPlan("px-4 pt-2");
        var edges = Single(plan, TwPropertyId.Padding).Value;
        Assert.Equal(16f, edges.X); // left
        Assert.Equal(8f, edges.Y);  // top
        Assert.Equal(16f, edges.Z); // right
        Assert.True(float.IsNaN(edges.W)); // bottom untouched
    }

    [Fact]
    public void Negative_margin()
    {
        var plan = NewEngine().GetPlan("-mt-2");
        var edges = Single(plan, TwPropertyId.Margin).Value;
        Assert.Equal(-8f, edges.Y);
    }

    [Fact]
    public void Negative_padding_is_rejected()
    {
        var diags = TwEngine.Validate("-p-2");
        Assert.Single(diags);
        Assert.Contains("negative padding", diags[0].Message);
    }

    [Theory]
    [InlineData("p-px", 1f)]
    [InlineData("p-2.5", 10f)]
    [InlineData("p-[13]", 13f)]
    [InlineData("p-[13px]", 13f)]
    public void Spacing_value_forms(string cls, float expected)
    {
        var plan = NewEngine().GetPlan(cls);
        Assert.Equal(expected, Single(plan, TwPropertyId.Padding).Value.X);
    }

    [Fact]
    public void Gap_axes()
    {
        var plan = NewEngine().GetPlan("gap-x-2 gap-y-4");
        var gap = Single(plan, TwPropertyId.Gap).Value;
        Assert.Equal(8f, gap.X);
        Assert.Equal(16f, gap.Y);
    }

    // ---------------------------------------------------------------- shape

    [Fact]
    public void Rounded_side_overrides_merge()
    {
        var plan = NewEngine().GetPlan("rounded-lg rounded-t-none");
        var corners = Single(plan, TwPropertyId.CornerRadius).Value;
        Assert.Equal(0f, corners.X);  // TL
        Assert.Equal(0f, corners.Y);  // TR
        Assert.Equal(8f, corners.Z);  // BL
        Assert.Equal(8f, corners.W);  // BR
    }

    [Fact]
    public void Bare_border_injects_default_border_color()
    {
        var plan = NewEngine().GetPlan("border");
        Assert.Equal(1f, Single(plan, TwPropertyId.BorderWidth).Value.X);
        Assert.Equal(TwTables.DefaultBorderColor, Single(plan, TwPropertyId.BorderColor).Value.Rgba);
    }

    [Fact]
    public void Explicit_border_color_suppresses_default()
    {
        var plan = NewEngine().GetPlan("border border-red-500");
        Assert.Equal(0xFFEF4444u, Single(plan, TwPropertyId.BorderColor).Value.Rgba);
    }

    // ---------------------------------------------------------------- variants

    [Fact]
    public void Platform_variants_filter_at_compile_time()
    {
        const string cls = "ios:pt-12 android:pt-6 pt-2";
        var androidPlan = NewEngine(AndroidPhone).GetPlan(cls);
        var windowsPlan = NewEngine(Windows).GetPlan(cls);

        Assert.Equal(24f, Single(androidPlan, TwPropertyId.Padding).Value.Y);
        Assert.Equal(8f, Single(windowsPlan, TwPropertyId.Padding).Value.Y);
    }

    [Fact]
    public void Dark_variant_produces_theme_split()
    {
        var plan = NewEngine().GetPlan("bg-white dark:bg-slate-900");
        Assert.True(plan.DiffersByTheme);
        Assert.Equal(0xFFFFFFFFu, plan.Light.Single(d => d.Property == TwPropertyId.Background).Value.Rgba);
        Assert.Equal(0xFF0F172Au, plan.Dark.Single(d => d.Property == TwPropertyId.Background).Value.Rgba);
    }

    [Fact]
    public void Dark_variant_wins_regardless_of_token_order()
    {
        var plan = NewEngine().GetPlan("dark:bg-slate-900 bg-white");
        Assert.Equal(0xFF0F172Au, plan.Dark.Single(d => d.Property == TwPropertyId.Background).Value.Rgba);
    }

    [Fact]
    public void Static_plan_reports_no_theme_difference()
    {
        var plan = NewEngine().GetPlan("bg-white p-4 rounded-lg");
        Assert.False(plan.DiffersByTheme);
        Assert.False(plan.HasStates);
    }

    [Fact]
    public void Pressed_state_produces_delta_plan()
    {
        var plan = NewEngine().GetPlan("bg-indigo-600 pressed:bg-indigo-700");
        Assert.True(plan.HasStates);
        var pressed = plan.States.Single(s => s.State == TwInteractiveState.Pressed);
        Assert.Equal(0xFF4338CAu, pressed.Light.Single().Value.Rgba);
    }

    [Fact]
    public void Stacked_dark_pressed_variant()
    {
        var plan = NewEngine().GetPlan("dark:pressed:bg-indigo-400");
        var pressed = plan.States.Single(s => s.State == TwInteractiveState.Pressed);
        Assert.Empty(pressed.Light);
        Assert.Single(pressed.Dark);
    }

    [Fact]
    public void Hover_and_active_aliases_map_to_states()
    {
        var plan = NewEngine().GetPlan("hover:bg-blue-100 active:bg-blue-200");
        Assert.Contains(plan.States, s => s.State == TwInteractiveState.Hover);
        Assert.Contains(plan.States, s => s.State == TwInteractiveState.Pressed);
    }

    // ---------------------------------------------------------------- diagnostics

    [Fact]
    public void Unknown_utility_is_a_diagnostic_never_silent()
    {
        var diags = TwEngine.Validate("bg-blue-500 not-a-thing");
        Assert.Single(diags);
        Assert.Equal("not-a-thing", diags[0].Token);
    }

    [Fact]
    public void Web_only_utilities_get_helpful_messages()
    {
        var diags = TwEngine.Validate("sr-only space-x-2 ring-2 animate-wiggle");
        Assert.Equal(4, diags.Length);
        Assert.Contains("SemanticProperties", diags[0].Message);
        Assert.Contains("gap-", diags[1].Message);
        Assert.All(diags, d => Assert.NotEqual("unknown utility", d.Message));
    }

    [Fact]
    public void Unknown_variant_is_reported()
    {
        var diags = TwEngine.Validate("hocus:bg-blue-500");
        Assert.Single(diags);
        Assert.Contains("unknown variant", diags[0].Message);
    }

    [Fact]
    public void Diagnostics_flow_to_engine_sink()
    {
        var seen = new List<TwDiagnostic>();
        var engine = new TwEngine(Windows, seen.Add);
        engine.GetPlan("bg-nope-500");
        Assert.Single(seen);
    }

    // ---------------------------------------------------------------- caching

    [Fact]
    public void Same_string_returns_identical_plan_instance()
    {
        var engine = NewEngine();
        var a = engine.GetPlan("bg-white p-4");
        var b = engine.GetPlan("bg-white p-4");
        Assert.Same(a, b);
        Assert.Equal(1, engine.CachedPlanCount);
    }

    [Fact]
    public void Empty_and_null_return_empty_plan()
    {
        var engine = NewEngine();
        Assert.Same(StylePlan.Empty, engine.GetPlan(null));
        Assert.Same(StylePlan.Empty, engine.GetPlan("   "));
    }

    // ---------------------------------------------------------------- misc utilities

    [Fact]
    public void Typography_bundle()
    {
        var plan = NewEngine().GetPlan("font-semibold italic tracking-wide line-clamp-2 text-center");
        Assert.Equal(600f, Single(plan, TwPropertyId.FontWeight).Value.X);
        Assert.Equal(1f, Single(plan, TwPropertyId.FontItalic).Value.X);
        Assert.Equal(0.025f, Single(plan, TwPropertyId.CharacterSpacingEm).Value.X, 4);
        Assert.Equal(2f, Single(plan, TwPropertyId.LineClamp).Value.X);
        Assert.Equal((byte)TwTextAlign.Center, (byte)Single(plan, TwPropertyId.TextAlign).Value.X);
    }

    [Fact]
    public void Sizing_full_and_named_max_width()
    {
        var plan = NewEngine().GetPlan("w-full max-w-md h-10");
        Assert.True(Single(plan, TwPropertyId.Width).Value.IsFull);
        Assert.Equal(448f, Single(plan, TwPropertyId.MaxWidth).Value.X);
        Assert.Equal(40f, Single(plan, TwPropertyId.Height).Value.X);
    }

    [Fact]
    public void Shadow_and_opacity()
    {
        var plan = NewEngine().GetPlan("shadow-md opacity-75");
        var shadow = Single(plan, TwPropertyId.Shadow).Value;
        Assert.Equal(0x1A000000u, shadow.Rgba);
        Assert.Equal(4f, shadow.Y);
        Assert.Equal(0.75f, Single(plan, TwPropertyId.Opacity).Value.X);
    }

    [Fact]
    public void Last_wins_for_conflicting_scalar_utilities()
    {
        var plan = NewEngine().GetPlan("bg-red-500 bg-blue-500");
        Assert.Equal(0xFF3B82F6u, Single(plan, TwPropertyId.Background).Value.Rgba);
    }
}
