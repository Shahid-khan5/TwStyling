using Tw.Core;

namespace Tw.Core.Tests;

public class ExpandedUtilityTests
{
    private static readonly TwEngine Engine = new(new TwEnvironment(TwPlatforms.Windows, TwIdioms.Desktop));

    private static TwDeclaration Single(string classes, TwPropertyId property)
    {
        var matches = Engine.GetPlan(classes).Light.Where(d => d.Property == property).ToArray();
        Assert.Single(matches);
        return matches[0];
    }

    [Theory]
    [InlineData("underline", TwTextDecoration.Underline)]
    [InlineData("line-through", TwTextDecoration.Strikethrough)]
    [InlineData("no-underline", TwTextDecoration.None)]
    public void Text_decoration(string cls, TwTextDecoration expected) =>
        Assert.Equal((byte)expected, (byte)Single(cls, TwPropertyId.TextDecoration).Value.X);

    [Theory]
    [InlineData("uppercase", TwTextTransform.Uppercase)]
    [InlineData("lowercase", TwTextTransform.Lowercase)]
    [InlineData("normal-case", TwTextTransform.None)]
    public void Text_transform(string cls, TwTextTransform expected) =>
        Assert.Equal((byte)expected, (byte)Single(cls, TwPropertyId.TextTransform).Value.X);

    [Fact]
    public void Truncate_is_single_line_clamp() =>
        Assert.Equal(1f, Single("truncate", TwPropertyId.LineClamp).Value.X);

    [Theory]
    [InlineData("rotate-45", 45f)]
    [InlineData("-rotate-12", -12f)]
    [InlineData("rotate-[17deg]", 17f)]
    public void Rotate(string cls, float expected) =>
        Assert.Equal(expected, Single(cls, TwPropertyId.Rotate).Value.X);

    [Theory]
    [InlineData("scale-95", 0.95f)]
    [InlineData("scale-110", 1.10f)]
    public void Scale(string cls, float expected) =>
        Assert.Equal(expected, Single(cls, TwPropertyId.Scale).Value.X, 3);

    [Theory]
    [InlineData("translate-x-4", TwPropertyId.TranslateX, 16f)]
    [InlineData("-translate-y-2", TwPropertyId.TranslateY, -8f)]
    [InlineData("translate-x-[9px]", TwPropertyId.TranslateX, 9f)]
    public void Translate(string cls, TwPropertyId property, float expected) =>
        Assert.Equal(expected, Single(cls, property).Value.X);

    [Theory]
    [InlineData("z-10", 10f)]
    [InlineData("z-auto", 0f)]
    public void ZIndex(string cls, float expected) =>
        Assert.Equal(expected, Single(cls, TwPropertyId.ZIndex).Value.X);

    [Fact]
    public void Size_sets_width_and_height()
    {
        Assert.Equal(48f, Single("size-12", TwPropertyId.Width).Value.X);
        Assert.Equal(48f, Single("size-12", TwPropertyId.Height).Value.X);
    }

    [Fact]
    public void Gradient_direction_and_stops()
    {
        const string cls = "bg-gradient-to-r from-indigo-500 via-purple-500 to-pink-500";
        Assert.Equal((byte)TwGradientDirection.Right, (byte)Single(cls, TwPropertyId.GradientDirection).Value.X);
        Assert.Equal(0xFF6366F1u, Single(cls, TwPropertyId.GradientFrom).Value.Rgba);
        Assert.Equal(0xFFA855F7u, Single(cls, TwPropertyId.GradientVia).Value.Rgba);
        Assert.Equal(0xFFEC4899u, Single(cls, TwPropertyId.GradientTo).Value.Rgba);
    }

    [Fact]
    public void Unknown_gradient_direction_is_diagnostic()
    {
        var diags = TwEngine.Validate("bg-gradient-to-x");
        Assert.Single(diags);
        Assert.Contains("gradient direction", diags[0].Message);
    }

    [Theory]
    [InlineData("mx-auto", TwPropertyId.AlignSelfX, TwAlign.Center)]
    [InlineData("my-auto", TwPropertyId.AlignSelfY, TwAlign.Center)]
    [InlineData("ml-auto", TwPropertyId.AlignSelfX, TwAlign.End)]
    [InlineData("mr-auto", TwPropertyId.AlignSelfX, TwAlign.Start)]
    [InlineData("mt-auto", TwPropertyId.AlignSelfY, TwAlign.End)]
    [InlineData("mb-auto", TwPropertyId.AlignSelfY, TwAlign.Start)]
    public void Auto_margins_become_alignment(string cls, TwPropertyId property, TwAlign expected) =>
        Assert.Equal((byte)expected, (byte)Single(cls, property).Value.X);

    [Fact]
    public void M_auto_centers_both_axes()
    {
        Assert.Equal((byte)TwAlign.Center, (byte)Single("m-auto", TwPropertyId.AlignSelfX).Value.X);
        Assert.Equal((byte)TwAlign.Center, (byte)Single("m-auto", TwPropertyId.AlignSelfY).Value.X);
    }

    [Theory]
    [InlineData("text-[17]", 17f)]
    [InlineData("text-[17px]", 17f)]
    public void Arbitrary_text_size(string cls, float expected) =>
        Assert.Equal(expected, Single(cls, TwPropertyId.FontSize).Value.X);

    [Fact]
    public void Positioning_utilities_get_helpful_message()
    {
        var diags = TwEngine.Validate("absolute top-4 inset-0");
        Assert.Equal(3, diags.Length);
        Assert.All(diags, d => Assert.Contains("positioning", d.Message));
    }

    [Fact]
    public void Capitalize_and_screen_get_helpful_messages()
    {
        var diags = TwEngine.Validate("capitalize w-screen");
        Assert.Equal(2, diags.Length);
        Assert.Contains("TextTransform", diags[0].Message);
        Assert.Contains("w-full", diags[1].Message);
    }
}
