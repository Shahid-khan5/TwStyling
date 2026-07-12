using System.Collections.Generic;

namespace TwStyling;

/// <summary>
/// The Tailwind default theme scales, compiled in as static data.
/// This is the single source of truth: the resolver, the (future) build-time
/// validator, and generated docs/skills all read from here.
/// </summary>
public static class TwTables
{
    /// <summary>Device-independent units per spacing step (Tailwind: 1 step = 0.25rem = 4px).</summary>
    public const float SpacingUnit = 4f;

    /// <summary>font size name → (size DIU, line-height multiplier).</summary>
    public static readonly IReadOnlyDictionary<string, (float Size, float Line)> FontSizes =
        new Dictionary<string, (float, float)>(StringComparer.Ordinal)
        {
            ["xs"] = (12, 16f / 12),
            ["sm"] = (14, 20f / 14),
            ["base"] = (16, 24f / 16),
            ["lg"] = (18, 28f / 18),
            ["xl"] = (20, 28f / 20),
            ["2xl"] = (24, 32f / 24),
            ["3xl"] = (30, 36f / 30),
            ["4xl"] = (36, 40f / 36),
            ["5xl"] = (48, 1),
            ["6xl"] = (60, 1),
            ["7xl"] = (72, 1),
            ["8xl"] = (96, 1),
            ["9xl"] = (128, 1),
        };

    /// <summary>font-* weight name → numeric weight.</summary>
    public static readonly IReadOnlyDictionary<string, float> FontWeights =
        new Dictionary<string, float>(StringComparer.Ordinal)
        {
            ["thin"] = 100,
            ["extralight"] = 200,
            ["light"] = 300,
            ["normal"] = 400,
            ["medium"] = 500,
            ["semibold"] = 600,
            ["bold"] = 700,
            ["extrabold"] = 800,
            ["black"] = 900,
        };

    /// <summary>rounded-* name → radius in DIU. Empty key is bare <c>rounded</c>.</summary>
    public static readonly IReadOnlyDictionary<string, float> Radii =
        new Dictionary<string, float>(StringComparer.Ordinal)
        {
            ["none"] = 0,
            ["sm"] = 2,
            [""] = 4,
            ["md"] = 6,
            ["lg"] = 8,
            ["xl"] = 12,
            ["2xl"] = 16,
            ["3xl"] = 24,
            ["full"] = 9999,
        };

    /// <summary>
    /// shadow-* name → primary shadow layer (Tailwind multi-layer shadows reduced
    /// to the dominant layer, which native single-shadow systems can render).
    /// Color is 0xAARRGGBB black with the layer's opacity in the alpha channel.
    /// Empty key is bare <c>shadow</c>.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, TwValue> Shadows =
        new Dictionary<string, TwValue>(StringComparer.Ordinal)
        {
            ["sm"] = TwValue.Shadow(0x0D000000, 0, 1, 2),      // 5% black
            [""] = TwValue.Shadow(0x1A000000, 0, 1, 3),        // 10%
            ["md"] = TwValue.Shadow(0x1A000000, 0, 4, 6),
            ["lg"] = TwValue.Shadow(0x1A000000, 0, 10, 15),
            ["xl"] = TwValue.Shadow(0x1A000000, 0, 20, 25),
            ["2xl"] = TwValue.Shadow(0x40000000, 0, 25, 50),   // 25%
            ["none"] = TwValue.Shadow(0x00000000, 0, 0, 0),
        };

    /// <summary>
    /// text-shadow-* name → shadow layer. Native has one Shadow per element, so a text
    /// shadow renders as the element's shadow (ideal on transparent-background labels).
    /// Empty key is bare <c>text-shadow</c>.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, TwValue> TextShadows =
        new Dictionary<string, TwValue>(StringComparer.Ordinal)
        {
            ["sm"] = TwValue.Shadow(0x26000000, 0, 1, 1),   // ~15% black
            [""] = TwValue.Shadow(0x40000000, 0, 1, 2),     // ~25%
            ["lg"] = TwValue.Shadow(0x59000000, 0, 2, 4),   // ~35%
            ["none"] = TwValue.Shadow(0x00000000, 0, 0, 0),
        };

    /// <summary>tracking-* name → letter spacing in em.</summary>
    public static readonly IReadOnlyDictionary<string, float> Tracking =
        new Dictionary<string, float>(StringComparer.Ordinal)
        {
            ["tighter"] = -0.05f,
            ["tight"] = -0.025f,
            ["normal"] = 0f,
            ["wide"] = 0.025f,
            ["wider"] = 0.05f,
            ["widest"] = 0.1f,
        };

    /// <summary>Default border color when <c>border</c> is used without an explicit border-* color (Tailwind preflight: gray-200).</summary>
    public const uint DefaultBorderColor = 0xFFE5E7EB;

    /// <summary>max-w-* named scale → DIU. float.MaxValue = none.</summary>
    public static readonly IReadOnlyDictionary<string, float> MaxWidths =
        new Dictionary<string, float>(StringComparer.Ordinal)
        {
            ["xs"] = 320, ["sm"] = 384, ["md"] = 448, ["lg"] = 512, ["xl"] = 576,
            ["2xl"] = 672, ["3xl"] = 768, ["4xl"] = 896, ["5xl"] = 1024,
            ["6xl"] = 1152, ["7xl"] = 1280, ["none"] = float.MaxValue,
        };

    /// <summary>leading-* named scale → line-height multiplier.</summary>
    public static readonly IReadOnlyDictionary<string, float> Leadings =
        new Dictionary<string, float>(StringComparer.Ordinal)
        {
            ["none"] = 1f, ["tight"] = 1.25f, ["snug"] = 1.375f,
            ["normal"] = 1.5f, ["relaxed"] = 1.625f, ["loose"] = 2f,
        };


    /// <summary>Variant prefix → its meaning. Used by the parser and by generated docs.</summary>
    public static readonly IReadOnlyDictionary<string, TwVariantKind> Variants =
        new Dictionary<string, TwVariantKind>(StringComparer.Ordinal)
        {
            ["dark"] = new(TwVariantClass.Theme, theme: TwTheme.Dark),
            ["light"] = new(TwVariantClass.Theme, theme: TwTheme.Light),
            ["pressed"] = new(TwVariantClass.Interactive, state: TwInteractiveState.Pressed),
            ["active"] = new(TwVariantClass.Interactive, state: TwInteractiveState.Pressed),
            ["hover"] = new(TwVariantClass.Interactive, state: TwInteractiveState.Hover),
            ["focus"] = new(TwVariantClass.Interactive, state: TwInteractiveState.Focus),
            ["disabled"] = new(TwVariantClass.Interactive, state: TwInteractiveState.Disabled),
            ["android"] = new(TwVariantClass.Platform, platforms: TwPlatforms.Android),
            ["ios"] = new(TwVariantClass.Platform, platforms: TwPlatforms.Ios),
            ["mac"] = new(TwVariantClass.Platform, platforms: TwPlatforms.Mac),
            ["macos"] = new(TwVariantClass.Platform, platforms: TwPlatforms.Mac),
            ["windows"] = new(TwVariantClass.Platform, platforms: TwPlatforms.Windows),
            ["tizen"] = new(TwVariantClass.Platform, platforms: TwPlatforms.Tizen),
            ["mobile"] = new(TwVariantClass.Platform, platforms: TwPlatforms.Android | TwPlatforms.Ios),
            ["sm"] = new(TwVariantClass.Breakpoint, breakpointMinWidth: 640),
            ["md"] = new(TwVariantClass.Breakpoint, breakpointMinWidth: 768),
            ["lg"] = new(TwVariantClass.Breakpoint, breakpointMinWidth: 1024),
            ["xl"] = new(TwVariantClass.Breakpoint, breakpointMinWidth: 1280),
            ["2xl"] = new(TwVariantClass.Breakpoint, breakpointMinWidth: 1536),
            ["phone"] = new(TwVariantClass.Idiom, idioms: TwIdioms.Phone),
            ["tablet"] = new(TwVariantClass.Idiom, idioms: TwIdioms.Tablet),
            ["desktop"] = new(TwVariantClass.Idiom, idioms: TwIdioms.Desktop),
            ["tv"] = new(TwVariantClass.Idiom, idioms: TwIdioms.Tv),
            ["watch"] = new(TwVariantClass.Idiom, idioms: TwIdioms.Watch),
        };
}

public enum TwVariantClass : byte
{
    Platform,
    Idiom,
    Theme,
    Interactive,
    Breakpoint,
}

public readonly struct TwVariantKind(
    TwVariantClass @class,
    TwPlatforms platforms = TwPlatforms.None,
    TwIdioms idioms = TwIdioms.None,
    TwTheme theme = TwTheme.Any,
    TwInteractiveState state = TwInteractiveState.None,
    float breakpointMinWidth = 0)
{
    public readonly TwVariantClass Class = @class;
    public readonly TwPlatforms Platforms = platforms;
    public readonly TwIdioms Idioms = idioms;
    public readonly TwTheme Theme = theme;
    public readonly TwInteractiveState State = state;
    public readonly float BreakpointMinWidth = breakpointMinWidth;
}

