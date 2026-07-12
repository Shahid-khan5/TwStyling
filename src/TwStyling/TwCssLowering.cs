using TwStyling.Css;

namespace TwStyling;

/// <summary>
/// Lowers a resolved CSS declaration onto the framework-neutral <see cref="TwDeclaration"/> IR.
///
/// This replaces per-utility parsing with per-property mapping. The Tailwind catalog grows without
/// bound; the set of CSS properties a native control can consume does not. <c>bg-red-500</c>,
/// <c>bg-[#abc]</c>, <c>bg-red-500/50</c> and <c>bg-(--brand)</c> are four utilities but one
/// declaration: <c>background-color</c>.
/// </summary>
internal static class TwCssLowering
{
    /// <summary>
    /// CSS properties with no native analog. Emitting a diagnostic per property — rather than per
    /// utility — keeps the "never a silent no-op" guarantee against a smaller, stabler list.
    /// </summary>
    private static readonly Dictionary<string, string> WebOnly = new(StringComparer.Ordinal)
    {
        ["float"] = "'float' has no native analog — use layout containers",
        ["clear"] = "'clear' has no native analog",
        ["filter"] = "CSS filters have no native analog",
        ["backdrop-filter"] = "backdrop filters have no native analog",
        ["mix-blend-mode"] = "blend modes have no native analog",
        ["background-blend-mode"] = "blend modes have no native analog",
        ["appearance"] = "'appearance' has no native analog",
        ["cursor"] = "'cursor' has no native analog",
        ["user-select"] = "'user-select' has no native analog",
        ["resize"] = "'resize' has no native analog",
        ["list-style-type"] = "list styles have no native analog",
        ["list-style-position"] = "list styles have no native analog",
        ["border-collapse"] = "table layout has no native analog",
        ["border-spacing"] = "table layout has no native analog",
        ["box-sizing"] = "'box-sizing' has no native analog — native layout is always border-box",
        ["vertical-align"] = "'vertical-align' has no native analog",
        ["text-indent"] = "'text-indent' has no native analog",
        ["position"] = "'position' has no native analog — use layout, margins or translate",
        ["top"] = "inset properties have no native analog — use margins or translate",
        ["right"] = "inset properties have no native analog — use margins or translate",
        ["bottom"] = "inset properties have no native analog — use margins or translate",
        ["left"] = "inset properties have no native analog — use margins or translate",
        ["inset"] = "inset properties have no native analog — use margins or translate",
        ["aspect-ratio"] = "'aspect-ratio' has no native analog",
        ["outline-style"] = "'outline' has no native analog — use a border",
        ["outline-width"] = "'outline' has no native analog — use a border",
        ["outline-color"] = "'outline' has no native analog — use a border",
        ["outline-offset"] = "'outline' has no native analog — use a border",
        ["font-feature-settings"] = "OpenType feature settings have no native analog",
        ["font-variation-settings"] = "variable-font axes have no native analog",
        ["font-variant-numeric"] = "numeric font variants have no native analog",
        ["tab-size"] = "'tab-size' has no native analog",
        ["hyphens"] = "'hyphens' has no native analog",
        ["content"] = "generated content has no native analog",
        ["justify-items"] = "container default child-alignment has no native analog — set items-*/self-*/place-self-* on the children",
        ["place-items"] = "container default child-alignment has no native analog — set items-*/self-*/place-self-* on the children",
        ["background-clip"] = "'background-clip' has no native analog",
        ["background-origin"] = "'background-origin' has no native analog",
        ["background-position"] = "'background-position' has no native analog",
        ["background-repeat"] = "'background-repeat' has no native analog",
        ["background-attachment"] = "'background-attachment' has no native analog",
        ["grid-auto-flow"] = "implicit grid placement has no native analog",
        ["grid-auto-columns"] = "implicit grid tracks have no native analog",
        ["grid-auto-rows"] = "implicit grid tracks have no native analog",
        ["isolation"] = "'isolation' has no native analog",
        ["overscroll-behavior"] = "'overscroll-behavior' has no native analog",
        ["scroll-behavior"] = "'scroll-behavior' has no native analog",
        ["touch-action"] = "'touch-action' has no native analog",
        ["will-change"] = "'will-change' has no native analog",
        ["forced-color-adjust"] = "'forced-color-adjust' has no native analog",
    };

    /// <summary>
    /// Maps one declaration into zero or more <see cref="TwDeclaration"/>s.
    /// Returns false and sets <paramref name="error"/> when the property has no native mapping,
    /// or when its value cannot be resolved.
    /// </summary>
    public static bool TryLower(
        string property, CssValue value, CssEnvironment env,
        List<TwDeclaration> output, out string? error)
    {
        error = null;

        // Internal Tailwind plumbing (`--tw-scale-x`) and author tokens are inputs to var(),
        // already substituted by the evaluator. They are never applied directly.
        if (property.StartsWith("--", StringComparison.Ordinal)) return true;

        // Vendor prefixes carry no information we do not get from the unprefixed property,
        // except for the one that expresses line clamping.
        if (property.StartsWith("-webkit-", StringComparison.Ordinal) && property != "-webkit-line-clamp")
            return true;

        // `truncate` is overflow-hidden + nowrap + text-overflow:ellipsis. The first two lower on
        // their own, but the ellipsis is what makes it a truncation rather than a hard clip — and
        // natively that is a one-line clamp. Anything else (clip) is already covered by overflow.
        if (property == "text-overflow")
        {
            if (value is CssIdent { Name: "ellipsis" })
                return Add(output, TwPropertyId.LineClamp, TwValue.Scalar(1));
            return true;
        }

        // The utility styles its children through a descendant selector (space-x-*, divide-*).
        // Native toolkits have no such thing — the container spaces its children itself.
        if (property == CssStylesheetParser.DescendantSelector)
        {
            error = "styles children through a descendant selector, which native layout has no equivalent for — use gap-* on the container";
            return false;
        }

        if (WebOnly.TryGetValue(property, out var reason)) { error = reason; return false; }

        try
        {
            return Lower(property, value, env, output, ref error);
        }
        catch (CssEvalException ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private static bool Lower(string property, CssValue v, CssEnvironment env, List<TwDeclaration> o, ref string? error)
    {
        switch (property)
        {
            // ------------------------------------------------------------ color
            case "background-color": return Add(o, TwPropertyId.Background, TwValue.Color(Color(v, env)));
            case "color": return Add(o, TwPropertyId.TextColor, TwValue.Color(Color(v, env)));

            case "border-color":
            case "border-top-color":
            case "border-right-color":
            case "border-bottom-color":
            case "border-left-color":
                // TwPropertyId.BorderColor is uniform; a per-side color would need a new id.
                return Add(o, TwPropertyId.BorderColor, TwValue.Color(Color(v, env)));

            // ------------------------------------------------------------ edges
            case "padding": return Add(o, TwPropertyId.Padding, Shorthand(v, env));
            case "padding-top": return Add(o, TwPropertyId.Padding, Side(top: Px(v, env)));
            case "padding-right": return Add(o, TwPropertyId.Padding, Side(right: Px(v, env)));
            case "padding-bottom": return Add(o, TwPropertyId.Padding, Side(bottom: Px(v, env)));
            case "padding-left": return Add(o, TwPropertyId.Padding, Side(left: Px(v, env)));
            case "padding-inline": return Add(o, TwPropertyId.Padding, Side(left: Px(v, env), right: Px(v, env)));
            case "padding-block": return Add(o, TwPropertyId.Padding, Side(top: Px(v, env), bottom: Px(v, env)));
            case "padding-inline-start": return Add(o, TwPropertyId.Padding, Side(left: Px(v, env)));
            case "padding-inline-end": return Add(o, TwPropertyId.Padding, Side(right: Px(v, env)));
            case "padding-block-start": return Add(o, TwPropertyId.Padding, Side(top: Px(v, env)));
            case "padding-block-end": return Add(o, TwPropertyId.Padding, Side(bottom: Px(v, env)));

            case "margin": return Margin(v, env, o, MarginSide.All, ref error);
            case "margin-top": return Margin(v, env, o, MarginSide.Top, ref error);
            case "margin-right": return Margin(v, env, o, MarginSide.Right, ref error);
            case "margin-bottom": return Margin(v, env, o, MarginSide.Bottom, ref error);
            case "margin-left": return Margin(v, env, o, MarginSide.Left, ref error);
            case "margin-inline": return Margin(v, env, o, MarginSide.Inline, ref error);
            case "margin-block": return Margin(v, env, o, MarginSide.Block, ref error);

            case "gap": return Add(o, TwPropertyId.Gap, TwValue.Edges(Px(v, env), Px(v, env), float.NaN, float.NaN));
            case "column-gap": return Add(o, TwPropertyId.Gap, TwValue.Edges(Px(v, env), float.NaN, float.NaN, float.NaN));
            case "row-gap": return Add(o, TwPropertyId.Gap, TwValue.Edges(float.NaN, Px(v, env), float.NaN, float.NaN));

            // ----------------------------------------------------------- border
            case "border-width": return Add(o, TwPropertyId.BorderWidth, Shorthand(v, env));
            case "border-top-width": return Add(o, TwPropertyId.BorderWidth, Side(top: Px(v, env)));
            case "border-right-width": return Add(o, TwPropertyId.BorderWidth, Side(right: Px(v, env)));
            case "border-bottom-width": return Add(o, TwPropertyId.BorderWidth, Side(bottom: Px(v, env)));
            case "border-left-width": return Add(o, TwPropertyId.BorderWidth, Side(left: Px(v, env)));
            case "border-inline-width": return Add(o, TwPropertyId.BorderWidth, Side(left: Px(v, env), right: Px(v, env)));
            case "border-block-width": return Add(o, TwPropertyId.BorderWidth, Side(top: Px(v, env), bottom: Px(v, env)));

            // Tailwind sets `border-*-style: var(--tw-border-style)` alongside every width.
            // Only `solid` is renderable; anything else would silently look wrong.
            case "border-style":
            case "border-top-style":
            case "border-right-style":
            case "border-bottom-style":
            case "border-left-style":
            case "border-inline-style":
            case "border-block-style":
                if (v is CssIdent { Name: "solid" or "none" }) return true;
                error = $"border-style '{v}' has no native analog — only solid strokes render";
                return false;

            case "border-radius": return Add(o, TwPropertyId.CornerRadius, Corners(v, env));
            case "border-top-left-radius": return Add(o, TwPropertyId.CornerRadius, Corner(topLeft: Px(v, env)));
            case "border-top-right-radius": return Add(o, TwPropertyId.CornerRadius, Corner(topRight: Px(v, env)));
            case "border-bottom-left-radius": return Add(o, TwPropertyId.CornerRadius, Corner(bottomLeft: Px(v, env)));
            case "border-bottom-right-radius": return Add(o, TwPropertyId.CornerRadius, Corner(bottomRight: Px(v, env)));

            // ----------------------------------------------------------- sizing
            case "width": return Size(v, env, o, TwPropertyId.Width, ref error);
            case "height": return Size(v, env, o, TwPropertyId.Height, ref error);
            case "min-width": return Size(v, env, o, TwPropertyId.MinWidth, ref error);
            case "min-height": return Size(v, env, o, TwPropertyId.MinHeight, ref error);
            case "max-width": return Size(v, env, o, TwPropertyId.MaxWidth, ref error);
            case "max-height": return Size(v, env, o, TwPropertyId.MaxHeight, ref error);

            // ------------------------------------------------------- typography
            case "font-size": return Add(o, TwPropertyId.FontSize, TwValue.Scalar(Px(v, env)));
            case "font-weight": return Add(o, TwPropertyId.FontWeight, TwValue.Scalar(Number(v)));
            case "font-style":
                return Add(o, TwPropertyId.FontItalic, TwValue.Scalar(Ident(v) == "italic" ? 1 : 0));

            case "font-family": return Add(o, TwPropertyId.FontFamily, TwValue.Enum((byte)FontFamilyOf(v)));

            case "line-height": return LineHeight(v, env, o);

            case "letter-spacing":
                // Tailwind's tracking scale is in em, which is exactly what the IR carries.
                return Add(o, TwPropertyId.CharacterSpacingEm, TwValue.Scalar(Em(v, env)));

            case "text-align": return Enum(property, v, o, TwPropertyId.TextAlign, ref error, TextAlignOf);
            case "text-transform": return Enum(property, v, o, TwPropertyId.TextTransform, ref error, TextTransformOf);
            case "text-decoration-line": return Enum(property, v, o, TwPropertyId.TextDecoration, ref error, TextDecorationOf);

            case "white-space": return Enum(property, v, o, TwPropertyId.LineBreak, ref error, WhiteSpaceOf);
            case "overflow-wrap":
            case "word-break": return Enum(property, v, o, TwPropertyId.LineBreak, ref error, WordBreakOf);

            case "-webkit-line-clamp": return Add(o, TwPropertyId.LineClamp, TwValue.Scalar(Number(v)));

            // ---------------------------------------------------------- effects
            case "opacity": return Add(o, TwPropertyId.Opacity, TwValue.Scalar(Ratio(v)));
            case "box-shadow":
            case "text-shadow": return Shadow(v, env, o, ref error);
            // `z-auto` means "let the platform decide" — the native default, so nothing to set.
            case "z-index":
                return v is CssIdent { Name: "auto" }
                    ? true
                    : Add(o, TwPropertyId.ZIndex, TwValue.Scalar(Number(v)));

            // `visibility: hidden` hides the element but KEEPS its space, which is opacity on native.
            // Mapping it to IsVisible would collapse the layout — that is `display: none`, handled
            // separately below. Getting these two confused silently reflows the page.
            case "visibility":
                return Add(o, TwPropertyId.Opacity, TwValue.Scalar(Ident(v) == "hidden" ? 0 : 1));

            case "overflow":
            case "overflow-x":
            case "overflow-y":
                if (Ident(v) is "hidden" or "clip") return Add(o, TwPropertyId.Clip, TwValue.Scalar(1));
                if (Ident(v) == "visible") return Add(o, TwPropertyId.Clip, TwValue.Scalar(0));
                error = $"overflow '{v}' has no native analog — use a ScrollView";
                return false;

            case "display": return Display(v, o, ref error);
            case "pointer-events":
                return Add(o, TwPropertyId.PointerEventsNone, TwValue.Scalar(Ident(v) == "none" ? 1 : 0));

            case "object-fit": return Enum(property, v, o, TwPropertyId.ObjectFit, ref error, ObjectFitOf);

            // ------------------------------------------------------- transforms
            case "rotate": return Add(o, TwPropertyId.Rotate, TwValue.Scalar(Degrees(v, env)));
            case "scale": return Scale(v, o);
            case "translate": return Translate(v, env, o);
            case "transform-origin": return TransformOrigin(v, env, o, ref error);

            // Tailwind composes `transform` out of the pieces it could not express as their own
            // properties: skews and 3D rotations. `rotate`, `scale` and `translate` arrive as
            // dedicated properties (above) and never come through here, so anything left is
            // something native cannot do.
            case "transform":
            {
                var text = v.ToString();
                if (text.IndexOf("skew", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    error = "skew has no native analog — native transforms are translate, rotate and scale";
                    return false;
                }
                if (text.IndexOf("rotateX", StringComparison.OrdinalIgnoreCase) >= 0
                    || text.IndexOf("rotateY", StringComparison.OrdinalIgnoreCase) >= 0
                    || text.IndexOf("perspective", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    error = "3D transforms have no native analog";
                    return false;
                }
                return true; // an empty composition — the real values came through their own properties
            }

            // ------------------------------------------------------ flex & grid
            case "flex-direction": return Enum(property, v, o, TwPropertyId.FlexDirection, ref error, FlexDirectionOf);
            case "flex-wrap": return Enum(property, v, o, TwPropertyId.FlexWrap, ref error, FlexWrapOf);
            case "flex-grow": return Add(o, TwPropertyId.FlexGrow, TwValue.Scalar(Number(v)));
            case "flex-shrink": return Add(o, TwPropertyId.FlexShrink, TwValue.Scalar(Number(v)));
            case "flex-basis": return FlexBasis(v, env, o, ref error);
            case "flex": return Flex(v, env, o, ref error);
            case "order": return Add(o, TwPropertyId.Order, TwValue.Scalar(Number(v)));

            case "justify-content": return Enum(property, v, o, TwPropertyId.JustifyContent, ref error, JustifyOf);
            case "align-items": return Enum(property, v, o, TwPropertyId.AlignItems, ref error, AlignItemsOf);
            case "align-content": return Enum(property, v, o, TwPropertyId.AlignContent, ref error, AlignContentOf);
            case "align-self": return Enum(property, v, o, TwPropertyId.FlexAlignSelf, ref error, AlignSelfFlexOf);
            // `auto` defers to the container's alignment — the native default, so nothing to set.
            case "justify-self":
                return Ident(v) == "auto" ? true : Enum(property, v, o, TwPropertyId.AlignSelfX, ref error, AlignOf);

            case "place-self":
                return Ident(v) == "auto" ? true : PlaceSelf(v, o, ref error);
            case "place-content": return PlaceContent(v, o, ref error);

            case "grid-template-columns": return Add(o, TwPropertyId.GridColumns, TwValue.Scalar(TrackCount(v, ref error)));
            case "grid-template-rows": return Add(o, TwPropertyId.GridRows, TwValue.Scalar(TrackCount(v, ref error)));
            case "grid-column": return GridPlacement(v, o, TwPropertyId.GridColumn, TwPropertyId.GridColumnSpan, ref error);
            case "grid-row": return GridPlacement(v, o, TwPropertyId.GridRow, TwPropertyId.GridRowSpan, ref error);
            case "grid-column-start": return Add(o, TwPropertyId.GridColumn, TwValue.Scalar(Number(v) - 1));
            case "grid-row-start": return Add(o, TwPropertyId.GridRow, TwValue.Scalar(Number(v) - 1));

            case "grid-column-end":
            case "grid-row-end":
                error = $"'{property}' needs start+end span folding — not yet lowered";
                return false;

            // ------------------------------------------------------ transitions
            case "transition-property": return Add(o, TwPropertyId.TransitionProps, TwValue.Scalar((float)TransitionPropsOf(v)));
            case "transition-duration": return Add(o, TwPropertyId.TransitionDuration, TwValue.Scalar(Milliseconds(v, env)));
            case "transition-delay": return Add(o, TwPropertyId.TransitionDelay, TwValue.Scalar(Milliseconds(v, env)));
            case "transition-timing-function": return Add(o, TwPropertyId.TransitionEasing, TwValue.Enum((byte)EasingOf(v)));
            case "animation": return Animation(v, o, ref error);

            case "background-image": return Gradient(v, env, o, ref error);

            default:
                error = $"'{property}' has no native mapping";
                return false;
        }
    }

    // ------------------------------------------------------------------ helpers

    private static bool Add(List<TwDeclaration> o, TwPropertyId id, TwValue value)
    {
        o.Add(new TwDeclaration(id, value));
        return true;
    }

    /// <summary>
    /// The CSS property is named in the failure on purpose: "'capitalize' is not a supported value"
    /// leaves the reader guessing which property rejected it.
    /// </summary>
    private static bool Enum<T>(string property, CssValue v, List<TwDeclaration> o, TwPropertyId id, ref string? error, Func<string, T?> map)
        where T : struct, Enum
    {
        var name = Ident(v);
        if (map(name) is { } result) return Add(o, id, TwValue.Enum(Convert.ToByte(result)));
        error = $"{property}: '{name}' has no native equivalent";
        return false;
    }

    private static string Ident(CssValue v) => v switch
    {
        CssIdent i => i.Name.ToLowerInvariant(),
        CssList l when l.Items.Count > 0 => Ident(l.Items[0]),
        _ => v.ToString(),
    };

    private static float Number(CssValue v) =>
        v is CssNumber n ? (float)n.Value : throw new CssEvalException($"'{v}' is not a number");

    /// <summary>A 0..1 ratio; CSS allows either <c>0.5</c> or <c>50%</c>.</summary>
    private static float Ratio(CssValue v) => v switch
    {
        CssNumber { Unit: CssUnit.Percent } p => (float)(p.Value / 100.0),
        CssNumber n => (float)n.Value,
        _ => throw new CssEvalException($"'{v}' is not a number"),
    };

    /// <summary>
    /// The "pill" radius. Tailwind expresses <c>rounded-full</c> as <c>calc(infinity * 1px)</c>,
    /// which is exact in CSS but not a number a native renderer can take — MAUI's Button.CornerRadius
    /// is an <c>int</c>. Collapse it to the same large finite radius the class-name parser used, which
    /// exceeds any realistic control size and so renders as a pill.
    /// </summary>
    private const float PillRadius = 9999f;

    private static float Px(CssValue v, CssEnvironment env)
    {
        if (v is not CssNumber n) throw new CssEvalException($"'{v}' is not a length");

        double px = CssEvaluator.ToPixels(n, env);
        if (double.IsPositiveInfinity(px)) return PillRadius;
        if (double.IsNegativeInfinity(px)) return -PillRadius;
        return (float)px;
    }

    private static float Em(CssValue v, CssEnvironment env) => v switch
    {
        CssNumber { Unit: CssUnit.Em } e => (float)e.Value,
        CssNumber { Unit: CssUnit.None } n => (float)n.Value,
        CssNumber n => (float)(CssEvaluator.ToPixels(n, env) / env.EmBase),
        _ => throw new CssEvalException($"'{v}' is not an em length"),
    };

    private static float Degrees(CssValue v, CssEnvironment env) => v switch
    {
        CssNumber { Unit: CssUnit.Deg } d => (float)d.Value,
        CssNumber { Unit: CssUnit.Turn } t => (float)(t.Value * 360),
        CssNumber { Unit: CssUnit.Rad } r => (float)(r.Value * 180 / Math.PI),
        CssNumber n => (float)n.Value,
        _ => throw new CssEvalException($"'{v}' is not an angle"),
    };

    private static float Milliseconds(CssValue v, CssEnvironment env) => v switch
    {
        CssNumber { Unit: CssUnit.S } s => (float)(s.Value * 1000),
        CssNumber n => (float)n.Value,
        _ => throw new CssEvalException($"'{v}' is not a duration"),
    };

    private static uint Color(CssValue v, CssEnvironment env) => CssEvaluator.ToColor(v, env).ToRgba();

    private static TwValue Side(float left = float.NaN, float top = float.NaN, float right = float.NaN, float bottom = float.NaN) =>
        TwValue.Edges(left, top, right, bottom);

    private static TwValue Corner(float topLeft = float.NaN, float topRight = float.NaN, float bottomLeft = float.NaN, float bottomRight = float.NaN) =>
        TwValue.Corners(topLeft, topRight, bottomLeft, bottomRight);

    /// <summary>CSS 1-to-4-value box shorthand: all / (block inline) / (top inline bottom) / (t r b l).</summary>
    private static TwValue Shorthand(CssValue v, CssEnvironment env)
    {
        var parts = v is CssList list ? list.Items : new[] { v };
        float P(int i) => Px(parts[i], env);

        return parts.Count switch
        {
            1 => TwValue.Edges(P(0), P(0), P(0), P(0)),
            2 => TwValue.Edges(P(1), P(0), P(1), P(0)),
            3 => TwValue.Edges(P(1), P(0), P(1), P(2)),
            _ => TwValue.Edges(P(3), P(0), P(1), P(2)),
        };
    }

    /// <summary>CSS radius shorthand order is top-left, top-right, bottom-right, bottom-left.</summary>
    private static TwValue Corners(CssValue v, CssEnvironment env)
    {
        var parts = v is CssList list ? list.Items : new[] { v };
        float P(int i) => Px(parts[i], env);

        return parts.Count switch
        {
            1 => TwValue.Corners(P(0), P(0), P(0), P(0)),
            2 => TwValue.Corners(P(0), P(1), P(1), P(0)),
            3 => TwValue.Corners(P(0), P(1), P(1), P(2)),
            _ => TwValue.Corners(P(0), P(1), P(3), P(2)),
        };
    }

    private enum MarginSide { All, Top, Right, Bottom, Left, Inline, Block }

    /// <summary>
    /// <c>auto</c> margins are Tailwind's alignment idiom, not a length. `mx-auto` centers;
    /// `ml-auto` pushes the element to the end; `mr-auto` pins it to the start.
    /// </summary>
    private static bool Margin(CssValue v, CssEnvironment env, List<TwDeclaration> o, MarginSide side, ref string? error)
    {
        if (v is CssIdent { Name: "auto" })
        {
            switch (side)
            {
                case MarginSide.Inline: return Add(o, TwPropertyId.AlignSelfX, TwValue.Enum((byte)TwAlign.Center));
                case MarginSide.Block: return Add(o, TwPropertyId.AlignSelfY, TwValue.Enum((byte)TwAlign.Center));
                case MarginSide.Left: return Add(o, TwPropertyId.AlignSelfX, TwValue.Enum((byte)TwAlign.End));
                case MarginSide.Right: return Add(o, TwPropertyId.AlignSelfX, TwValue.Enum((byte)TwAlign.Start));
                case MarginSide.Top: return Add(o, TwPropertyId.AlignSelfY, TwValue.Enum((byte)TwAlign.End));
                case MarginSide.Bottom: return Add(o, TwPropertyId.AlignSelfY, TwValue.Enum((byte)TwAlign.Start));
                case MarginSide.All:
                    Add(o, TwPropertyId.AlignSelfX, TwValue.Enum((byte)TwAlign.Center));
                    return Add(o, TwPropertyId.AlignSelfY, TwValue.Enum((byte)TwAlign.Center));
            }
        }

        float px = Px(v, env);
        return side switch
        {
            MarginSide.All => Add(o, TwPropertyId.Margin, Shorthand(v, env)),
            MarginSide.Top => Add(o, TwPropertyId.Margin, Side(top: px)),
            MarginSide.Right => Add(o, TwPropertyId.Margin, Side(right: px)),
            MarginSide.Bottom => Add(o, TwPropertyId.Margin, Side(bottom: px)),
            MarginSide.Left => Add(o, TwPropertyId.Margin, Side(left: px)),
            MarginSide.Inline => Add(o, TwPropertyId.Margin, Side(left: px, right: px)),
            _ => Add(o, TwPropertyId.Margin, Side(top: px, bottom: px)),
        };
    }

    private static bool Size(CssValue v, CssEnvironment env, List<TwDeclaration> o, TwPropertyId id, ref string? error)
    {
        switch (v)
        {
            case CssIdent { Name: "auto" }:
                return true; // native default sizing
            case CssNumber { Unit: CssUnit.Percent } p when Math.Abs(p.Value - 100) < 0.001:
                return Add(o, id, TwValue.Scalar(TwValue.Full));

            // A percentage of the parent has no native property; the native way to say "half the
            // row" is a flex basis or a grid column.
            case CssNumber { Unit: CssUnit.Percent }:
                error = $"fractional size '{v}' has no native analog — use basis-1/2 (in a flex row) or a grid column";
                return false;

            // Viewport units: the window, not the parent. w-full is the native intent in practice.
            case CssNumber { Unit: CssUnit.Vw or CssUnit.Vh }:
                error = $"viewport size '{v}' has no native analog — use w-full / h-full to fill the parent";
                return false;

            case CssIdent ident:
                error = $"size '{ident.Name}' has no native analog";
                return false;
            default:
                return Add(o, id, TwValue.Scalar(Px(v, env)));
        }
    }

    /// <summary>Unitless line-height is a multiplier; a length must be divided by the font size later.</summary>
    private static bool LineHeight(CssValue v, CssEnvironment env, List<TwDeclaration> o) => v switch
    {
        CssNumber { Unit: CssUnit.None } n => Add(o, TwPropertyId.LineHeight, TwValue.Scalar((float)n.Value)),
        _ => Add(o, TwPropertyId.LineHeight, TwValue.AbsoluteLength(Px(v, env))),
    };

    private static bool Display(CssValue v, List<TwDeclaration> o, ref string? error)
    {
        switch (Ident(v))
        {
            case "none": return Add(o, TwPropertyId.Visible, TwValue.Scalar(0));
            case "flex": return Add(o, TwPropertyId.FlexDirection, TwValue.Enum((byte)TwFlexDirection.Row));
            case "grid": return true; // grid-cols-* / grid-rows-* carry the actual configuration
            case "-webkit-box": return true; // emitted alongside line-clamp
            case "contents":
            case "inline": return true;
            default:
                error = $"display '{Ident(v)}' has no native analog";
                return false;
        }
    }

    /// <summary>Tailwind emits <c>scale: x y</c>; equal components collapse to the uniform Scale property.</summary>
    private static bool Scale(CssValue v, List<TwDeclaration> o)
    {
        var parts = v is CssList list ? list.Items : new[] { v };
        float x = Ratio(parts[0]);
        float y = parts.Count > 1 ? Ratio(parts[1]) : x;

        if (Math.Abs(x - y) < 0.0001f) return Add(o, TwPropertyId.Scale, TwValue.Scalar(x));
        Add(o, TwPropertyId.ScaleX, TwValue.Scalar(x));
        return Add(o, TwPropertyId.ScaleY, TwValue.Scalar(y));
    }

    private static bool Translate(CssValue v, CssEnvironment env, List<TwDeclaration> o)
    {
        var parts = v is CssList list ? list.Items : new[] { v };
        Add(o, TwPropertyId.TranslateX, TwValue.Scalar(Px(parts[0], env)));
        return Add(o, TwPropertyId.TranslateY, TwValue.Scalar(parts.Count > 1 ? Px(parts[1], env) : 0));
    }

    private static bool TransformOrigin(CssValue v, CssEnvironment env, List<TwDeclaration> o, ref string? error)
    {
        var parts = v is CssList list ? list.Items : new[] { v };

        float? x = null, y = null;
        foreach (var part in parts)
        {
            switch (part)
            {
                case CssIdent { Name: "left" }: x = 0; break;
                case CssIdent { Name: "right" }: x = 1; break;
                case CssIdent { Name: "top" }: y = 0; break;
                case CssIdent { Name: "bottom" }: y = 1; break;
                case CssIdent { Name: "center" }: if (x is null) x = 0.5f; else y = 0.5f; break;
                case CssNumber { Unit: CssUnit.Percent } p:
                    if (x is null) x = (float)(p.Value / 100); else y = (float)(p.Value / 100);
                    break;

                // Tailwind writes the corners numerically — `origin-top-left` is `0 0`, not
                // `top left`. A bare 0 is the leading edge; any other length would need the
                // element's size, which we do not have at compile time.
                case CssNumber { Value: 0 }:
                    if (x is null) x = 0f; else y = 0f;
                    break;

                default:
                    error = $"transform-origin '{part}' has no native analog — use origin-{{top,right,bottom,left,center}}";
                    return false;
            }
        }

        Add(o, TwPropertyId.TransformOriginX, TwValue.Scalar(x ?? 0.5f));
        return Add(o, TwPropertyId.TransformOriginY, TwValue.Scalar(y ?? 0.5f));
    }

    /// <summary>
    /// <c>basis-1/2</c> compiles to <c>flex-basis: 50%</c>. The IR tags a relative fraction with
    /// Y = 1 (the same encoding <see cref="TwValue.AbsoluteLength"/> uses) so the adapter knows to
    /// treat X as a proportion rather than a device-independent length.
    /// </summary>
    private static bool FlexBasis(CssValue v, CssEnvironment env, List<TwDeclaration> o, ref string? error) => v switch
    {
        CssIdent { Name: "auto" } => Add(o, TwPropertyId.FlexBasis, TwValue.Scalar(float.NaN)),
        CssNumber { Unit: CssUnit.Percent } p => Add(o, TwPropertyId.FlexBasis, TwValue.AbsoluteLength((float)(p.Value / 100.0))),
        _ => Add(o, TwPropertyId.FlexBasis, TwValue.Scalar(Px(v, env))),
    };

    private static bool Flex(CssValue v, CssEnvironment env, List<TwDeclaration> o, ref string? error)
    {
        // `flex: 1 1 0%`, `flex: none`, `flex: auto`, `flex: 1`
        if (v is CssIdent { Name: "none" })
        {
            Add(o, TwPropertyId.FlexGrow, TwValue.Scalar(0));
            return Add(o, TwPropertyId.FlexShrink, TwValue.Scalar(0));
        }

        var parts = v is CssList list ? list.Items : new[] { v };
        if (parts[0] is CssNumber grow) Add(o, TwPropertyId.FlexGrow, TwValue.Scalar((float)grow.Value));

        // The one-value form `flex: 1` is shorthand for `1 1 0%` — Tailwind emits exactly that for
        // flex-1, so reading only the grow factor would silently drop the shrink and basis.
        if (parts.Count == 1)
        {
            Add(o, TwPropertyId.FlexShrink, TwValue.Scalar(1));
            return Add(o, TwPropertyId.FlexBasis, TwValue.AbsoluteLength(0));
        }

        if (parts.Count > 1 && parts[1] is CssNumber shrink) Add(o, TwPropertyId.FlexShrink, TwValue.Scalar((float)shrink.Value));
        if (parts.Count > 2) return FlexBasis(parts[2], env, o, ref error);
        return true;
    }

    private static bool PlaceSelf(CssValue v, List<TwDeclaration> o, ref string? error)
    {
        if (AlignOf(Ident(v)) is not { } align) { error = $"place-self '{Ident(v)}' is not supported"; return false; }
        Add(o, TwPropertyId.AlignSelfX, TwValue.Enum((byte)align));
        return Add(o, TwPropertyId.AlignSelfY, TwValue.Enum((byte)align));
    }

    private static bool PlaceContent(CssValue v, List<TwDeclaration> o, ref string? error)
    {
        var name = Ident(v);
        if (AlignContentOf(name) is not { } alignContent) { error = $"place-content '{name}' is not supported"; return false; }
        Add(o, TwPropertyId.AlignContent, TwValue.Enum((byte)alignContent));

        // TwJustify has no `stretch`, so a stretch place-content only sets align-content.
        if (JustifyOf(name) is { } justify) Add(o, TwPropertyId.JustifyContent, TwValue.Enum((byte)justify));
        return true;
    }

    /// <summary><c>repeat(3, minmax(0, 1fr))</c> → 3 star tracks.</summary>
    private static float TrackCount(CssValue v, ref string? error)
    {
        if (v is CssFunction { Name: "repeat" } repeat && repeat.Args.Count > 0 && repeat.Args[0] is CssNumber n)
            return (float)n.Value;
        if (v is CssList list) return list.Items.Count;
        throw new CssEvalException($"'{v}' is not a track list we can map");
    }

    /// <summary><c>span 2 / span 2</c> → a span; <c>3</c> → a 1-based start.</summary>
    private static bool GridPlacement(CssValue v, List<TwDeclaration> o, TwPropertyId startId, TwPropertyId spanId, ref string? error)
    {
        var parts = v is CssList list ? list.Items : new[] { v };

        for (int i = 0; i < parts.Count; i++)
        {
            if (parts[i] is CssIdent { Name: "span" } && i + 1 < parts.Count && parts[i + 1] is CssNumber span)
                return Add(o, spanId, TwValue.Scalar((float)span.Value));
        }

        if (parts[0] is CssNumber start) return Add(o, startId, TwValue.Scalar((float)start.Value - 1));

        error = $"grid placement '{v}' is not supported";
        return false;
    }

    /// <summary>
    /// Tailwind composes `box-shadow` from five slots — inset, inset-ring, ring-offset, ring, shadow —
    /// and fills the unused ones with the fully-transparent placeholder `0 0 #0000`. It also nests a
    /// comma list inside a slot (`--tw-shadow` is itself two shadows). So: flatten, drop the
    /// transparent placeholders, and take the first real layer. Native carries a single shadow.
    /// </summary>
    private static bool Shadow(CssValue v, CssEnvironment env, List<TwDeclaration> o, ref string? error)
    {
        if (v is CssIdent { Name: "none" }) return Add(o, TwPropertyId.Shadow, TwValue.Shadow(0, 0, 0, 0));

        var layers = new List<CssValue>(4);
        Flatten(v, layers);

        foreach (var layer in layers)
        {
            var parts = layer is CssList list ? list.Items : new[] { layer };

            var lengths = new List<float>(4);
            uint rgba = 0xFF000000;

            foreach (var part in parts)
            {
                if (part is CssNumber n) lengths.Add((float)CssEvaluator.ToPixels(n, env));
                else if (part is CssColor c) rgba = c.ToRgba();
                else if (part is CssIdent { Name: "inset" }) { error = "inset shadows have no native analog"; return false; }
            }

            if (lengths.Count < 2) continue;
            if ((rgba >> 24) == 0) continue; // a transparent placeholder slot

            float blur = lengths.Count > 2 ? lengths[2] : 0;
            return Add(o, TwPropertyId.Shadow, TwValue.Shadow(rgba, lengths[0], lengths[1], blur));
        }

        // Every slot was a placeholder: the utility genuinely means "no shadow".
        return Add(o, TwPropertyId.Shadow, TwValue.Shadow(0, 0, 0, 0));

        static void Flatten(CssValue value, List<CssValue> into)
        {
            if (value is CssCommaList commas)
                foreach (var item in commas.Items) Flatten(item, into);
            else
                into.Add(value);
        }
    }

    /// <summary>
    /// Lowers <c>linear-gradient(&lt;direction&gt;, &lt;stop&gt;, …)</c>. After substitution the whole thing
    /// arrives as a single argument holding a comma list — Tailwind writes
    /// <c>linear-gradient(var(--tw-gradient-stops))</c> and the stops expand into it — so unwrap that
    /// before reading positional arguments.
    /// </summary>
    private static bool Gradient(CssValue v, CssEnvironment env, List<TwDeclaration> o, ref string? error)
    {
        if (v is CssIdent { Name: "none" }) return true;

        if (v is not CssFunction { Name: "linear-gradient" } fn)
        {
            error = $"only linear gradients have a native analog, not '{v}'";
            return false;
        }

        var args = fn.Args.Count == 1 && fn.Args[0] is CssCommaList expanded
            ? expanded.Items
            : fn.Args;

        if (args.Count < 3)
        {
            error = "a gradient needs a direction and at least two colour stops";
            return false;
        }

        if (DirectionOf(args[0]) is not { } direction)
        {
            error = $"gradient direction '{args[0]}' has no native analog — use bg-linear-to-{{t,tr,r,br,b,bl,l,tl}}";
            return false;
        }
        Add(o, TwPropertyId.GradientDirection, TwValue.Enum((byte)direction));

        // Two stops are from/to; three insert a via. Each stop is `<color> <position>`.
        var stops = new List<uint>(3);
        for (int i = 1; i < args.Count; i++)
            stops.Add(CssEvaluator.ToColor(args[i] is CssList stop ? stop.Items[0] : args[i], env).ToRgba());

        Add(o, TwPropertyId.GradientFrom, TwValue.Color(stops[0]));
        if (stops.Count > 2) Add(o, TwPropertyId.GradientVia, TwValue.Color(stops[1]));
        return Add(o, TwPropertyId.GradientTo, TwValue.Color(stops[stops.Count - 1]));
    }

    /// <summary>`to bottom right in oklab` → BottomRight. The interpolation space is not renderable.</summary>
    private static TwGradientDirection? DirectionOf(CssValue value)
    {
        var tokens = value is CssList list ? list.Items : new[] { value };

        bool top = false, bottom = false, left = false, right = false;
        foreach (var token in tokens)
        {
            if (token is not CssIdent ident) continue;
            switch (ident.Name.ToLowerInvariant())
            {
                case "top": top = true; break;
                case "bottom": bottom = true; break;
                case "left": left = true; break;
                case "right": right = true; break;
                case "to": break;
                case "in": return Compose(top, bottom, left, right); // `in <space>` ends the direction
            }
        }
        return Compose(top, bottom, left, right);

        static TwGradientDirection? Compose(bool top, bool bottom, bool left, bool right) =>
            (top, bottom, left, right) switch
            {
                (true, false, false, false) => TwGradientDirection.Top,
                (true, false, false, true) => TwGradientDirection.TopRight,
                (false, false, false, true) => TwGradientDirection.Right,
                (false, true, false, true) => TwGradientDirection.BottomRight,
                (false, true, false, false) => TwGradientDirection.Bottom,
                (false, true, true, false) => TwGradientDirection.BottomLeft,
                (false, false, true, false) => TwGradientDirection.Left,
                (true, false, true, false) => TwGradientDirection.TopLeft,
                _ => null,
            };
    }

    private static bool Animation(CssValue v, List<TwDeclaration> o, ref string? error)
    {
        var name = v is CssList list && list.Items.Count > 0 ? Ident(list.Items[0]) : Ident(v);
        TwKeyframes? kind = name switch
        {
            "spin" => TwKeyframes.Spin,
            "pulse" => TwKeyframes.Pulse,
            "bounce" => TwKeyframes.Bounce,
            "none" => TwKeyframes.None,
            _ => null,
        };
        if (kind is null) { error = $"animation '{name}' is not a built-in keyframe set"; return false; }
        return Add(o, TwPropertyId.Keyframes, TwValue.Enum((byte)kind.Value));
    }

    private static TwTransitionProps TransitionPropsOf(CssValue v)
    {
        var items = v is CssCommaList commas ? commas.Items : new[] { v };
        var flags = TwTransitionProps.None;

        foreach (var item in items)
        {
            switch (Ident(item))
            {
                case "all": return TwTransitionProps.All;
                case "none": return TwTransitionProps.None;
                case "color":
                case "background-color":
                case "border-color":
                case "text-decoration-color":
                case "fill":
                case "stroke": flags |= TwTransitionProps.Colors; break;
                case "opacity": flags |= TwTransitionProps.Opacity; break;
                case "transform":
                case "translate":
                case "scale":
                case "rotate": flags |= TwTransitionProps.Transform; break;
                case "width":
                case "height":
                case "font-size": flags |= TwTransitionProps.Sizes; break;
            }
        }
        return flags;
    }

    /// <summary>Recognizes Tailwind's four easing curves by their cubic-bezier control points.</summary>
    private static TwEasing EasingOf(CssValue v)
    {
        if (v is CssIdent { Name: "linear" }) return TwEasing.Linear;
        if (v is not CssFunction { Name: "cubic-bezier" } fn || fn.Args.Count != 4) return TwEasing.InOut;

        float x1 = fn.Args[0] is CssNumber a ? (float)a.Value : 0;
        float x2 = fn.Args[2] is CssNumber c ? (float)c.Value : 0;

        bool easesIn = x1 > 0.001f;
        bool easesOut = x2 < 0.999f;

        if (easesIn && easesOut) return TwEasing.InOut;
        if (easesIn) return TwEasing.In;
        if (easesOut) return TwEasing.Out;
        return TwEasing.Linear;
    }

    private static TwFontFamily FontFamilyOf(CssValue v)
    {
        var text = v.ToString().ToLowerInvariant();
        if (text.Contains("monospace") || text.Contains("ui-monospace")) return TwFontFamily.Mono;
        if (text.Contains("serif") && !text.Contains("sans-serif")) return TwFontFamily.Serif;
        return TwFontFamily.Sans;
    }

    // ---------------------------------------------------------- enum mappings

    private static TwTextAlign? TextAlignOf(string s) => s switch
    {
        "left" or "start" => TwTextAlign.Start,
        "center" => TwTextAlign.Center,
        "right" or "end" => TwTextAlign.End,
        "justify" => TwTextAlign.Justify,
        _ => null,
    };

    private static TwTextTransform? TextTransformOf(string s) => s switch
    {
        "uppercase" => TwTextTransform.Uppercase,
        "lowercase" => TwTextTransform.Lowercase,
        "none" => TwTextTransform.None,
        _ => null,
    };

    private static TwTextDecoration? TextDecorationOf(string s) => s switch
    {
        "underline" => TwTextDecoration.Underline,
        "line-through" => TwTextDecoration.Strikethrough,
        "none" => TwTextDecoration.None,
        _ => null,
    };

    private static TwLineBreak? WhiteSpaceOf(string s) => s switch
    {
        "nowrap" => TwLineBreak.NoWrap,
        "normal" => TwLineBreak.WordWrap,
        _ => null,
    };

    private static TwLineBreak? WordBreakOf(string s) => s switch
    {
        "break-all" or "anywhere" => TwLineBreak.CharacterWrap,
        "break-word" or "normal" => TwLineBreak.WordWrap,
        _ => null,
    };

    private static TwObjectFit? ObjectFitOf(string s) => s switch
    {
        "contain" => TwObjectFit.Contain,
        "cover" => TwObjectFit.Cover,
        "fill" => TwObjectFit.Fill,
        "none" => TwObjectFit.None,
        "scale-down" => TwObjectFit.ScaleDown,
        _ => null,
    };

    private static TwFlexDirection? FlexDirectionOf(string s) => s switch
    {
        "row" => TwFlexDirection.Row,
        "row-reverse" => TwFlexDirection.RowReverse,
        "column" => TwFlexDirection.Column,
        "column-reverse" => TwFlexDirection.ColumnReverse,
        _ => null,
    };

    private static TwFlexWrap? FlexWrapOf(string s) => s switch
    {
        "nowrap" => TwFlexWrap.NoWrap,
        "wrap" => TwFlexWrap.Wrap,
        "wrap-reverse" => TwFlexWrap.WrapReverse,
        _ => null,
    };

    private static TwJustify? JustifyOf(string s) => s switch
    {
        "flex-start" or "start" or "normal" => TwJustify.Start,
        "flex-end" or "end" => TwJustify.End,
        "center" => TwJustify.Center,
        "space-between" => TwJustify.Between,
        "space-around" => TwJustify.Around,
        "space-evenly" => TwJustify.Evenly,
        _ => null,
    };

    private static TwAlignItems? AlignItemsOf(string s) => s switch
    {
        "stretch" or "normal" => TwAlignItems.Stretch,
        "flex-start" or "start" => TwAlignItems.Start,
        "flex-end" or "end" => TwAlignItems.End,
        "center" => TwAlignItems.Center,
        _ => null,
    };

    private static TwAlignContent? AlignContentOf(string s) => s switch
    {
        "flex-start" or "start" => TwAlignContent.Start,
        "flex-end" or "end" => TwAlignContent.End,
        "center" => TwAlignContent.Center,
        "space-between" => TwAlignContent.Between,
        "space-around" => TwAlignContent.Around,
        "space-evenly" => TwAlignContent.Evenly,
        "stretch" or "normal" => TwAlignContent.Stretch,
        _ => null,
    };

    private static TwAlignSelfFlex? AlignSelfFlexOf(string s) => s switch
    {
        "auto" => TwAlignSelfFlex.Auto,
        "stretch" => TwAlignSelfFlex.Stretch,
        "flex-start" or "start" => TwAlignSelfFlex.Start,
        "flex-end" or "end" => TwAlignSelfFlex.End,
        "center" => TwAlignSelfFlex.Center,
        _ => null,
    };

    private static TwAlign? AlignOf(string s) => s switch
    {
        "start" or "flex-start" => TwAlign.Start,
        "center" => TwAlign.Center,
        "end" or "flex-end" => TwAlign.End,
        "stretch" => TwAlign.Stretch,
        _ => null,
    };
}
