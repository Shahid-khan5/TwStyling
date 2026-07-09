namespace Tw.Core;

/// <summary>
/// Framework-neutral style properties a utility can resolve to.
/// Adapters map these onto concrete framework properties per control type.
/// </summary>
public enum TwPropertyId : byte
{
    Background,
    TextColor,
    BorderColor,
    BorderWidth,
    CornerRadius,
    Padding,
    Margin,
    Gap,
    FontSize,
    LineHeight,
    FontWeight,
    FontItalic,
    TextAlign,
    CharacterSpacingEm,
    LineClamp,
    Shadow,
    Opacity,
    Width,
    Height,
    MinWidth,
    MinHeight,
    MaxWidth,
    MaxHeight,
    /// <summary>Enum: <see cref="TwTextDecoration"/>.</summary>
    TextDecoration,
    /// <summary>Enum: <see cref="TwTextTransform"/>.</summary>
    TextTransform,
    /// <summary>Scalar: degrees.</summary>
    Rotate,
    /// <summary>Scalar: factor (scale-95 → 0.95).</summary>
    Scale,
    /// <summary>Scalar: DIU.</summary>
    TranslateX,
    /// <summary>Scalar: DIU.</summary>
    TranslateY,
    ZIndex,
    /// <summary>Enum: <see cref="TwAlign"/> — horizontal self-alignment (mx-auto / ml-auto…).</summary>
    AlignSelfX,
    /// <summary>Enum: <see cref="TwAlign"/> — vertical self-alignment (my-auto / mt-auto…).</summary>
    AlignSelfY,
    /// <summary>Enum: <see cref="TwGradientDirection"/>. Presence turns Background into a gradient.</summary>
    GradientDirection,
    GradientFrom,
    GradientVia,
    GradientTo,
    /// <summary>Enum: <see cref="TwFlexDirection"/> (flex / flex-row / flex-col).</summary>
    FlexDirection,
    /// <summary>Enum: <see cref="TwFlexWrap"/>.</summary>
    FlexWrap,
    /// <summary>Enum: <see cref="TwJustify"/>.</summary>
    JustifyContent,
    /// <summary>Enum: <see cref="TwAlignItems"/>.</summary>
    AlignItems,
    /// <summary>Enum: <see cref="TwAlignSelfFlex"/> (self-*).</summary>
    FlexAlignSelf,
    /// <summary>Scalar factor (grow / grow-0).</summary>
    FlexGrow,
    /// <summary>Scalar factor (shrink / shrink-0).</summary>
    FlexShrink,
    /// <summary>X = value, Y = 1 when relative fraction (basis-1/2); X NaN = auto.</summary>
    FlexBasis,
    /// <summary>Scalar: number of star columns (grid-cols-N).</summary>
    GridColumns,
    /// <summary>Scalar: number of star rows (grid-rows-N).</summary>
    GridRows,
    /// <summary>Scalar: 0-based column (col-start-N is 1-based in CSS).</summary>
    GridColumn,
    GridRow,
    GridColumnSpan,
    GridRowSpan,
    /// <summary>Scalar bool: overflow-hidden → 1.</summary>
    Clip,
    /// <summary>Scalar bool: hidden → 0, visible → 1.</summary>
    Visible,
    /// <summary>Scalar: <see cref="TwTransitionProps"/> flags.</summary>
    TransitionProps,
    /// <summary>Scalar: milliseconds.</summary>
    TransitionDuration,
    /// <summary>Scalar: milliseconds.</summary>
    TransitionDelay,
    /// <summary>Enum: <see cref="TwEasing"/>.</summary>
    TransitionEasing,
    /// <summary>Enum: <see cref="TwKeyframes"/> (animate-*).</summary>
    Keyframes,
    /// <summary>Enum: <see cref="TwObjectFit"/> (object-cover / object-contain…). Image only.</summary>
    ObjectFit,
    /// <summary>Scalar: horizontal scale factor (scale-x-*). Negative flips.</summary>
    ScaleX,
    /// <summary>Scalar: vertical scale factor (scale-y-*). Negative flips.</summary>
    ScaleY,
    /// <summary>Scalar 0–1: transform-origin X anchor (origin-*).</summary>
    TransformOriginX,
    /// <summary>Scalar 0–1: transform-origin Y anchor (origin-*).</summary>
    TransformOriginY,
    /// <summary>Scalar bool: pointer-events-none → 1 (input transparent), pointer-events-auto → 0.</summary>
    PointerEventsNone,
    /// <summary>Enum: <see cref="TwAlignContent"/> (content-*). FlexLayout only.</summary>
    AlignContent,
    /// <summary>Scalar: flex order (order-*). FlexLayout child.</summary>
    Order,
    /// <summary>Enum: <see cref="TwLineBreak"/> (whitespace-* / break-*). Label only.</summary>
    LineBreak,
    /// <summary>Color: shadow tint (shadow-{color}). Folds into <see cref="Shadow"/>'s brush.</summary>
    ShadowColor,
}

/// <summary>object-fit values (object-*). Maps to the platform image scaling mode.</summary>
public enum TwObjectFit : byte
{
    Contain = 0,
    Cover = 1,
    Fill = 2,
    None = 3,
    ScaleDown = 4,
}

/// <summary>align-content values (content-*).</summary>
public enum TwAlignContent : byte
{
    Start = 0,
    End = 1,
    Center = 2,
    Between = 3,
    Around = 4,
    Evenly = 5,
    Stretch = 6,
}

/// <summary>Text line-break behavior (whitespace-* / break-*).</summary>
public enum TwLineBreak : byte
{
    /// <summary>whitespace-normal / break-words — wrap on word boundaries.</summary>
    WordWrap = 0,
    /// <summary>break-all — wrap on any character.</summary>
    CharacterWrap = 1,
    /// <summary>whitespace-nowrap — never wrap.</summary>
    NoWrap = 2,
}

/// <summary>Built-in looping keyframe animations (animate-*).</summary>
public enum TwKeyframes : byte
{
    None = 0,
    Spin = 1,
    Pulse = 2,
    Bounce = 3,
}

public enum TwFlexDirection : byte { Row = 0, RowReverse = 1, Column = 2, ColumnReverse = 3 }

public enum TwFlexWrap : byte { NoWrap = 0, Wrap = 1, WrapReverse = 2 }

public enum TwJustify : byte { Start = 0, End = 1, Center = 2, Between = 3, Around = 4, Evenly = 5 }

public enum TwAlignItems : byte { Stretch = 0, Start = 1, End = 2, Center = 3 }

public enum TwAlignSelfFlex : byte { Auto = 0, Stretch = 1, Start = 2, End = 3, Center = 4 }

public enum TwEasing : byte { InOut = 0, Linear = 1, In = 2, Out = 3 }

[Flags]
public enum TwTransitionProps : byte
{
    None = 0,
    Colors = 1,
    Opacity = 2,
    Transform = 4,
    /// <summary>Font size and dimensions — only <c>transition-all</c> includes these (CSS parity).</summary>
    Sizes = 8,
    Default = Colors | Opacity | Transform,
    All = Colors | Opacity | Transform | Sizes,
}

public enum TwTextDecoration : byte
{
    None = 0,
    Underline = 1,
    Strikethrough = 2,
}

public enum TwTextTransform : byte
{
    None = 0,
    Uppercase = 1,
    Lowercase = 2,
}

public enum TwAlign : byte
{
    Center = 0,
    Start = 1,
    End = 2,
}

/// <summary>bg-gradient-to-* directions, clockwise from top.</summary>
public enum TwGradientDirection : byte
{
    Top = 0,
    TopRight = 1,
    Right = 2,
    BottomRight = 3,
    Bottom = 4,
    BottomLeft = 5,
    Left = 6,
    TopLeft = 7,
}

[Flags]
public enum TwPlatforms : byte
{
    None = 0,
    Android = 1 << 0,
    Ios = 1 << 1,
    Mac = 1 << 2,
    Windows = 1 << 3,
    Tizen = 1 << 4,
    Any = Android | Ios | Mac | Windows | Tizen,
}

[Flags]
public enum TwIdioms : byte
{
    None = 0,
    Phone = 1 << 0,
    Tablet = 1 << 1,
    Desktop = 1 << 2,
    Tv = 1 << 3,
    Watch = 1 << 4,
    Any = Phone | Tablet | Desktop | Tv | Watch,
}

/// <summary>Theme qualifier on a utility (<c>dark:</c> / <c>light:</c>).</summary>
public enum TwTheme : byte
{
    Any = 0,
    Light = 1,
    Dark = 2,
}

/// <summary>Interactive visual state qualifier (<c>pressed:</c>, <c>hover:</c>, …).</summary>
public enum TwInteractiveState : byte
{
    None = 0,
    Pressed = 1,
    Hover = 2,
    Focus = 3,
    Disabled = 4,
}

public enum TwTextAlign : byte
{
    Start = 0,
    Center = 1,
    End = 2,
    Justify = 3,
}

public enum TwValueKind : byte
{
    /// <summary>Single float in <see cref="TwValue.X"/>.</summary>
    Scalar,
    /// <summary>0xAARRGGBB in <see cref="TwValue.Rgba"/>.</summary>
    Color,
    /// <summary>Left/Top/Right/Bottom in X/Y/Z/W; NaN = side not specified.</summary>
    Edges,
    /// <summary>TopLeft/TopRight/BottomLeft/BottomRight in X/Y/Z/W; NaN = corner not specified.</summary>
    Corners,
    /// <summary>Color in Rgba (alpha carries opacity); X/Y = offset, Z = blur radius.</summary>
    Shadow,
    /// <summary>Enum ordinal in <see cref="TwValue.X"/> (e.g. <see cref="TwTextAlign"/>).</summary>
    Enum,
}

/// <summary>
/// Compact framework-neutral value. One struct covers every declaration shape so
/// compiled plans are flat arrays with no per-value heap objects.
/// </summary>
public readonly struct TwValue
{
    public readonly TwValueKind Kind;
    public readonly uint Rgba;
    public readonly float X, Y, Z, W;

    /// <summary>Sentinel scalar meaning "fill available space" (w-full / h-full).</summary>
    public const float Full = float.PositiveInfinity;

    private TwValue(TwValueKind kind, uint rgba, float x, float y, float z, float w)
    {
        Kind = kind;
        Rgba = rgba;
        X = x;
        Y = y;
        Z = z;
        W = w;
    }

    public static TwValue Scalar(float value) => new(TwValueKind.Scalar, 0, value, 0, 0, 0);
    /// <summary>A length in absolute DIU (numeric leading-*), tagged (Y=1) so the adapter divides by font size.</summary>
    public static TwValue AbsoluteLength(float diu) => new(TwValueKind.Scalar, 0, diu, 1, 0, 0);
    /// <summary>True when this scalar carries an absolute length that needs font-size conversion.</summary>
    public bool IsAbsoluteLength => Kind == TwValueKind.Scalar && Y > 0;
    public static TwValue Color(uint rgba) => new(TwValueKind.Color, rgba, 0, 0, 0, 0);
    public static TwValue Edges(float left, float top, float right, float bottom) =>
        new(TwValueKind.Edges, 0, left, top, right, bottom);
    public static TwValue Corners(float topLeft, float topRight, float bottomLeft, float bottomRight) =>
        new(TwValueKind.Corners, 0, topLeft, topRight, bottomLeft, bottomRight);
    public static TwValue Shadow(uint rgba, float offsetX, float offsetY, float blur) =>
        new(TwValueKind.Shadow, rgba, offsetX, offsetY, blur, 0);
    public static TwValue Enum(byte ordinal) => new(TwValueKind.Enum, 0, ordinal, 0, 0, 0);

    public bool IsFull => Kind == TwValueKind.Scalar && float.IsPositiveInfinity(X);

    /// <summary>
    /// Reduces a per-side Edges value to the single width a uniform-stroke renderer
    /// (MAUI's <c>StrokeThickness</c>) can carry: exact when all sides are equal, else
    /// the largest specified side as a best-effort approximation. NaN (unset) sides are
    /// ignored. A framework with a per-side border thickness (WPF) reads X/Y/Z/W directly.
    /// </summary>
    public float UniformEdge()
    {
        float max = 0f;
        if (!float.IsNaN(X) && X > max) max = X;
        if (!float.IsNaN(Y) && Y > max) max = Y;
        if (!float.IsNaN(Z) && Z > max) max = Z;
        if (!float.IsNaN(W) && W > max) max = W;
        return max;
    }
}

/// <summary>One resolved (property, value) pair inside a compiled plan.</summary>
public readonly struct TwDeclaration(TwPropertyId property, TwValue value)
{
    public readonly TwPropertyId Property = property;
    public readonly TwValue Value = value;
}

/// <summary>
/// The full variant qualification of a utility, e.g. <c>ios:dark:pressed:bg-blue-700</c>.
/// Platform and idiom are static per process and filtered out at plan-compile time.
/// </summary>
public readonly struct TwVariantSet
{
    public readonly TwPlatforms Platforms;
    public readonly TwIdioms Idioms;
    public readonly TwTheme Theme;
    public readonly TwInteractiveState State;

    /// <summary>Responsive breakpoint minimum window width in DIU; 0 = unqualified.</summary>
    public readonly float BreakpointMinWidth;

    public TwVariantSet(TwPlatforms platforms, TwIdioms idioms, TwTheme theme, TwInteractiveState state, float breakpointMinWidth = 0)
    {
        Platforms = platforms;
        Idioms = idioms;
        Theme = theme;
        State = state;
        BreakpointMinWidth = breakpointMinWidth;
    }

    public static readonly TwVariantSet Default = new(TwPlatforms.Any, TwIdioms.Any, TwTheme.Any, TwInteractiveState.None);

    public TwVariantSet With(TwPlatforms p) => new(Platforms == TwPlatforms.Any ? p : Platforms | p, Idioms, Theme, State, BreakpointMinWidth);
    public TwVariantSet With(TwIdioms i) => new(Platforms, Idioms == TwIdioms.Any ? i : Idioms | i, Theme, State, BreakpointMinWidth);
    public TwVariantSet With(TwTheme t) => new(Platforms, Idioms, t, State, BreakpointMinWidth);
    public TwVariantSet With(TwInteractiveState s) => new(Platforms, Idioms, Theme, s, BreakpointMinWidth);
    public TwVariantSet WithBreakpoint(float minWidth) => new(Platforms, Idioms, Theme, State, minWidth);

    public bool AppliesTo(in TwEnvironment env) =>
        (Platforms & env.Platform) != 0 && (Idioms & env.Idiom) != 0;
}

/// <summary>Static facts about the running process, supplied once by the adapter.</summary>
public readonly struct TwEnvironment(TwPlatforms platform, TwIdioms idiom)
{
    public readonly TwPlatforms Platform = platform;
    public readonly TwIdioms Idiom = idiom;
}

/// <summary>A problem found while compiling a class string. Never a silent no-op.</summary>
public readonly struct TwDiagnostic(string classString, string token, string message)
{
    public readonly string ClassString = classString;
    public readonly string Token = token;
    public readonly string Message = message;

    public override string ToString() => $"[Tw] '{Token}' in \"{ClassString}\": {Message}";
}
