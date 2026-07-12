using TwStyling;

namespace TwStyling.Tests;

public class LayoutUtilityTests
{
    private static readonly TwEngine Engine = new(new TwEnvironment(TwPlatforms.Windows, TwIdioms.Desktop));

    private static TwDeclaration Single(string classes, TwPropertyId property)
    {
        var matches = Engine.GetPlan(classes).Light.Where(d => d.Property == property).ToArray();
        Assert.Single(matches);
        return matches[0];
    }

    // ---------------------------------------------------------------- flexbox

    [Theory]
    [InlineData("flex", TwFlexDirection.Row)]
    [InlineData("flex-row", TwFlexDirection.Row)]
    [InlineData("flex-col", TwFlexDirection.Column)]
    [InlineData("flex-row-reverse", TwFlexDirection.RowReverse)]
    public void Flex_direction(string cls, TwFlexDirection expected) =>
        Assert.Equal((byte)expected, (byte)Single(cls, TwPropertyId.FlexDirection).Value.X);

    [Theory]
    [InlineData("justify-start", TwJustify.Start)]
    [InlineData("justify-center", TwJustify.Center)]
    [InlineData("justify-between", TwJustify.Between)]
    [InlineData("justify-evenly", TwJustify.Evenly)]
    public void Justify_content(string cls, TwJustify expected) =>
        Assert.Equal((byte)expected, (byte)Single(cls, TwPropertyId.JustifyContent).Value.X);

    [Theory]
    [InlineData("items-start", TwAlignItems.Start)]
    [InlineData("items-center", TwAlignItems.Center)]
    [InlineData("items-stretch", TwAlignItems.Stretch)]
    public void Align_items(string cls, TwAlignItems expected) =>
        Assert.Equal((byte)expected, (byte)Single(cls, TwPropertyId.AlignItems).Value.X);

    [Fact]
    public void Flex_1_expands_to_grow_shrink_basis()
    {
        var plan = Engine.GetPlan("flex-1");
        Assert.Equal(1f, plan.Light.Single(d => d.Property == TwPropertyId.FlexGrow).Value.X);
        Assert.Equal(1f, plan.Light.Single(d => d.Property == TwPropertyId.FlexShrink).Value.X);
        Assert.Equal(0f, plan.Light.Single(d => d.Property == TwPropertyId.FlexBasis).Value.X);
    }

    [Fact]
    public void Basis_fraction_is_relative()
    {
        var basis = Single("basis-1/2", TwPropertyId.FlexBasis).Value;
        Assert.Equal(0.5f, basis.X);
        Assert.Equal(1f, basis.Y); // relative flag
    }

    [Fact]
    public void Basis_spacing_is_absolute()
    {
        var basis = Single("basis-24", TwPropertyId.FlexBasis).Value;
        Assert.Equal(96f, basis.X);
        Assert.Equal(0f, basis.Y);
    }

    [Theory]
    [InlineData("grow", 1f)]
    [InlineData("grow-0", 0f)]
    [InlineData("grow-2", 2f)]
    public void Grow(string cls, float expected) =>
        Assert.Equal(expected, Single(cls, TwPropertyId.FlexGrow).Value.X);

    [Fact]
    public void Self_alignment() =>
        Assert.Equal((byte)TwAlignSelfFlex.Center, (byte)Single("self-center", TwPropertyId.FlexAlignSelf).Value.X);

    // ---------------------------------------------------------------- grid

    [Fact]
    public void Grid_columns_and_spans()
    {
        Assert.Equal(3f, Single("grid-cols-3", TwPropertyId.GridColumns).Value.X);
        Assert.Equal(2f, Single("col-span-2", TwPropertyId.GridColumnSpan).Value.X);
        Assert.Equal(1f, Single("col-start-2", TwPropertyId.GridColumn).Value.X); // 1-based → 0-based
    }

    [Fact]
    public void Bare_grid_is_valid_and_empty()
    {
        Assert.Empty(TwEngine.Validate("grid"));
        Assert.Same(StylePlan.Empty, Engine.GetPlan("grid"));
    }

    // ---------------------------------------------------------------- breakpoints

    [Fact]
    public void Breakpoint_utilities_compile_to_overlays()
    {
        var plan = Engine.GetPlan("p-2 md:p-6 lg:p-10");
        Assert.Equal(2, plan.Breakpoints.Length);
        Assert.Equal(768f, plan.Breakpoints[0].MinWidth);
        Assert.Equal(8f, plan.Light.Single(d => d.Property == TwPropertyId.Padding).Value.X);
        Assert.Equal(24f, plan.Breakpoints[0].Light.Single().Value.X);
        Assert.Equal(40f, plan.Breakpoints[1].Light.Single().Value.X);
    }

    [Fact]
    public void Breakpoint_stacks_with_dark()
    {
        var plan = Engine.GetPlan("md:dark:bg-slate-800");
        var bp = plan.Breakpoints.Single();
        Assert.Empty(bp.Light);
        Assert.Single(bp.Dark);
        Assert.True(plan.DiffersByTheme);
    }

    [Fact]
    public void Breakpoint_with_interactive_is_rejected_loudly()
    {
        var plan = Engine.GetPlan("md:pressed:bg-red-500");
        Assert.Empty(plan.Breakpoints);
        Assert.False(plan.HasStates);
        Assert.Contains(plan.Diagnostics, d => d.Message.Contains("cannot combine"));
    }

    // ---------------------------------------------------------------- transitions

    [Fact]
    public void Transition_sets_props_and_default_duration()
    {
        var plan = Engine.GetPlan("transition-colors duration-300 ease-out delay-75");
        Assert.Equal((float)(byte)TwTransitionProps.Colors, plan.Light.Single(d => d.Property == TwPropertyId.TransitionProps).Value.X);
        Assert.Equal(300f, plan.Light.Single(d => d.Property == TwPropertyId.TransitionDuration).Value.X);
        Assert.Equal(75f, plan.Light.Single(d => d.Property == TwPropertyId.TransitionDelay).Value.X);
        Assert.Equal((byte)TwEasing.Out, (byte)plan.Light.Single(d => d.Property == TwPropertyId.TransitionEasing).Value.X);
    }

    [Theory]
    [InlineData("animate-spin", TwKeyframes.Spin)]
    [InlineData("animate-pulse", TwKeyframes.Pulse)]
    [InlineData("animate-bounce", TwKeyframes.Bounce)]
    [InlineData("animate-none", TwKeyframes.None)]
    public void Keyframe_animations(string cls, TwKeyframes expected) =>
        Assert.Equal((byte)expected, (byte)Single(cls, TwPropertyId.Keyframes).Value.X);

    [Fact]
    public void Unknown_animation_lists_supported_ones()
    {
        var diags = TwEngine.Validate("animate-wiggle");
        Assert.Single(diags);
        Assert.Contains("animate-spin", diags[0].Message);
    }

    // ---------------------------------------------------------------- visibility

    [Fact]
    public void Visibility_and_overflow()
    {
        Assert.Equal(0f, Single("hidden", TwPropertyId.Visible).Value.X);
        Assert.Equal(0f, Single("invisible", TwPropertyId.Opacity).Value.X);
        Assert.Equal(1f, Single("overflow-hidden", TwPropertyId.Clip).Value.X);
    }

    [Fact]
    public void Pressed_outranks_hover_when_states_compose()
    {
        // Desktop: pressing implies hovering — both active simultaneously. The plan's
        // state order IS the composition precedence (later wins), so Pressed must
        // come after Hover or hover styles would win while the mouse is down.
        var plan = Engine.GetPlan("hover:bg-blue-600 pressed:bg-blue-800 focus:bg-blue-500");
        int hover = Array.FindIndex(plan.States, s => s.State == TwInteractiveState.Hover);
        int pressed = Array.FindIndex(plan.States, s => s.State == TwInteractiveState.Pressed);
        int focus = Array.FindIndex(plan.States, s => s.State == TwInteractiveState.Focus);
        Assert.True(hover < pressed, "Hover must precede Pressed so Pressed overrides it");
        Assert.True(focus < pressed, "Focus must precede Pressed so Pressed overrides it");
    }

    // ---------------------------------------------------------------- state borders (review fix)

    [Fact]
    public void State_border_gets_default_color()
    {
        var plan = Engine.GetPlan("hover:border-2");
        var hover = plan.States.Single(s => s.State == TwInteractiveState.Hover);
        Assert.Contains(hover.Light, d => d.Property == TwPropertyId.BorderColor);
    }

    [Fact]
    public void State_border_respects_explicit_base_color()
    {
        var plan = Engine.GetPlan("border-red-500 hover:border-2");
        var hover = plan.States.Single(s => s.State == TwInteractiveState.Hover);
        Assert.DoesNotContain(hover.Light, d => d.Property == TwPropertyId.BorderColor);
    }
}
