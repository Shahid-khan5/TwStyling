using Tw.Core;

namespace Tw.Core.Tests;

/// <summary>Coverage for the utilities added in the compatibility push:
/// object-fit, scale-x/y, transform-origin, pointer-events, align-content,
/// order, whitespace/break, and numeric leading.</summary>
public class NewUtilityTests
{
    private static readonly TwEngine Engine = new(new TwEnvironment(TwPlatforms.Windows, TwIdioms.Desktop));

    private static TwDeclaration Single(string classes, TwPropertyId property)
    {
        var matches = Engine.GetPlan(classes).Light.Where(d => d.Property == property).ToArray();
        Assert.Single(matches);
        return matches[0];
    }

    // ---------------------------------------------------------------- object-fit

    [Theory]
    [InlineData("object-contain", TwObjectFit.Contain)]
    [InlineData("object-cover", TwObjectFit.Cover)]
    [InlineData("object-fill", TwObjectFit.Fill)]
    [InlineData("object-none", TwObjectFit.None)]
    [InlineData("object-scale-down", TwObjectFit.ScaleDown)]
    public void Object_fit(string cls, TwObjectFit expected) =>
        Assert.Equal((byte)expected, (byte)Single(cls, TwPropertyId.ObjectFit).Value.X);

    [Fact]
    public void Object_position_gets_helpful_message()
    {
        var diags = TwEngine.Validate("object-top");
        Assert.Single(diags);
        Assert.Contains("object-position", diags[0].Message);
    }

    // ---------------------------------------------------------------- scale x/y

    [Theory]
    [InlineData("scale-x-50", TwPropertyId.ScaleX, 0.5f)]
    [InlineData("scale-y-110", TwPropertyId.ScaleY, 1.10f)]
    [InlineData("-scale-x-100", TwPropertyId.ScaleX, -1.0f)]
    public void Scale_axis(string cls, TwPropertyId property, float expected) =>
        Assert.Equal(expected, Single(cls, property).Value.X, 3);

    [Fact]
    public void Bare_scale_still_works() =>
        Assert.Equal(0.95f, Single("scale-95", TwPropertyId.Scale).Value.X, 3);

    // ---------------------------------------------------------------- transform-origin

    [Theory]
    [InlineData("origin-center", 0.5f, 0.5f)]
    [InlineData("origin-top-left", 0f, 0f)]
    [InlineData("origin-bottom-right", 1f, 1f)]
    [InlineData("origin-right", 1f, 0.5f)]
    public void Transform_origin(string cls, float x, float y)
    {
        Assert.Equal(x, Single(cls, TwPropertyId.TransformOriginX).Value.X);
        Assert.Equal(y, Single(cls, TwPropertyId.TransformOriginY).Value.X);
    }

    // ---------------------------------------------------------------- pointer-events

    [Theory]
    [InlineData("pointer-events-none", 1f)]
    [InlineData("pointer-events-auto", 0f)]
    public void Pointer_events(string cls, float expected) =>
        Assert.Equal(expected, Single(cls, TwPropertyId.PointerEventsNone).Value.X);

    // ---------------------------------------------------------------- align-content

    [Theory]
    [InlineData("content-start", TwAlignContent.Start)]
    [InlineData("content-center", TwAlignContent.Center)]
    [InlineData("content-between", TwAlignContent.Between)]
    [InlineData("content-stretch", TwAlignContent.Stretch)]
    public void Align_content(string cls, TwAlignContent expected) =>
        Assert.Equal((byte)expected, (byte)Single(cls, TwPropertyId.AlignContent).Value.X);

    // ---------------------------------------------------------------- order

    [Theory]
    [InlineData("order-3", 3f)]
    [InlineData("order-first", -9999f)]
    [InlineData("order-last", 9999f)]
    [InlineData("order-none", 0f)]
    [InlineData("-order-2", -2f)]
    public void Order(string cls, float expected) =>
        Assert.Equal(expected, Single(cls, TwPropertyId.Order).Value.X);

    // ---------------------------------------------------------------- line break

    [Theory]
    [InlineData("whitespace-nowrap", TwLineBreak.NoWrap)]
    [InlineData("whitespace-normal", TwLineBreak.WordWrap)]
    [InlineData("break-words", TwLineBreak.WordWrap)]
    [InlineData("break-all", TwLineBreak.CharacterWrap)]
    [InlineData("break-normal", TwLineBreak.WordWrap)]
    public void Line_break(string cls, TwLineBreak expected) =>
        Assert.Equal((byte)expected, (byte)Single(cls, TwPropertyId.LineBreak).Value.X);

    // ---------------------------------------------------------------- numeric leading

    [Fact]
    public void Named_leading_is_a_multiplier() =>
        Assert.Equal(1.5f, Single("leading-normal", TwPropertyId.LineHeight).Value.X);

    [Theory]
    [InlineData("leading-6", 24f)]  // 6 * 4px
    [InlineData("leading-8", 32f)]
    public void Numeric_leading_is_absolute_length(string cls, float expectedPx)
    {
        var v = Single(cls, TwPropertyId.LineHeight).Value;
        Assert.True(v.IsAbsoluteLength);
        Assert.Equal(expectedPx, v.X);
    }

    // ---------------------------------------------------------------- diagnostics

    [Fact]
    public void Fractional_width_gets_helpful_message()
    {
        var diags = TwEngine.Validate("w-1/2");
        Assert.Single(diags);
        Assert.Contains("basis-1/2", diags[0].Message);
    }

    [Theory]
    [InlineData("aspect-square", "aspect")]
    [InlineData("cursor-pointer", "cursor")]
    [InlineData("select-none", "user-select")]
    [InlineData("skew-x-6", "skew")]
    [InlineData("blur-sm", "filter")]
    public void Web_only_utilities_get_helpful_messages(string cls, string expectedFragment)
    {
        var diags = TwEngine.Validate(cls);
        Assert.Single(diags);
        Assert.Contains(expectedFragment, diags[0].Message);
    }

    [Fact]
    public void W_auto_is_a_noop_not_an_error()
    {
        var diags = TwEngine.Validate("w-auto");
        Assert.Empty(diags);
    }

    // ---------------------------------------------------------------- per-side border width

    // Edges encoding: X=left, Y=top, Z=right, W=bottom; NaN = side not specified.
    [Fact]
    public void Uniform_border_sets_all_sides()
    {
        var v = Single("border", TwPropertyId.BorderWidth).Value;
        Assert.Equal(TwValueKind.Edges, v.Kind);
        Assert.Equal(1f, v.X); Assert.Equal(1f, v.Y); Assert.Equal(1f, v.Z); Assert.Equal(1f, v.W);
    }

    [Theory]
    [InlineData("border-2", 2f)]
    [InlineData("border-4", 4f)]
    [InlineData("border-[3px]", 3f)]
    public void Uniform_border_width(string cls, float w)
    {
        var v = Single(cls, TwPropertyId.BorderWidth).Value;
        Assert.Equal(w, v.X); Assert.Equal(w, v.Y); Assert.Equal(w, v.Z); Assert.Equal(w, v.W);
    }

    [Fact]
    public void Border_top_sets_only_top()
    {
        var v = Single("border-t-2", TwPropertyId.BorderWidth).Value;
        Assert.True(float.IsNaN(v.X)); Assert.Equal(2f, v.Y); Assert.True(float.IsNaN(v.Z)); Assert.True(float.IsNaN(v.W));
    }

    [Fact]
    public void Border_bottom_bare_is_one()
    {
        var v = Single("border-b", TwPropertyId.BorderWidth).Value;
        Assert.True(float.IsNaN(v.X)); Assert.True(float.IsNaN(v.Y)); Assert.True(float.IsNaN(v.Z)); Assert.Equal(1f, v.W);
    }

    [Fact]
    public void Border_x_sets_left_and_right()
    {
        var v = Single("border-x-4", TwPropertyId.BorderWidth).Value;
        Assert.Equal(4f, v.X); Assert.True(float.IsNaN(v.Y)); Assert.Equal(4f, v.Z); Assert.True(float.IsNaN(v.W));
    }

    [Fact]
    public void Partial_border_widths_side_merge()
    {
        // border → all 1; border-t-0 overlays top = 0. Mirrors padding side-merge.
        var v = Single("border border-t-0", TwPropertyId.BorderWidth).Value;
        Assert.Equal(1f, v.X); Assert.Equal(0f, v.Y); Assert.Equal(1f, v.Z); Assert.Equal(1f, v.W);
    }

    [Fact]
    public void UniformEdge_reduces_per_side_to_max_for_uniform_renderers()
    {
        Assert.Equal(4f, Single("border-t-4", TwPropertyId.BorderWidth).Value.UniformEdge());
        Assert.Equal(1f, Single("border", TwPropertyId.BorderWidth).Value.UniformEdge());
    }

    [Fact]
    public void Border_color_still_parses_as_color()
    {
        // 'blue-500' must not be mistaken for a side.
        var v = Single("border-blue-500", TwPropertyId.BorderColor).Value;
        Assert.Equal(TwValueKind.Color, v.Kind);
    }

    // ---------------------------------------------------------------- colored shadows

    [Fact]
    public void Shadow_color_resolves_to_palette()
    {
        var v = Single("shadow-blue-500", TwPropertyId.ShadowColor).Value;
        Assert.Equal(TwValueKind.Color, v.Kind);
        Assert.Equal(TwPalette.Colors["blue-500"], v.Rgba);
    }

    [Fact]
    public void Shadow_color_carries_opacity_modifier()
    {
        var v = Single("shadow-black/25", TwPropertyId.ShadowColor).Value;
        Assert.Equal(0x40u, (v.Rgba >> 24) & 0xFF); // 25% of 255 ≈ 64 = 0x40
    }

    [Fact]
    public void Sized_colored_shadow_has_both_declarations()
    {
        var plan = Engine.GetPlan("shadow-lg shadow-blue-500");
        Assert.Contains(plan.Light, d => d.Property == TwPropertyId.Shadow);
        Assert.Contains(plan.Light, d => d.Property == TwPropertyId.ShadowColor);
    }

    [Fact]
    public void Named_shadow_sizes_still_work() =>
        Assert.Single(Engine.GetPlan("shadow-xl").Light, d => d.Property == TwPropertyId.Shadow);

    [Fact]
    public void Shadow_inner_gets_helpful_message()
    {
        var diags = TwEngine.Validate("shadow-inner");
        Assert.Single(diags);
        Assert.Contains("inset", diags[0].Message);
    }

    // ---------------------------------------------------------------- font-family

    [Theory]
    [InlineData("font-sans", TwFontFamily.Sans)]
    [InlineData("font-serif", TwFontFamily.Serif)]
    [InlineData("font-mono", TwFontFamily.Mono)]
    public void Font_family(string cls, TwFontFamily expected) =>
        Assert.Equal((byte)expected, (byte)Single(cls, TwPropertyId.FontFamily).Value.X);

    [Fact]
    public void Font_weight_still_works_alongside_family() =>
        Assert.Equal(700f, Single("font-bold", TwPropertyId.FontWeight).Value.X);

    // ---------------------------------------------------------------- text-shadow

    [Theory]
    [InlineData("text-shadow")]
    [InlineData("text-shadow-sm")]
    [InlineData("text-shadow-lg")]
    [InlineData("text-shadow-none")]
    public void Text_shadow_maps_to_shadow(string cls) =>
        Assert.Equal(TwValueKind.Shadow, Single(cls, TwPropertyId.Shadow).Value.Kind);

    [Fact]
    public void Text_align_still_works() => // text-shadow must not shadow text-center
        Assert.Equal((byte)TwTextAlign.Center, (byte)Single("text-center", TwPropertyId.TextAlign).Value.X);

    // ---------------------------------------------------------------- grid alignment

    [Theory]
    [InlineData("justify-self-start", TwAlign.Start)]
    [InlineData("justify-self-center", TwAlign.Center)]
    [InlineData("justify-self-end", TwAlign.End)]
    [InlineData("justify-self-stretch", TwAlign.Stretch)]
    public void Justify_self(string cls, TwAlign expected) =>
        Assert.Equal((byte)expected, (byte)Single(cls, TwPropertyId.AlignSelfX).Value.X);

    [Fact]
    public void Justify_self_auto_is_a_noop() => Assert.Empty(TwEngine.Validate("justify-self-auto"));

    [Fact]
    public void Place_self_sets_both_axes()
    {
        Assert.Equal((byte)TwAlign.Center, (byte)Single("place-self-center", TwPropertyId.AlignSelfX).Value.X);
        Assert.Equal((byte)TwAlign.Center, (byte)Single("place-self-center", TwPropertyId.AlignSelfY).Value.X);
    }

    [Fact]
    public void Place_content_sets_align_and_justify()
    {
        Assert.Equal((byte)TwAlignContent.Center, (byte)Single("place-content-center", TwPropertyId.AlignContent).Value.X);
        Assert.Equal((byte)TwJustify.Center, (byte)Single("place-content-center", TwPropertyId.JustifyContent).Value.X);
    }

    [Fact]
    public void Place_content_stretch_is_align_content_only()
    {
        var plan = Engine.GetPlan("place-content-stretch");
        Assert.Equal((byte)TwAlignContent.Stretch, (byte)Single("place-content-stretch", TwPropertyId.AlignContent).Value.X);
        Assert.DoesNotContain(plan.Light, d => d.Property == TwPropertyId.JustifyContent);
    }

    // ---------------------------------------------------------------- reclassified as N/A

    [Theory]
    [InlineData("justify-items-center", "child-alignment")]
    [InlineData("place-items-center", "child-alignment")]
    [InlineData("indent-4", "text-indent")]
    public void Non_mappable_alignment_gets_helpful_message(string cls, string fragment)
    {
        var diags = TwEngine.Validate(cls);
        Assert.Single(diags);
        Assert.Contains(fragment, diags[0].Message);
    }
}
