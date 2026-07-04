using System.Collections.Concurrent;
using Microsoft.Maui.Controls.Shapes;
using Tw.Core;

namespace Tw.Maui;

/// <summary>
/// A <see cref="StylePlan"/> lowered onto concrete MAUI bindable properties for one
/// control type: pre-boxed values, nothing left to parse or convert at apply time.
/// Cached per (plan, type) — every element sharing a class string shares this.
/// </summary>
internal sealed class TwMauiPlan
{
    internal readonly struct Entry(BindableProperty property, object? light, object? dark)
    {
        public readonly BindableProperty Property = property;
        public readonly object? Light = light;
        public readonly object? Dark = dark;
        public bool Differs => !Equals(Light, Dark);
    }

    /// <summary>
    /// Wraps values that are MAUI Elements/BindableObjects (shapes, brushes, shadows):
    /// they get a Parent when attached, so the cached plan must NOT share one instance
    /// across elements. The plan stores this factory; the applicator materializes a
    /// fresh instance per element. Equality is by the value key, so theme-identical
    /// values still compare equal and skip AppThemeBinding.
    /// </summary>
    internal sealed class FreshValue(object key, Func<object> factory)
    {
        public readonly Func<object> Factory = factory;
        private readonly object _key = key;

        public override bool Equals(object? obj) => obj is FreshValue other && Equals(_key, other._key);
        public override int GetHashCode() => _key.GetHashCode();
    }

    /// <summary>Unwrap a plan value for one element: fresh instance for element-typed values.</summary>
    internal static object? Materialize(object? value) => value is FreshValue f ? f.Factory() : value;

    internal readonly struct StateEntries(string visualState, Entry[] entries)
    {
        public readonly string VisualState = visualState;
        public readonly Entry[] Entries = entries;
    }

    public Entry[] Setters = [];
    public StateEntries[] States = [];

    /// <summary>Responsive overlays, ascending MinWidth; applied on top of Setters.</summary>
    public (float MinWidth, Entry[] Entries)[] Breakpoints = [];

    /// <summary>Non-null when the class string asks property changes to animate.</summary>
    public TwTransitionSpec? Transition;

    /// <summary>Looping keyframe animation (animate-spin/pulse/bounce).</summary>
    public TwKeyframes Keyframes;

    public bool AnySetterDiffersByTheme;
    public bool AnyStateDiffersByTheme;
    public bool HasStates => States.Length > 0;

    private static readonly ConcurrentDictionary<(StylePlan, Type), TwMauiPlan> Cache = new();

    public static TwMauiPlan Get(StylePlan plan, Type elementType) =>
        Cache.GetOrAdd((plan, elementType), static key => Compile(key.Item1, key.Item2));

    // ------------------------------------------------------------- compilation

    private static TwMauiPlan Compile(StylePlan plan, Type type)
    {
        var result = new TwMauiPlan();

        var setters = new List<Entry>();
        CompileBucket(plan.Light, plan.Dark, type, setters);
        result.Setters = setters.ToArray();
        result.AnySetterDiffersByTheme = setters.Exists(e => e.Differs);

        if (plan.States.Length > 0)
        {
            var states = new List<StateEntries>(plan.States.Length);
            foreach (var state in plan.States)
            {
                var entries = new List<Entry>();
                CompileBucket(state.Light, state.Dark, type, entries);
                if (entries.Count > 0)
                {
                    states.Add(new StateEntries(VisualStateName(state.State), entries.ToArray()));
                    result.AnyStateDiffersByTheme |= entries.Exists(e => e.Differs);
                }
            }
            result.States = states.ToArray();
        }

        if (plan.Breakpoints.Length > 0)
        {
            var overlays = new List<(float, Entry[])>(plan.Breakpoints.Length);
            foreach (var bp in plan.Breakpoints)
            {
                var entries = new List<Entry>();
                CompileBucket(bp.Light, bp.Dark, type, entries);
                if (entries.Count > 0)
                    overlays.Add((bp.MinWidth, entries.ToArray()));
            }
            result.Breakpoints = overlays.ToArray();
        }

        result.Transition = TransitionOf(plan.Light);
        result.Keyframes = TryFind(plan.Light, TwPropertyId.Keyframes, out var kf)
            ? (TwKeyframes)(byte)kf.X
            : TwKeyframes.None;
        return result;
    }

    private static TwTransitionSpec? TransitionOf(TwDeclaration[] set)
    {
        if (!TryFind(set, TwPropertyId.TransitionProps, out var props) || (byte)props.X == 0)
            return null;
        float duration = TryFind(set, TwPropertyId.TransitionDuration, out var d) ? d.X : 150;
        float delay = TryFind(set, TwPropertyId.TransitionDelay, out var dl) ? dl.X : 0;
        var easing = TryFind(set, TwPropertyId.TransitionEasing, out var e) ? (TwEasing)(byte)e.X : TwEasing.InOut;
        return new TwTransitionSpec((TwTransitionProps)(byte)props.X, (int)duration, (int)delay, easing);
    }

    private static string VisualStateName(TwInteractiveState state) => state switch
    {
        TwInteractiveState.Pressed => "Pressed",
        TwInteractiveState.Hover => VisualStateManager.CommonStates.PointerOver,
        TwInteractiveState.Focus => VisualStateManager.CommonStates.Focused,
        _ => VisualStateManager.CommonStates.Disabled,
    };

    /// <summary>
    /// Lowers a light/dark declaration pair onto (BindableProperty, boxed value) entries.
    /// A neutral property may fan out to several framework properties (line-clamp →
    /// MaxLines + LineBreakMode) or fold several declarations into one value
    /// (font weight + italic → FontAttributes).
    /// </summary>
    private static void CompileBucket(
        TwDeclaration[] light, TwDeclaration[] dark, Type type, List<Entry> output)
    {
        // Font weight/italic fold into a single FontAttributes value per theme.
        var lightAttrs = FontAttributesOf(light);
        var darkAttrs = FontAttributesOf(dark);
        if (lightAttrs is not null || darkAttrs is not null)
        {
            var p = Prop(type, TwProps.FontAttributes);
            if (p is not null)
                output.Add(new Entry(p, lightAttrs ?? FontAttributes.None, darkAttrs ?? FontAttributes.None));
        }

        // Gradient tokens fold into one Background brush per theme (and win over bg-color).
        bool hasGradient = HasGradient(light) || HasGradient(dark);
        if (hasGradient)
        {
            output.Add(new Entry(VisualElement.BackgroundProperty,
                (object?)GradientBrushOf(light) ?? VisualElement.BackgroundProperty.DefaultValue,
                (object?)GradientBrushOf(dark) ?? VisualElement.BackgroundProperty.DefaultValue));
        }

        foreach (var id in UnionProperties(light, dark))
        {
            if (id is TwPropertyId.FontWeight or TwPropertyId.FontItalic
                or TwPropertyId.GradientDirection or TwPropertyId.GradientFrom
                or TwPropertyId.GradientVia or TwPropertyId.GradientTo)
                continue; // folded above

            if (id is TwPropertyId.TransitionProps or TwPropertyId.TransitionDuration
                or TwPropertyId.TransitionDelay or TwPropertyId.TransitionEasing
                or TwPropertyId.Keyframes)
                continue; // extracted into TwMauiPlan.Transition / Keyframes

            if (id == TwPropertyId.Background && hasGradient)
                continue; // gradient wins

            bool hasLight = TryFind(light, id, out var lv);
            bool hasDark = TryFind(dark, id, out var dv);

            AddEntries(type, id, hasLight, lv, hasDark, dv, light, dark, output);
        }
    }

    private static bool HasGradient(TwDeclaration[] set) =>
        TryFind(set, TwPropertyId.GradientDirection, out _)
        || TryFind(set, TwPropertyId.GradientFrom, out _)
        || TryFind(set, TwPropertyId.GradientTo, out _);

    private static FreshValue? GradientBrushOf(TwDeclaration[] set)
    {
        if (!HasGradient(set)) return null;

        var direction = TryFind(set, TwPropertyId.GradientDirection, out var d)
            ? (TwGradientDirection)(byte)d.X
            : TwGradientDirection.Right;
        bool hasFrom = TryFind(set, TwPropertyId.GradientFrom, out var from);
        bool hasVia = TryFind(set, TwPropertyId.GradientVia, out var via);
        bool hasTo = TryFind(set, TwPropertyId.GradientTo, out var to);

        // Tailwind semantics: a missing from/to fades to transparent-of-the-other-stop.
        uint fromRgba = hasFrom ? from.Rgba : (hasTo ? to.Rgba & 0x00FFFFFF : 0x00000000);
        uint toRgba = hasTo ? to.Rgba : (hasFrom ? from.Rgba & 0x00FFFFFF : 0x00000000);
        uint viaRgba = via.Rgba;

        return new FreshValue((direction, fromRgba, hasVia, viaRgba, toRgba), () =>
        {
            var stops = new GradientStopCollection
            {
                new GradientStop(ToColor(fromRgba), 0f),
            };
            if (hasVia)
                stops.Add(new GradientStop(ToColor(viaRgba), 0.5f));
            stops.Add(new GradientStop(ToColor(toRgba), 1f));

            var (start, end) = direction switch
            {
                TwGradientDirection.Top => (new Point(0.5, 1), new Point(0.5, 0)),
                TwGradientDirection.TopRight => (new Point(0, 1), new Point(1, 0)),
                TwGradientDirection.Right => (new Point(0, 0.5), new Point(1, 0.5)),
                TwGradientDirection.BottomRight => (new Point(0, 0), new Point(1, 1)),
                TwGradientDirection.Bottom => (new Point(0.5, 0), new Point(0.5, 1)),
                TwGradientDirection.BottomLeft => (new Point(1, 0), new Point(0, 1)),
                TwGradientDirection.Left => (new Point(1, 0.5), new Point(0, 0.5)),
                _ => (new Point(1, 1), new Point(0, 0)),
            };

            return new LinearGradientBrush(stops, start, end);
        });
    }

    private static void AddEntries(
        Type type, TwPropertyId id,
        bool hasLight, TwValue lv, bool hasDark, TwValue dv,
        TwDeclaration[] lightSet, TwDeclaration[] darkSet,
        List<Entry> output)
    {
        switch (id)
        {
            case TwPropertyId.Background:
                Add(type, TwProps.Background, ColorOrDefault(hasLight, lv), ColorOrDefault(hasDark, dv));
                break;

            case TwPropertyId.TextColor:
                Add(type, TwProps.TextColor, ColorOrDefault(hasLight, lv), ColorOrDefault(hasDark, dv));
                break;

            case TwPropertyId.BorderColor:
                if (typeof(Border).IsAssignableFrom(type))
                    AddDirect(Border.StrokeProperty, BrushOf(hasLight, lv), BrushOf(hasDark, dv));
                else
                    Add(type, TwProps.BorderColor, ColorOrDefault(hasLight, lv), ColorOrDefault(hasDark, dv));
                break;

            case TwPropertyId.BorderWidth:
                if (typeof(Border).IsAssignableFrom(type))
                    AddDirect(Border.StrokeThicknessProperty, Boxed.Double(hasLight ? lv.X : 0), Boxed.Double(hasDark ? dv.X : 0));
                else
                    Add(type, TwProps.BorderWidth, Boxed.Double(hasLight ? lv.X : 0), Boxed.Double(hasDark ? dv.X : 0));
                break;

            case TwPropertyId.CornerRadius:
                AddCornerRadius(type, hasLight, lv, hasDark, dv, output);
                break;

            case TwPropertyId.Padding:
                Add(type, TwProps.Padding, ThicknessOf(hasLight, lv), ThicknessOf(hasDark, dv));
                break;

            case TwPropertyId.Margin:
                AddDirect(View.MarginProperty, ThicknessOf(hasLight, lv), ThicknessOf(hasDark, dv));
                break;

            case TwPropertyId.Gap:
                AddGap(type, hasLight, lv, hasDark, dv, output);
                break;

            case TwPropertyId.FontSize:
                Add(type, TwProps.FontSize, Boxed.Double(hasLight ? lv.X : 14), Boxed.Double(hasDark ? dv.X : 14));
                break;

            case TwPropertyId.LineHeight:
                Add(type, TwProps.LineHeight, Boxed.Double(hasLight ? lv.X : 1), Boxed.Double(hasDark ? dv.X : 1));
                break;

            case TwPropertyId.TextAlign:
                Add(type, TwProps.HorizontalTextAlignment,
                    hasLight ? ToTextAlignment(lv) : TextAlignment.Start,
                    hasDark ? ToTextAlignment(dv) : TextAlignment.Start);
                break;

            case TwPropertyId.CharacterSpacingEm:
                // em → DIU needs the font size; use the plan's own font size when present.
                Add(type, TwProps.CharacterSpacing,
                    Boxed.Double((hasLight ? lv.X : 0) * FontSizeIn(lightSet)),
                    Boxed.Double((hasDark ? dv.X : 0) * FontSizeIn(darkSet)));
                break;

            case TwPropertyId.LineClamp:
                if (typeof(Label).IsAssignableFrom(type))
                {
                    AddDirect(Label.MaxLinesProperty, Boxed.Int(hasLight ? (int)lv.X : -1), Boxed.Int(hasDark ? (int)dv.X : -1));
                    AddDirect(Label.LineBreakModeProperty, LineBreakMode.TailTruncation, LineBreakMode.TailTruncation);
                }
                break;

            case TwPropertyId.Opacity:
                AddDirect(VisualElement.OpacityProperty, Boxed.Double(hasLight ? lv.X : 1), Boxed.Double(hasDark ? dv.X : 1));
                break;

            case TwPropertyId.Width:
                AddSize(hasLight, lv, hasDark, dv, VisualElement.WidthRequestProperty, View.HorizontalOptionsProperty, output);
                break;

            case TwPropertyId.Height:
                AddSize(hasLight, lv, hasDark, dv, VisualElement.HeightRequestProperty, View.VerticalOptionsProperty, output);
                break;

            case TwPropertyId.MinWidth:
                AddDirect(VisualElement.MinimumWidthRequestProperty, Boxed.Double(hasLight ? lv.X : -1), Boxed.Double(hasDark ? dv.X : -1));
                break;

            case TwPropertyId.MinHeight:
                AddDirect(VisualElement.MinimumHeightRequestProperty, Boxed.Double(hasLight ? lv.X : -1), Boxed.Double(hasDark ? dv.X : -1));
                break;

            case TwPropertyId.MaxWidth:
                AddDirect(VisualElement.MaximumWidthRequestProperty, MaxOf(hasLight, lv), MaxOf(hasDark, dv));
                break;

            case TwPropertyId.MaxHeight:
                AddDirect(VisualElement.MaximumHeightRequestProperty, MaxOf(hasLight, lv), MaxOf(hasDark, dv));
                break;

            case TwPropertyId.TextDecoration:
                Add(type, TwProps.TextDecorations, DecorationOf(hasLight, lv), DecorationOf(hasDark, dv));
                break;

            case TwPropertyId.TextTransform:
                Add(type, TwProps.TextTransform, TransformOf(hasLight, lv), TransformOf(hasDark, dv));
                break;

            case TwPropertyId.Rotate:
                AddDirect(VisualElement.RotationProperty, Boxed.Double(hasLight ? lv.X : 0), Boxed.Double(hasDark ? dv.X : 0));
                break;

            case TwPropertyId.Scale:
                AddDirect(VisualElement.ScaleProperty, Boxed.Double(hasLight ? lv.X : 1), Boxed.Double(hasDark ? dv.X : 1));
                break;

            case TwPropertyId.TranslateX:
                AddDirect(VisualElement.TranslationXProperty, Boxed.Double(hasLight ? lv.X : 0), Boxed.Double(hasDark ? dv.X : 0));
                break;

            case TwPropertyId.TranslateY:
                AddDirect(VisualElement.TranslationYProperty, Boxed.Double(hasLight ? lv.X : 0), Boxed.Double(hasDark ? dv.X : 0));
                break;

            case TwPropertyId.ZIndex:
                AddDirect(VisualElement.ZIndexProperty, Boxed.Int(hasLight ? (int)lv.X : 0), Boxed.Int(hasDark ? (int)dv.X : 0));
                break;

            case TwPropertyId.AlignSelfX:
                AddDirect(View.HorizontalOptionsProperty, AlignOf(hasLight, lv), AlignOf(hasDark, dv));
                break;

            case TwPropertyId.AlignSelfY:
                AddDirect(View.VerticalOptionsProperty, AlignOf(hasLight, lv), AlignOf(hasDark, dv));
                break;

            case TwPropertyId.Shadow:
                AddDirect(VisualElement.ShadowProperty, ShadowOf(hasLight, lv), ShadowOf(hasDark, dv));
                break;

            case TwPropertyId.FlexDirection when typeof(FlexLayout).IsAssignableFrom(type):
                AddDirect(FlexLayout.DirectionProperty, FlexDirectionOf(hasLight, lv), FlexDirectionOf(hasDark, dv));
                break;

            case TwPropertyId.FlexWrap when typeof(FlexLayout).IsAssignableFrom(type):
                AddDirect(FlexLayout.WrapProperty, FlexWrapOf(hasLight, lv), FlexWrapOf(hasDark, dv));
                break;

            case TwPropertyId.JustifyContent when typeof(FlexLayout).IsAssignableFrom(type):
                AddDirect(FlexLayout.JustifyContentProperty, JustifyOf(hasLight, lv), JustifyOf(hasDark, dv));
                break;

            case TwPropertyId.AlignItems when typeof(FlexLayout).IsAssignableFrom(type):
                AddDirect(FlexLayout.AlignItemsProperty, AlignItemsOf(hasLight, lv), AlignItemsOf(hasDark, dv));
                break;

            case TwPropertyId.FlexAlignSelf: // attached — set on the child itself
                AddDirect(FlexLayout.AlignSelfProperty, AlignSelfOf(hasLight, lv), AlignSelfOf(hasDark, dv));
                break;

            case TwPropertyId.FlexGrow:
                AddDirect(FlexLayout.GrowProperty, FloatOf(hasLight, lv, 0f), FloatOf(hasDark, dv, 0f));
                break;

            case TwPropertyId.FlexShrink:
                AddDirect(FlexLayout.ShrinkProperty, FloatOf(hasLight, lv, 1f), FloatOf(hasDark, dv, 1f));
                break;

            case TwPropertyId.FlexBasis:
                AddDirect(FlexLayout.BasisProperty, BasisOf(hasLight, lv), BasisOf(hasDark, dv));
                break;

            case TwPropertyId.GridColumns when typeof(Grid).IsAssignableFrom(type):
                AddDirect(Grid.ColumnDefinitionsProperty, ColumnDefsOf(hasLight, lv), ColumnDefsOf(hasDark, dv));
                break;

            case TwPropertyId.GridRows when typeof(Grid).IsAssignableFrom(type):
                AddDirect(Grid.RowDefinitionsProperty, RowDefsOf(hasLight, lv), RowDefsOf(hasDark, dv));
                break;

            case TwPropertyId.GridColumn:
                AddDirect(Grid.ColumnProperty, Boxed.Int(hasLight ? (int)lv.X : 0), Boxed.Int(hasDark ? (int)dv.X : 0));
                break;

            case TwPropertyId.GridRow:
                AddDirect(Grid.RowProperty, Boxed.Int(hasLight ? (int)lv.X : 0), Boxed.Int(hasDark ? (int)dv.X : 0));
                break;

            case TwPropertyId.GridColumnSpan:
                AddDirect(Grid.ColumnSpanProperty, Boxed.Int(hasLight ? (int)lv.X : 1), Boxed.Int(hasDark ? (int)dv.X : 1));
                break;

            case TwPropertyId.GridRowSpan:
                AddDirect(Grid.RowSpanProperty, Boxed.Int(hasLight ? (int)lv.X : 1), Boxed.Int(hasDark ? (int)dv.X : 1));
                break;

            case TwPropertyId.Clip when typeof(Layout).IsAssignableFrom(type):
                AddDirect(Layout.IsClippedToBoundsProperty, (hasLight ? lv.X : 0) > 0, (hasDark ? dv.X : 0) > 0);
                break;

            case TwPropertyId.Visible:
                AddDirect(VisualElement.IsVisibleProperty, !hasLight || lv.X > 0, !hasDark || dv.X > 0);
                break;
        }

        void Add(Type t, (Type, BindableProperty)[] table, object? lightValue, object? darkValue)
        {
            var p = Prop(t, table);
            if (p is not null)
                output.Add(new Entry(p, lightValue ?? p.DefaultValue, darkValue ?? p.DefaultValue));
        }

        void AddDirect(BindableProperty p, object? lightValue, object? darkValue) =>
            output.Add(new Entry(p, lightValue ?? p.DefaultValue, darkValue ?? p.DefaultValue));
    }

    private static void AddCornerRadius(Type type, bool hasLight, TwValue lv, bool hasDark, TwValue dv, List<Entry> output)
    {
        if (typeof(Border).IsAssignableFrom(type))
        {
            output.Add(new Entry(Border.StrokeShapeProperty, RoundRect(hasLight, lv), RoundRect(hasDark, dv)));
        }
        else if (typeof(Button).IsAssignableFrom(type))
        {
            output.Add(new Entry(Button.CornerRadiusProperty, Boxed.Int(UniformRadius(hasLight, lv)), Boxed.Int(UniformRadius(hasDark, dv))));
        }
        else if (typeof(ImageButton).IsAssignableFrom(type))
        {
            output.Add(new Entry(ImageButton.CornerRadiusProperty, Boxed.Int(UniformRadius(hasLight, lv)), Boxed.Int(UniformRadius(hasDark, dv))));
        }
        else if (typeof(BoxView).IsAssignableFrom(type))
        {
            output.Add(new Entry(BoxView.CornerRadiusProperty, CornerRadiusOf(hasLight, lv), CornerRadiusOf(hasDark, dv)));
        }
        // Other controls can't render corners themselves — the diagnostic for this
        // lives in TwMauiApplicator so it can name the element.
    }

    private static void AddGap(Type type, bool hasLight, TwValue lv, bool hasDark, TwValue dv, List<Entry> output)
    {
        double L(float f) => float.IsNaN(f) ? 0 : f;

        if (typeof(VerticalStackLayout).IsAssignableFrom(type) || typeof(StackLayout).IsAssignableFrom(type))
            output.Add(new Entry(StackBase.SpacingProperty, Boxed.Double(hasLight ? L(lv.Y) : 0), Boxed.Double(hasDark ? L(dv.Y) : 0)));
        else if (typeof(HorizontalStackLayout).IsAssignableFrom(type))
            output.Add(new Entry(StackBase.SpacingProperty, Boxed.Double(hasLight ? L(lv.X) : 0), Boxed.Double(hasDark ? L(dv.X) : 0)));
        else if (typeof(Grid).IsAssignableFrom(type))
        {
            if (!float.IsNaN(lv.Y) || !float.IsNaN(dv.Y))
                output.Add(new Entry(Grid.RowSpacingProperty, Boxed.Double(hasLight ? L(lv.Y) : 0), Boxed.Double(hasDark ? L(dv.Y) : 0)));
            if (!float.IsNaN(lv.X) || !float.IsNaN(dv.X))
                output.Add(new Entry(Grid.ColumnSpacingProperty, Boxed.Double(hasLight ? L(lv.X) : 0), Boxed.Double(hasDark ? L(dv.X) : 0)));
        }
    }

    private static void AddSize(
        bool hasLight, TwValue lv, bool hasDark, TwValue dv,
        BindableProperty request, BindableProperty options, List<Entry> output)
    {
        bool anyFull = (hasLight && lv.IsFull) || (hasDark && dv.IsFull);
        if (anyFull)
        {
            output.Add(new Entry(options, LayoutOptions.Fill, LayoutOptions.Fill));
            return;
        }
        output.Add(new Entry(request, Boxed.Double(hasLight ? lv.X : -1), Boxed.Double(hasDark ? dv.X : -1)));
    }

    // ------------------------------------------------------------- conversions

    internal static Color ToColor(uint argb) => Color.FromUint(argb);

    private static object? ColorOrDefault(bool has, TwValue v) => has ? ToColor(v.Rgba) : null;

    private static object ToTextAlignment(TwValue v) => (TwTextAlign)(byte)v.X switch
    {
        TwTextAlign.Center => TextAlignment.Center,
        TwTextAlign.End => TextAlignment.End,
        TwTextAlign.Justify => TextAlignment.Justify,
        _ => TextAlignment.Start,
    };

    private static object? ThicknessOf(bool has, TwValue v) => has
        ? new Thickness(Z(v.X), Z(v.Y), Z(v.Z), Z(v.W))
        : null;

    private static object? CornerRadiusOf(bool has, TwValue v) => has
        ? new CornerRadius(Z(v.X), Z(v.Y), Z(v.Z), Z(v.W))
        : null;

    private static object? RoundRect(bool has, TwValue v)
    {
        if (!has) return null;
        var radius = new CornerRadius(Z(v.X), Z(v.Y), Z(v.Z), Z(v.W));
        return new FreshValue(radius, () => new RoundRectangle { CornerRadius = radius });
    }

    private static object? BrushOf(bool has, TwValue v)
    {
        if (!has) return null;
        uint rgba = v.Rgba;
        return new FreshValue(rgba, () => new SolidColorBrush(ToColor(rgba)));
    }

    private static object? ShadowOf(bool has, TwValue v)
    {
        if (!has) return null;
        var (rgba, x, y, blur) = (v.Rgba, v.X, v.Y, v.Z);
        return new FreshValue((rgba, x, y, blur), () =>
        {
            float alpha = ((rgba >> 24) & 0xFF) / 255f;
            return new Shadow
            {
                Brush = new SolidColorBrush(ToColor(rgba | 0xFF000000)),
                Offset = new Point(x, y),
                Radius = blur,
                Opacity = alpha,
            };
        });
    }

    private static int UniformRadius(bool has, TwValue v) => has ? (int)MathF.Max(Z(v.X), MathF.Max(Z(v.Y), MathF.Max(Z(v.Z), Z(v.W)))) : -1;

    private static object? MaxOf(bool has, TwValue v) =>
        has ? Boxed.Double(v.X >= float.MaxValue ? double.PositiveInfinity : v.X) : null;

    private static float Z(float f) => float.IsNaN(f) ? 0 : f;

    private static object? DecorationOf(bool has, TwValue v) => !has ? null : (TwTextDecoration)(byte)v.X switch
    {
        TwTextDecoration.Underline => TextDecorations.Underline,
        TwTextDecoration.Strikethrough => TextDecorations.Strikethrough,
        _ => TextDecorations.None,
    };

    private static object? TransformOf(bool has, TwValue v) => !has ? null : (TwTextTransform)(byte)v.X switch
    {
        TwTextTransform.Uppercase => TextTransform.Uppercase,
        TwTextTransform.Lowercase => TextTransform.Lowercase,
        _ => TextTransform.None,
    };

    private static object? AlignOf(bool has, TwValue v) => !has ? null : (TwAlign)(byte)v.X switch
    {
        TwAlign.Start => LayoutOptions.Start,
        TwAlign.End => LayoutOptions.End,
        _ => LayoutOptions.Center,
    };

    private static object? FloatOf(bool has, TwValue v, float fallback) => has ? v.X : fallback;

    private static object? FlexDirectionOf(bool has, TwValue v) => !has ? null : (TwFlexDirection)(byte)v.X switch
    {
        TwFlexDirection.RowReverse => Microsoft.Maui.Layouts.FlexDirection.RowReverse,
        TwFlexDirection.Column => Microsoft.Maui.Layouts.FlexDirection.Column,
        TwFlexDirection.ColumnReverse => Microsoft.Maui.Layouts.FlexDirection.ColumnReverse,
        _ => Microsoft.Maui.Layouts.FlexDirection.Row,
    };

    private static object? FlexWrapOf(bool has, TwValue v) => !has ? null : (TwFlexWrap)(byte)v.X switch
    {
        TwFlexWrap.Wrap => Microsoft.Maui.Layouts.FlexWrap.Wrap,
        TwFlexWrap.WrapReverse => Microsoft.Maui.Layouts.FlexWrap.Reverse,
        _ => Microsoft.Maui.Layouts.FlexWrap.NoWrap,
    };

    private static object? JustifyOf(bool has, TwValue v) => !has ? null : (TwJustify)(byte)v.X switch
    {
        TwJustify.End => Microsoft.Maui.Layouts.FlexJustify.End,
        TwJustify.Center => Microsoft.Maui.Layouts.FlexJustify.Center,
        TwJustify.Between => Microsoft.Maui.Layouts.FlexJustify.SpaceBetween,
        TwJustify.Around => Microsoft.Maui.Layouts.FlexJustify.SpaceAround,
        TwJustify.Evenly => Microsoft.Maui.Layouts.FlexJustify.SpaceEvenly,
        _ => Microsoft.Maui.Layouts.FlexJustify.Start,
    };

    private static object? AlignItemsOf(bool has, TwValue v) => !has ? null : (TwAlignItems)(byte)v.X switch
    {
        TwAlignItems.Start => Microsoft.Maui.Layouts.FlexAlignItems.Start,
        TwAlignItems.End => Microsoft.Maui.Layouts.FlexAlignItems.End,
        TwAlignItems.Center => Microsoft.Maui.Layouts.FlexAlignItems.Center,
        _ => Microsoft.Maui.Layouts.FlexAlignItems.Stretch,
    };

    private static object? AlignSelfOf(bool has, TwValue v) => !has ? null : (TwAlignSelfFlex)(byte)v.X switch
    {
        TwAlignSelfFlex.Stretch => Microsoft.Maui.Layouts.FlexAlignSelf.Stretch,
        TwAlignSelfFlex.Start => Microsoft.Maui.Layouts.FlexAlignSelf.Start,
        TwAlignSelfFlex.End => Microsoft.Maui.Layouts.FlexAlignSelf.End,
        TwAlignSelfFlex.Center => Microsoft.Maui.Layouts.FlexAlignSelf.Center,
        _ => Microsoft.Maui.Layouts.FlexAlignSelf.Auto,
    };

    private static object? BasisOf(bool has, TwValue v)
    {
        if (!has) return null;
        if (float.IsNaN(v.X)) return Microsoft.Maui.Layouts.FlexBasis.Auto;
        return new Microsoft.Maui.Layouts.FlexBasis(v.X, isRelative: v.Y > 0);
    }

    private static object? ColumnDefsOf(bool has, TwValue v)
    {
        if (!has) return null;
        int count = (int)v.X;
        return new FreshValue(("cols", count), () =>
        {
            var defs = new ColumnDefinitionCollection();
            for (int i = 0; i < count; i++)
                defs.Add(new ColumnDefinition(GridLength.Star));
            return defs;
        });
    }

    private static object? RowDefsOf(bool has, TwValue v)
    {
        if (!has) return null;
        int count = (int)v.X;
        return new FreshValue(("rows", count), () =>
        {
            var defs = new RowDefinitionCollection();
            for (int i = 0; i < count; i++)
                defs.Add(new RowDefinition(GridLength.Star));
            return defs;
        });
    }

    private static FontAttributes? FontAttributesOf(TwDeclaration[] set)
    {
        bool any = false;
        var attrs = FontAttributes.None;
        foreach (var d in set)
        {
            if (d.Property == TwPropertyId.FontWeight)
            {
                any = true;
                if (d.Value.X >= 600) attrs |= FontAttributes.Bold;
            }
            else if (d.Property == TwPropertyId.FontItalic)
            {
                any = true;
                if (d.Value.X > 0) attrs |= FontAttributes.Italic;
            }
        }
        return any ? attrs : null;
    }

    private static double FontSizeIn(TwDeclaration[] set) =>
        TryFind(set, TwPropertyId.FontSize, out var v) ? v.X : 14;

    private static bool TryFind(TwDeclaration[] set, TwPropertyId id, out TwValue value)
    {
        foreach (var d in set)
        {
            if (d.Property == id)
            {
                value = d.Value;
                return true;
            }
        }
        value = default;
        return false;
    }

    private static IEnumerable<TwPropertyId> UnionProperties(TwDeclaration[] a, TwDeclaration[] b)
    {
        var seen = new HashSet<TwPropertyId>();
        foreach (var d in a) seen.Add(d.Property);
        foreach (var d in b) seen.Add(d.Property);
        return seen;
    }

    private static BindableProperty? Prop(Type type, (Type Type, BindableProperty Property)[] table)
    {
        foreach (var (t, p) in table)
            if (t.IsAssignableFrom(type))
                return p;
        return null;
    }

    /// <summary>Pre-boxed common values so cached plans don't re-box on every compile.</summary>
    private static class Boxed
    {
        public static object Double(double v) => v;
        public static object Int(int v) => v;
    }
}

/// <summary>How property changes should animate (transition-* / duration-* / ease-*).</summary>
internal readonly struct TwTransitionSpec(TwTransitionProps props, int durationMs, int delayMs, TwEasing easing)
{
    public readonly TwTransitionProps Props = props;
    public readonly int DurationMs = durationMs;
    public readonly int DelayMs = delayMs;
    public readonly TwEasing Easing = easing;
}

/// <summary>
/// Which framework property carries each neutral property, per control family.
/// First assignable match wins. Extend by prepending more specific types.
/// </summary>
internal static class TwProps
{
    /// <summary>First assignable match in a mapping table, or null.</summary>
    public static BindableProperty? For(Type type, (Type Type, BindableProperty Property)[] table)
    {
        foreach (var (t, p) in table)
            if (t.IsAssignableFrom(type))
                return p;
        return null;
    }

    public static readonly (Type, BindableProperty)[] Background =
    [
        // BoxView draws via Color (CornerRadius clips that layer, not BackgroundColor)
        (typeof(BoxView), BoxView.ColorProperty),
        (typeof(VisualElement), VisualElement.BackgroundColorProperty),
    ];

    public static readonly (Type, BindableProperty)[] TextColor =
    [
        (typeof(Label), Label.TextColorProperty),
        (typeof(Button), Button.TextColorProperty),
        (typeof(InputView), InputView.TextColorProperty),
        (typeof(Picker), Picker.TextColorProperty),
        (typeof(DatePicker), DatePicker.TextColorProperty),
        (typeof(TimePicker), TimePicker.TextColorProperty),
        (typeof(RadioButton), RadioButton.TextColorProperty),
    ];

    public static readonly (Type, BindableProperty)[] FontSize =
    [
        (typeof(Label), Label.FontSizeProperty),
        (typeof(Button), Button.FontSizeProperty),
        (typeof(Entry), Entry.FontSizeProperty),
        (typeof(Editor), Editor.FontSizeProperty),
        (typeof(SearchBar), SearchBar.FontSizeProperty),
        (typeof(Picker), Picker.FontSizeProperty),
        (typeof(DatePicker), DatePicker.FontSizeProperty),
        (typeof(TimePicker), TimePicker.FontSizeProperty),
        (typeof(RadioButton), RadioButton.FontSizeProperty),
    ];

    public static readonly (Type, BindableProperty)[] FontAttributes =
    [
        (typeof(Label), Label.FontAttributesProperty),
        (typeof(Button), Button.FontAttributesProperty),
        (typeof(Entry), Entry.FontAttributesProperty),
        (typeof(Editor), Editor.FontAttributesProperty),
        (typeof(SearchBar), SearchBar.FontAttributesProperty),
        (typeof(Picker), Picker.FontAttributesProperty),
        (typeof(DatePicker), DatePicker.FontAttributesProperty),
        (typeof(TimePicker), TimePicker.FontAttributesProperty),
        (typeof(RadioButton), RadioButton.FontAttributesProperty),
    ];

    public static readonly (Type, BindableProperty)[] CharacterSpacing =
    [
        (typeof(Label), Label.CharacterSpacingProperty),
        (typeof(Button), Button.CharacterSpacingProperty),
        (typeof(Entry), Entry.CharacterSpacingProperty),
        (typeof(Editor), Editor.CharacterSpacingProperty),
        (typeof(SearchBar), SearchBar.CharacterSpacingProperty),
    ];

    public static readonly (Type, BindableProperty)[] HorizontalTextAlignment =
    [
        (typeof(Label), Label.HorizontalTextAlignmentProperty),
        (typeof(Entry), Entry.HorizontalTextAlignmentProperty),
        (typeof(Editor), Editor.HorizontalTextAlignmentProperty),
        (typeof(SearchBar), SearchBar.HorizontalTextAlignmentProperty),
    ];

    public static readonly (Type, BindableProperty)[] LineHeight =
    [
        (typeof(Label), Label.LineHeightProperty),
    ];

    public static readonly (Type, BindableProperty)[] Padding =
    [
        (typeof(Label), Label.PaddingProperty),
        (typeof(Button), Button.PaddingProperty),
        (typeof(ImageButton), ImageButton.PaddingProperty),
        (typeof(Border), Border.PaddingProperty),
        (typeof(Layout), Layout.PaddingProperty),
        (typeof(Page), Page.PaddingProperty),
    ];

    public static readonly (Type, BindableProperty)[] BorderColor =
    [
        (typeof(Button), Button.BorderColorProperty),
        (typeof(ImageButton), ImageButton.BorderColorProperty),
    ];

    public static readonly (Type, BindableProperty)[] BorderWidth =
    [
        (typeof(Button), Button.BorderWidthProperty),
        (typeof(ImageButton), ImageButton.BorderWidthProperty),
    ];

    public static readonly (Type, BindableProperty)[] TextDecorations =
    [
        (typeof(Label), Label.TextDecorationsProperty),
    ];

    public static readonly (Type, BindableProperty)[] TextTransform =
    [
        (typeof(Label), Label.TextTransformProperty),
        (typeof(Button), Button.TextTransformProperty),
        (typeof(Entry), Entry.TextTransformProperty),
        (typeof(Editor), Editor.TextTransformProperty),
    ];
}
