using System.Globalization;

namespace Tw.Core;

/// <summary>A single utility with its variant qualification, before plan bucketing.</summary>
internal readonly struct ParsedItem(TwVariantSet variants, TwDeclaration declaration)
{
    public readonly TwVariantSet Variants = variants;
    public readonly TwDeclaration Declaration = declaration;
}

/// <summary>
/// Parses a class string into qualified declarations. This is the cold path —
/// it runs once per unique class string, then the compiled plan is cached.
/// </summary>
internal static class TwParser
{
    public static List<ParsedItem> Parse(string classes, Action<TwDiagnostic>? diagnostic)
    {
        var items = new List<ParsedItem>();
        var scratch = new List<TwDeclaration>(4);
        ReadOnlySpan<char> input = classes;

        int pos = 0;
        while (pos < input.Length)
        {
            while (pos < input.Length && char.IsWhiteSpace(input[pos])) pos++;
            if (pos >= input.Length) break;

            int start = pos;
            while (pos < input.Length && !char.IsWhiteSpace(input[pos])) pos++;
            var token = input[start..pos];

            ParseToken(token, classes, items, scratch, diagnostic);
        }

        return items;
    }

    private static void ParseToken(
        ReadOnlySpan<char> token,
        string classString,
        List<ParsedItem> items,
        List<TwDeclaration> scratch,
        Action<TwDiagnostic>? diagnostic)
    {
        var variants = TwVariantSet.Default;
        var rest = token;

        // Peel variant prefixes: "ios:dark:pressed:bg-blue-700".
        // A ':' inside "[...]" (arbitrary value) is not a variant separator.
        while (true)
        {
            int colon = IndexOfOutsideBrackets(rest, ':');
            if (colon < 0) break;

            var prefix = rest[..colon];
            if (TwTables.Variants.TryGetValue(prefix.ToString(), out var kind))
            {
                variants = kind.Class switch
                {
                    TwVariantClass.Platform => variants.With(kind.Platforms),
                    TwVariantClass.Idiom => variants.With(kind.Idioms),
                    TwVariantClass.Theme => variants.With(kind.Theme),
                    TwVariantClass.Breakpoint => variants.WithBreakpoint(kind.BreakpointMinWidth),
                    _ => variants.With(kind.State),
                };
                rest = rest[(colon + 1)..];
            }
            else
            {
                diagnostic?.Invoke(new TwDiagnostic(classString, token.ToString(),
                    $"unknown variant '{prefix.ToString()}:'"));
                return;
            }
        }

        bool negative = false;
        if (rest.Length > 1 && rest[0] == '-')
        {
            negative = true;
            rest = rest[1..];
        }

        scratch.Clear();
        if (TryResolveUtility(rest, negative, scratch, out string? error))
        {
            foreach (var decl in scratch)
                items.Add(new ParsedItem(variants, decl));
        }
        else
        {
            diagnostic?.Invoke(new TwDiagnostic(classString, token.ToString(),
                error ?? "unknown utility"));
        }
    }

    private static int IndexOfOutsideBrackets(ReadOnlySpan<char> s, char c)
    {
        int depth = 0;
        for (int i = 0; i < s.Length; i++)
        {
            if (s[i] == '[') depth++;
            else if (s[i] == ']') depth--;
            else if (s[i] == c && depth == 0) return i;
        }
        return -1;
    }

    // ---------------------------------------------------------------- utilities

    private static bool TryResolveUtility(
        ReadOnlySpan<char> u, bool negative, List<TwDeclaration> output, out string? error)
    {
        error = null;

        // Bare keyword utilities first.
        switch (u)
        {
            case "italic":
                output.Add(new(TwPropertyId.FontItalic, TwValue.Scalar(1)));
                return true;
            case "not-italic":
                output.Add(new(TwPropertyId.FontItalic, TwValue.Scalar(0)));
                return true;
            case "border":
                output.Add(new(TwPropertyId.BorderWidth, TwValue.Scalar(1)));
                return true;
            case "rounded":
                output.Add(new(TwPropertyId.CornerRadius, UniformCorners(TwTables.Radii[""])));
                return true;
            case "shadow":
                output.Add(new(TwPropertyId.Shadow, TwTables.Shadows[""]));
                return true;
            case "w-full":
                output.Add(new(TwPropertyId.Width, TwValue.Scalar(TwValue.Full)));
                return true;
            case "h-full":
                output.Add(new(TwPropertyId.Height, TwValue.Scalar(TwValue.Full)));
                return true;
            case "underline":
                output.Add(new(TwPropertyId.TextDecoration, TwValue.Enum((byte)TwTextDecoration.Underline)));
                return true;
            case "line-through":
                output.Add(new(TwPropertyId.TextDecoration, TwValue.Enum((byte)TwTextDecoration.Strikethrough)));
                return true;
            case "no-underline":
                output.Add(new(TwPropertyId.TextDecoration, TwValue.Enum((byte)TwTextDecoration.None)));
                return true;
            case "uppercase":
                output.Add(new(TwPropertyId.TextTransform, TwValue.Enum((byte)TwTextTransform.Uppercase)));
                return true;
            case "lowercase":
                output.Add(new(TwPropertyId.TextTransform, TwValue.Enum((byte)TwTextTransform.Lowercase)));
                return true;
            case "normal-case":
                output.Add(new(TwPropertyId.TextTransform, TwValue.Enum((byte)TwTextTransform.None)));
                return true;
            case "truncate":
                output.Add(new(TwPropertyId.LineClamp, TwValue.Scalar(1)));
                return true;
            case "hidden":
                output.Add(new(TwPropertyId.Visible, TwValue.Scalar(0)));
                return true;
            case "visible":
                output.Add(new(TwPropertyId.Visible, TwValue.Scalar(1)));
                return true;
            case "invisible": // CSS visibility:hidden keeps layout space → opacity 0
                output.Add(new(TwPropertyId.Opacity, TwValue.Scalar(0)));
                return true;
            case "overflow-hidden":
                output.Add(new(TwPropertyId.Clip, TwValue.Scalar(1)));
                return true;
            case "overflow-visible":
                output.Add(new(TwPropertyId.Clip, TwValue.Scalar(0)));
                return true;
            case "grid":
                return true; // structural marker — the Grid element itself is the grid
            case "flex":
                output.Add(new(TwPropertyId.FlexDirection, TwValue.Enum((byte)TwFlexDirection.Row)));
                return true;
            case "flex-row":
                output.Add(new(TwPropertyId.FlexDirection, TwValue.Enum((byte)TwFlexDirection.Row)));
                return true;
            case "flex-row-reverse":
                output.Add(new(TwPropertyId.FlexDirection, TwValue.Enum((byte)TwFlexDirection.RowReverse)));
                return true;
            case "flex-col":
                output.Add(new(TwPropertyId.FlexDirection, TwValue.Enum((byte)TwFlexDirection.Column)));
                return true;
            case "flex-col-reverse":
                output.Add(new(TwPropertyId.FlexDirection, TwValue.Enum((byte)TwFlexDirection.ColumnReverse)));
                return true;
            case "flex-wrap":
                output.Add(new(TwPropertyId.FlexWrap, TwValue.Enum((byte)TwFlexWrap.Wrap)));
                return true;
            case "flex-wrap-reverse":
                output.Add(new(TwPropertyId.FlexWrap, TwValue.Enum((byte)TwFlexWrap.WrapReverse)));
                return true;
            case "flex-nowrap":
                output.Add(new(TwPropertyId.FlexWrap, TwValue.Enum((byte)TwFlexWrap.NoWrap)));
                return true;
            case "flex-1":
                output.Add(new(TwPropertyId.FlexGrow, TwValue.Scalar(1)));
                output.Add(new(TwPropertyId.FlexShrink, TwValue.Scalar(1)));
                output.Add(new(TwPropertyId.FlexBasis, TwValue.Edges(0, 0, float.NaN, float.NaN)));
                return true;
            case "flex-auto":
                output.Add(new(TwPropertyId.FlexGrow, TwValue.Scalar(1)));
                output.Add(new(TwPropertyId.FlexShrink, TwValue.Scalar(1)));
                output.Add(new(TwPropertyId.FlexBasis, TwValue.Edges(float.NaN, 0, float.NaN, float.NaN)));
                return true;
            case "flex-initial":
                output.Add(new(TwPropertyId.FlexGrow, TwValue.Scalar(0)));
                output.Add(new(TwPropertyId.FlexShrink, TwValue.Scalar(1)));
                return true;
            case "flex-none":
                output.Add(new(TwPropertyId.FlexGrow, TwValue.Scalar(0)));
                output.Add(new(TwPropertyId.FlexShrink, TwValue.Scalar(0)));
                return true;
            case "grow":
                output.Add(new(TwPropertyId.FlexGrow, TwValue.Scalar(1)));
                return true;
            case "grow-0":
                output.Add(new(TwPropertyId.FlexGrow, TwValue.Scalar(0)));
                return true;
            case "shrink":
                output.Add(new(TwPropertyId.FlexShrink, TwValue.Scalar(1)));
                return true;
            case "shrink-0":
                output.Add(new(TwPropertyId.FlexShrink, TwValue.Scalar(0)));
                return true;
            case "transition":
                output.Add(new(TwPropertyId.TransitionProps, TwValue.Scalar((byte)TwTransitionProps.Default)));
                output.Add(new(TwPropertyId.TransitionDuration, TwValue.Scalar(150)));
                return true;
            case "transition-all":
                output.Add(new(TwPropertyId.TransitionProps, TwValue.Scalar((byte)TwTransitionProps.All)));
                output.Add(new(TwPropertyId.TransitionDuration, TwValue.Scalar(150)));
                return true;
            case "transition-colors":
                output.Add(new(TwPropertyId.TransitionProps, TwValue.Scalar((byte)TwTransitionProps.Colors)));
                output.Add(new(TwPropertyId.TransitionDuration, TwValue.Scalar(150)));
                return true;
            case "transition-opacity":
                output.Add(new(TwPropertyId.TransitionProps, TwValue.Scalar((byte)TwTransitionProps.Opacity)));
                output.Add(new(TwPropertyId.TransitionDuration, TwValue.Scalar(150)));
                return true;
            case "transition-transform":
                output.Add(new(TwPropertyId.TransitionProps, TwValue.Scalar((byte)TwTransitionProps.Transform)));
                output.Add(new(TwPropertyId.TransitionDuration, TwValue.Scalar(150)));
                return true;
            case "transition-none":
                output.Add(new(TwPropertyId.TransitionProps, TwValue.Scalar(0)));
                return true;
            case "ease-linear":
                output.Add(new(TwPropertyId.TransitionEasing, TwValue.Enum((byte)TwEasing.Linear)));
                return true;
            case "ease-in":
                output.Add(new(TwPropertyId.TransitionEasing, TwValue.Enum((byte)TwEasing.In)));
                return true;
            case "ease-out":
                output.Add(new(TwPropertyId.TransitionEasing, TwValue.Enum((byte)TwEasing.Out)));
                return true;
            case "ease-in-out":
                output.Add(new(TwPropertyId.TransitionEasing, TwValue.Enum((byte)TwEasing.InOut)));
                return true;
            case "animate-spin":
                output.Add(new(TwPropertyId.Keyframes, TwValue.Enum((byte)TwKeyframes.Spin)));
                return true;
            case "animate-pulse":
                output.Add(new(TwPropertyId.Keyframes, TwValue.Enum((byte)TwKeyframes.Pulse)));
                return true;
            case "animate-bounce":
                output.Add(new(TwPropertyId.Keyframes, TwValue.Enum((byte)TwKeyframes.Bounce)));
                return true;
            case "animate-none":
                output.Add(new(TwPropertyId.Keyframes, TwValue.Enum((byte)TwKeyframes.None)));
                return true;
        }

        if (TryPrefix(u, "bg-gradient-to-", out var arg))
        {
            TwGradientDirection? direction = arg switch
            {
                "t" => TwGradientDirection.Top,
                "tr" => TwGradientDirection.TopRight,
                "r" => TwGradientDirection.Right,
                "br" => TwGradientDirection.BottomRight,
                "b" => TwGradientDirection.Bottom,
                "bl" => TwGradientDirection.BottomLeft,
                "l" => TwGradientDirection.Left,
                "tl" => TwGradientDirection.TopLeft,
                _ => null,
            };
            if (direction is { } d)
            {
                output.Add(new(TwPropertyId.GradientDirection, TwValue.Enum((byte)d)));
                return true;
            }
            error = $"unknown gradient direction '{arg.ToString()}'";
            return false;
        }

        if (TryPrefix(u, "bg-", out arg))
            return TryColor(arg, TwPropertyId.Background, output, ref error);

        if (TryPrefix(u, "from-", out arg))
            return TryColor(arg, TwPropertyId.GradientFrom, output, ref error);

        if (TryPrefix(u, "via-", out arg))
            return TryColor(arg, TwPropertyId.GradientVia, output, ref error);

        if (TryPrefix(u, "to-", out arg))
            return TryColor(arg, TwPropertyId.GradientTo, output, ref error);

        if (TryPrefix(u, "text-", out arg))
            return ResolveText(arg, output, ref error);

        if (TryPrefix(u, "font-", out arg))
        {
            if (TwTables.FontWeights.TryGetValue(arg.ToString(), out float weight))
            {
                output.Add(new(TwPropertyId.FontWeight, TwValue.Scalar(weight)));
                return true;
            }
            error = arg is "sans" or "serif" or "mono"
                ? "font families are not supported in v0 — set FontFamily directly"
                : $"unknown font weight '{arg.ToString()}'";
            return false;
        }

        if (TryPrefix(u, "tracking-", out arg))
        {
            if (TwTables.Tracking.TryGetValue(arg.ToString(), out float em))
            {
                output.Add(new(TwPropertyId.CharacterSpacingEm, TwValue.Scalar(em)));
                return true;
            }
            error = $"unknown tracking '{arg.ToString()}'";
            return false;
        }

        if (TryPrefix(u, "line-clamp-", out arg))
        {
            if (SpanParse.PositiveInt(arg, out int lines) && lines > 0)
            {
                output.Add(new(TwPropertyId.LineClamp, TwValue.Scalar(lines)));
                return true;
            }
            error = $"line-clamp expects a positive integer, got '{arg.ToString()}'";
            return false;
        }

        if (TryPrefix(u, "leading-", out arg))
        {
            if (TwTables.Leadings.TryGetValue(arg.ToString(), out float multiplier))
            {
                output.Add(new(TwPropertyId.LineHeight, TwValue.Scalar(multiplier)));
                return true;
            }
            error = $"unknown line height '{arg.ToString()}' (numeric leading-* is not supported in v0)";
            return false;
        }

        if (TryPrefix(u, "opacity-", out arg))
        {
            if (SpanParse.Float(arg, out float pct) && pct is >= 0 and <= 100)
            {
                output.Add(new(TwPropertyId.Opacity, TwValue.Scalar(pct / 100f)));
                return true;
            }
            error = $"opacity expects 0–100, got '{arg.ToString()}'";
            return false;
        }

        if (TryPrefix(u, "shadow-", out arg))
        {
            if (TwTables.Shadows.TryGetValue(arg.ToString(), out var shadow))
            {
                output.Add(new(TwPropertyId.Shadow, shadow));
                return true;
            }
            error = $"unknown shadow '{arg.ToString()}' (colored shadows are not supported in v0)";
            return false;
        }

        if (TryPrefix(u, "rounded-", out arg))
            return ResolveRounded(arg, output, ref error);

        if (TryPrefix(u, "border-", out arg))
        {
            // width: border-0/2/4/8; anything else is a color
            if (arg.Length == 1 && arg[0] is '0' or '2' or '4' or '8')
            {
                output.Add(new(TwPropertyId.BorderWidth, TwValue.Scalar(arg[0] - '0')));
                return true;
            }
            return TryColor(arg, TwPropertyId.BorderColor, output, ref error);
        }

        if (TryPrefix(u, "gap-", out arg))
            return ResolveGap(arg, output, ref error);

        // Spacing: p/px/py/pt/pr/pb/pl and m equivalents.
        if (u.Length >= 2 && (u[0] == 'p' || u[0] == 'm'))
        {
            var property = u[0] == 'p' ? TwPropertyId.Padding : TwPropertyId.Margin;
            var sides = u[1] switch
            {
                '-' => Sides.All,
                'x' => Sides.Left | Sides.Right,
                'y' => Sides.Top | Sides.Bottom,
                't' => Sides.Top,
                'r' => Sides.Right,
                'b' => Sides.Bottom,
                'l' => Sides.Left,
                _ => Sides.None,
            };
            int argStart = u[1] == '-' ? 2 : 3;
            if (sides != Sides.None && (u[1] == '-' || (u.Length > 2 && u[2] == '-')))
            {
                if (property == TwPropertyId.Padding && negative)
                {
                    error = "negative padding is not valid";
                    return false;
                }
                // m-auto family: CSS auto margins become self-alignment natively.
                if (property == TwPropertyId.Margin && u[argStart..] is "auto")
                    return ResolveAutoMargin(u[1], sides, output, ref error);
                if (TrySpacing(u[argStart..], out float v, ref error))
                {
                    if (negative) v = -v;
                    output.Add(new(property, EdgesFor(sides, v)));
                    return true;
                }
                return false;
            }
        }

        if (TryPrefix(u, "rotate-", out arg))
        {
            if (TryNumber(arg, "deg", out float deg))
            {
                output.Add(new(TwPropertyId.Rotate, TwValue.Scalar(negative ? -deg : deg)));
                return true;
            }
            error = $"rotate expects a number of degrees, got '{arg.ToString()}'";
            return false;
        }

        if (TryPrefix(u, "scale-", out arg))
        {
            if (TryNumber(arg, "%", out float pct))
            {
                output.Add(new(TwPropertyId.Scale, TwValue.Scalar((negative ? -pct : pct) / 100f)));
                return true;
            }
            error = $"scale expects a percentage, got '{arg.ToString()}'";
            return false;
        }

        if (TryPrefix(u, "translate-x-", out arg))
        {
            if (!TrySpacing(arg, out float tx, ref error)) return false;
            output.Add(new(TwPropertyId.TranslateX, TwValue.Scalar(negative ? -tx : tx)));
            return true;
        }

        if (TryPrefix(u, "translate-y-", out arg))
        {
            if (!TrySpacing(arg, out float ty, ref error)) return false;
            output.Add(new(TwPropertyId.TranslateY, TwValue.Scalar(negative ? -ty : ty)));
            return true;
        }

        if (TryPrefix(u, "justify-", out arg))
        {
            TwJustify? justify = arg switch
            {
                "start" or "normal" => TwJustify.Start,
                "end" => TwJustify.End,
                "center" => TwJustify.Center,
                "between" => TwJustify.Between,
                "around" => TwJustify.Around,
                "evenly" => TwJustify.Evenly,
                _ => null,
            };
            if (justify is { } j)
            {
                output.Add(new(TwPropertyId.JustifyContent, TwValue.Enum((byte)j)));
                return true;
            }
            error = $"unknown justify '{arg.ToString()}'";
            return false;
        }

        if (TryPrefix(u, "items-", out arg))
        {
            TwAlignItems? items = arg switch
            {
                "start" => TwAlignItems.Start,
                "end" => TwAlignItems.End,
                "center" => TwAlignItems.Center,
                "stretch" => TwAlignItems.Stretch,
                _ => null,
            };
            if (items is { } i)
            {
                output.Add(new(TwPropertyId.AlignItems, TwValue.Enum((byte)i)));
                return true;
            }
            error = arg is "baseline"
                ? "'items-baseline' has no FlexLayout equivalent — use items-start/center/end"
                : $"unknown items alignment '{arg.ToString()}'";
            return false;
        }

        if (TryPrefix(u, "self-", out arg))
        {
            TwAlignSelfFlex? self = arg switch
            {
                "auto" => TwAlignSelfFlex.Auto,
                "start" => TwAlignSelfFlex.Start,
                "end" => TwAlignSelfFlex.End,
                "center" => TwAlignSelfFlex.Center,
                "stretch" => TwAlignSelfFlex.Stretch,
                _ => null,
            };
            if (self is { } s)
            {
                output.Add(new(TwPropertyId.FlexAlignSelf, TwValue.Enum((byte)s)));
                return true;
            }
            error = $"unknown self alignment '{arg.ToString()}'";
            return false;
        }

        if (TryPrefix(u, "grow-", out arg))
        {
            if (TryNumber(arg, "", out float grow) && grow >= 0)
            {
                output.Add(new(TwPropertyId.FlexGrow, TwValue.Scalar(grow)));
                return true;
            }
            error = $"grow expects a non-negative number, got '{arg.ToString()}'";
            return false;
        }

        if (TryPrefix(u, "shrink-", out arg))
        {
            if (TryNumber(arg, "", out float shrink) && shrink >= 0)
            {
                output.Add(new(TwPropertyId.FlexShrink, TwValue.Scalar(shrink)));
                return true;
            }
            error = $"shrink expects a non-negative number, got '{arg.ToString()}'";
            return false;
        }

        if (TryPrefix(u, "basis-", out arg))
        {
            if (arg is "auto")
            {
                output.Add(new(TwPropertyId.FlexBasis, TwValue.Edges(float.NaN, 0, float.NaN, float.NaN)));
                return true;
            }
            if (arg is "full")
            {
                output.Add(new(TwPropertyId.FlexBasis, TwValue.Edges(1, 1, float.NaN, float.NaN)));
                return true;
            }
            int slash = arg.IndexOf('/');
            if (slash > 0
                && SpanParse.Float(arg[..slash], out float num)
                && SpanParse.Float(arg[(slash + 1)..], out float den) && den > 0)
            {
                output.Add(new(TwPropertyId.FlexBasis, TwValue.Edges(num / den, 1, float.NaN, float.NaN)));
                return true;
            }
            if (TrySpacing(arg, out float basis, ref error))
            {
                output.Add(new(TwPropertyId.FlexBasis, TwValue.Edges(basis, 0, float.NaN, float.NaN)));
                return true;
            }
            return false;
        }

        if (TryPrefix(u, "grid-cols-", out arg))
            return ResolveGridCount(arg, TwPropertyId.GridColumns, output, ref error);

        if (TryPrefix(u, "grid-rows-", out arg))
            return ResolveGridCount(arg, TwPropertyId.GridRows, output, ref error);

        if (TryPrefix(u, "col-span-", out arg))
            return ResolveGridCount(arg, TwPropertyId.GridColumnSpan, output, ref error);

        if (TryPrefix(u, "row-span-", out arg))
            return ResolveGridCount(arg, TwPropertyId.GridRowSpan, output, ref error);

        if (TryPrefix(u, "col-start-", out arg))
            return ResolveGridStart(arg, TwPropertyId.GridColumn, output, ref error);

        if (TryPrefix(u, "row-start-", out arg))
            return ResolveGridStart(arg, TwPropertyId.GridRow, output, ref error);

        if (TryPrefix(u, "duration-", out arg))
        {
            if (TryNumber(arg, "ms", out float ms) && ms >= 0)
            {
                output.Add(new(TwPropertyId.TransitionDuration, TwValue.Scalar(ms)));
                return true;
            }
            error = $"duration expects milliseconds, got '{arg.ToString()}'";
            return false;
        }

        if (TryPrefix(u, "delay-", out arg))
        {
            if (TryNumber(arg, "ms", out float ms) && ms >= 0)
            {
                output.Add(new(TwPropertyId.TransitionDelay, TwValue.Scalar(ms)));
                return true;
            }
            error = $"delay expects milliseconds, got '{arg.ToString()}'";
            return false;
        }

        if (TryPrefix(u, "z-", out arg))
        {
            if (arg is "auto")
            {
                output.Add(new(TwPropertyId.ZIndex, TwValue.Scalar(0)));
                return true;
            }
            if (SpanParse.PositiveInt(arg, out int z))
            {
                output.Add(new(TwPropertyId.ZIndex, TwValue.Scalar(negative ? -z : z)));
                return true;
            }
            error = $"z-index expects an integer, got '{arg.ToString()}'";
            return false;
        }

        if (TryPrefix(u, "size-", out arg))
        {
            var before = output.Count;
            if (ResolveSize(arg, TwPropertyId.Width, output, ref error) && output.Count > before)
            {
                output.Add(new(TwPropertyId.Height, output[^1].Value));
                return true;
            }
            return false;
        }

        // Sizing.
        if (TryPrefix(u, "min-w-", out arg)) return ResolveSize(arg, TwPropertyId.MinWidth, output, ref error);
        if (TryPrefix(u, "min-h-", out arg)) return ResolveSize(arg, TwPropertyId.MinHeight, output, ref error);
        if (TryPrefix(u, "max-w-", out arg)) return ResolveMaxWidth(arg, output, ref error);
        if (TryPrefix(u, "max-h-", out arg)) return ResolveSize(arg, TwPropertyId.MaxHeight, output, ref error);
        if (TryPrefix(u, "w-", out arg)) return ResolveSize(arg, TwPropertyId.Width, output, ref error);
        if (TryPrefix(u, "h-", out arg)) return ResolveSize(arg, TwPropertyId.Height, output, ref error);

        error = KnownUnsupported(u) ?? "unknown utility";
        return false;
    }

    private static bool ResolveText(ReadOnlySpan<char> arg, List<TwDeclaration> output, ref string? error)
    {
        switch (arg)
        {
            case "left" or "start":
                output.Add(new(TwPropertyId.TextAlign, TwValue.Enum((byte)TwTextAlign.Start)));
                return true;
            case "center":
                output.Add(new(TwPropertyId.TextAlign, TwValue.Enum((byte)TwTextAlign.Center)));
                return true;
            case "right" or "end":
                output.Add(new(TwPropertyId.TextAlign, TwValue.Enum((byte)TwTextAlign.End)));
                return true;
            case "justify":
                output.Add(new(TwPropertyId.TextAlign, TwValue.Enum((byte)TwTextAlign.Justify)));
                return true;
        }

        if (TwTables.FontSizes.TryGetValue(arg.ToString(), out var size))
        {
            output.Add(new(TwPropertyId.FontSize, TwValue.Scalar(size.Size)));
            output.Add(new(TwPropertyId.LineHeight, TwValue.Scalar(size.Line)));
            return true;
        }

        return TryColor(arg, TwPropertyId.TextColor, output, ref error);
    }

    private static bool ResolveRounded(ReadOnlySpan<char> arg, List<TwDeclaration> output, ref string? error)
    {
        // rounded-{size} | rounded-{side} | rounded-{side}-{size}
        Corners corners = Corners.All;
        var sizeArg = arg;

        int dash = arg.IndexOf('-');
        var head = dash < 0 ? arg : arg[..dash];
        var sideCorners = head switch
        {
            "t" => Corners.TopLeft | Corners.TopRight,
            "b" => Corners.BottomLeft | Corners.BottomRight,
            "l" => Corners.TopLeft | Corners.BottomLeft,
            "r" => Corners.TopRight | Corners.BottomRight,
            "tl" => Corners.TopLeft,
            "tr" => Corners.TopRight,
            "bl" => Corners.BottomLeft,
            "br" => Corners.BottomRight,
            _ => Corners.None,
        };
        if (sideCorners != Corners.None)
        {
            corners = sideCorners;
            sizeArg = dash < 0 ? "" : arg[(dash + 1)..];
        }

        if (TwTables.Radii.TryGetValue(sizeArg.ToString(), out float radius))
        {
            output.Add(new(TwPropertyId.CornerRadius, CornersFor(corners, radius)));
            return true;
        }
        error = $"unknown radius '{arg.ToString()}'";
        return false;
    }

    private static bool ResolveGap(ReadOnlySpan<char> arg, List<TwDeclaration> output, ref string? error)
    {
        // Gap uses Edges encoding: X = horizontal (column) gap, Y = vertical (row) gap.
        if (TryPrefix(arg, "x-", out var rest))
        {
            if (!TrySpacing(rest, out float x, ref error)) return false;
            output.Add(new(TwPropertyId.Gap, TwValue.Edges(x, float.NaN, float.NaN, float.NaN)));
            return true;
        }
        if (TryPrefix(arg, "y-", out rest))
        {
            if (!TrySpacing(rest, out float y, ref error)) return false;
            output.Add(new(TwPropertyId.Gap, TwValue.Edges(float.NaN, y, float.NaN, float.NaN)));
            return true;
        }
        if (!TrySpacing(arg, out float v, ref error)) return false;
        output.Add(new(TwPropertyId.Gap, TwValue.Edges(v, v, float.NaN, float.NaN)));
        return true;
    }

    private static bool ResolveSize(ReadOnlySpan<char> arg, TwPropertyId property, List<TwDeclaration> output, ref string? error)
    {
        if (arg is "full")
        {
            output.Add(new(property, TwValue.Scalar(TwValue.Full)));
            return true;
        }
        if (arg is "screen")
        {
            error = "'*-screen' is not supported — use w-full/h-full on a root element";
            return false;
        }
        if (arg is "0")
        {
            output.Add(new(property, TwValue.Scalar(0)));
            return true;
        }
        if (TrySpacing(arg, out float v, ref error))
        {
            output.Add(new(property, TwValue.Scalar(v)));
            return true;
        }
        return false;
    }

    private static bool ResolveMaxWidth(ReadOnlySpan<char> arg, List<TwDeclaration> output, ref string? error)
    {
        // max-w has its own named scale (rem-based) on top of spacing values.
        if (TwTables.MaxWidths.TryGetValue(arg.ToString(), out float named))
        {
            output.Add(new(TwPropertyId.MaxWidth, TwValue.Scalar(named)));
            return true;
        }
        return ResolveSize(arg, TwPropertyId.MaxWidth, output, ref error);
    }

    private static bool ResolveAutoMargin(char side, Sides sides, List<TwDeclaration> output, ref string? error)
    {
        _ = sides;
        switch (side)
        {
            case '-': // m-auto → centered both ways
                output.Add(new(TwPropertyId.AlignSelfX, TwValue.Enum((byte)TwAlign.Center)));
                output.Add(new(TwPropertyId.AlignSelfY, TwValue.Enum((byte)TwAlign.Center)));
                return true;
            case 'x':
                output.Add(new(TwPropertyId.AlignSelfX, TwValue.Enum((byte)TwAlign.Center)));
                return true;
            case 'y':
                output.Add(new(TwPropertyId.AlignSelfY, TwValue.Enum((byte)TwAlign.Center)));
                return true;
            case 'l': // auto left margin pushes the element to the end
                output.Add(new(TwPropertyId.AlignSelfX, TwValue.Enum((byte)TwAlign.End)));
                return true;
            case 'r':
                output.Add(new(TwPropertyId.AlignSelfX, TwValue.Enum((byte)TwAlign.Start)));
                return true;
            case 't':
                output.Add(new(TwPropertyId.AlignSelfY, TwValue.Enum((byte)TwAlign.End)));
                return true;
            case 'b':
                output.Add(new(TwPropertyId.AlignSelfY, TwValue.Enum((byte)TwAlign.Start)));
                return true;
            default:
                error = "margin 'auto' is only valid on m/mx/my/ml/mr/mt/mb";
                return false;
        }
    }

    private static bool ResolveGridCount(ReadOnlySpan<char> arg, TwPropertyId property, List<TwDeclaration> output, ref string? error)
    {
        if (SpanParse.PositiveInt(arg, out int n) && n >= 1)
        {
            output.Add(new(property, TwValue.Scalar(n)));
            return true;
        }
        error = $"expected a positive count, got '{arg.ToString()}'";
        return false;
    }

    private static bool ResolveGridStart(ReadOnlySpan<char> arg, TwPropertyId property, List<TwDeclaration> output, ref string? error)
    {
        if (SpanParse.PositiveInt(arg, out int n) && n >= 1)
        {
            output.Add(new(property, TwValue.Scalar(n - 1))); // CSS 1-based → Grid 0-based
            return true;
        }
        error = $"expected a 1-based track index, got '{arg.ToString()}'";
        return false;
    }

    /// <summary>Plain number or arbitrary "[N]" with an optional unit suffix (deg, %).</summary>
    private static bool TryNumber(ReadOnlySpan<char> arg, string suffix, out float value)
    {
        if (arg.Length >= 2 && arg[0] == '[' && arg[^1] == ']')
        {
            var inner = arg[1..^1];
            if (inner.EndsWith(suffix, StringComparison.Ordinal)) inner = inner[..^suffix.Length];
            return SpanParse.Float(inner, out value);
        }
        return SpanParse.Float(arg, out value);
    }

    // ---------------------------------------------------------------- values

    private static bool TryColor(ReadOnlySpan<char> arg, TwPropertyId property, List<TwDeclaration> output, ref string? error)
    {
        // Arbitrary hex: bg-[#rgb] / bg-[#rrggbb] / bg-[#rrggbbaa] (CSS channel order).
        if (arg.Length >= 3 && arg[0] == '[' && arg[^1] == ']')
        {
            if (TryParseHexColor(arg[1..^1], out uint rgba))
            {
                output.Add(new(property, TwValue.Color(rgba)));
                return true;
            }
            error = $"invalid color '{arg.ToString()}'";
            return false;
        }

        // Opacity suffix: bg-black/50.
        int slash = arg.LastIndexOf('/');
        float alpha = 1f;
        var name = arg;
        if (slash >= 0)
        {
            if (!SpanParse.Float(arg[(slash + 1)..], out float pct) || pct is < 0 or > 100)
            {
                error = $"invalid opacity modifier '{arg.ToString()}'";
                return false;
            }
            alpha = pct / 100f;
            name = arg[..slash];
        }

        if (TwPalette.Colors.TryGetValue(name.ToString(), out uint color))
        {
            if (alpha < 1f)
                color = (color & 0x00FFFFFF) | ((uint)Math.Round(((color >> 24) & 0xFF) * alpha) << 24);
            output.Add(new(property, TwValue.Color(color)));
            return true;
        }

        error = $"unknown color '{name.ToString()}'";
        return false;
    }

    private static bool TryParseHexColor(ReadOnlySpan<char> hex, out uint rgba)
    {
        rgba = 0;
        if (hex.Length == 0 || hex[0] != '#') return false;
        hex = hex[1..];

        static bool Hex(ReadOnlySpan<char> s, out uint v) => SpanParse.HexUInt(s, out v);

        switch (hex.Length)
        {
            case 3: // #rgb
                if (!Hex(hex, out uint c3)) return false;
                uint r = (c3 >> 8) & 0xF, g = (c3 >> 4) & 0xF, b = c3 & 0xF;
                rgba = 0xFF000000 | (r * 17 << 16) | (g * 17 << 8) | (b * 17);
                return true;
            case 6: // #rrggbb
                if (!Hex(hex, out uint c6)) return false;
                rgba = 0xFF000000 | c6;
                return true;
            case 8: // #rrggbbaa — CSS puts alpha last; we store it first
                if (!Hex(hex, out uint c8)) return false;
                rgba = (c8 << 24) | (c8 >> 8);
                return true;
            default:
                return false;
        }
    }

    /// <summary>Spacing value: numeric step (×4), "px" (=1), or arbitrary "[N]"/"[Npx]" in DIU.</summary>
    private static bool TrySpacing(ReadOnlySpan<char> arg, out float value, ref string? error)
    {
        value = 0;
        if (arg is "px")
        {
            value = 1;
            return true;
        }
        if (arg.Length >= 2 && arg[0] == '[' && arg[^1] == ']')
        {
            var inner = arg[1..^1];
            if (inner.EndsWith("px", StringComparison.Ordinal)) inner = inner[..^2];
            if (SpanParse.Float(inner, out value))
                return true;
            error = $"invalid arbitrary value '{arg.ToString()}'";
            return false;
        }
        if (SpanParse.Float(arg, out float steps) && steps >= 0)
        {
            value = steps * TwTables.SpacingUnit;
            return true;
        }
        error = $"invalid spacing value '{arg.ToString()}'";
        return false;
    }

    // ---------------------------------------------------------------- helpers

    private static bool TryPrefix(ReadOnlySpan<char> s, string prefix, out ReadOnlySpan<char> rest)
    {
        if (s.StartsWith(prefix, StringComparison.Ordinal))
        {
            rest = s[prefix.Length..];
            return true;
        }
        rest = default;
        return false;
    }

    [Flags]
    private enum Sides : byte { None = 0, Left = 1, Top = 2, Right = 4, Bottom = 8, All = 15 }

    [Flags]
    private enum Corners : byte { None = 0, TopLeft = 1, TopRight = 2, BottomLeft = 4, BottomRight = 8, All = 15 }

    private static TwValue EdgesFor(Sides sides, float v) => TwValue.Edges(
        (sides & Sides.Left) != 0 ? v : float.NaN,
        (sides & Sides.Top) != 0 ? v : float.NaN,
        (sides & Sides.Right) != 0 ? v : float.NaN,
        (sides & Sides.Bottom) != 0 ? v : float.NaN);

    private static TwValue CornersFor(Corners corners, float v) => TwValue.Corners(
        (corners & Corners.TopLeft) != 0 ? v : float.NaN,
        (corners & Corners.TopRight) != 0 ? v : float.NaN,
        (corners & Corners.BottomLeft) != 0 ? v : float.NaN,
        (corners & Corners.BottomRight) != 0 ? v : float.NaN);

    private static TwValue UniformCorners(float radius) => TwValue.Corners(radius, radius, radius, radius);

    /// <summary>Friendly messages for common web-only utilities AI models emit.</summary>
    private static string? KnownUnsupported(ReadOnlySpan<char> u)
    {
        if (u is "sr-only") return "'sr-only' has no native equivalent — use SemanticProperties instead";
        if (u is "capitalize") return "'capitalize' has no native equivalent (MAUI TextTransform supports upper/lower only)";
        if (u is "w-screen" or "h-screen") return "'*-screen' is not supported — use w-full/h-full on a root element";
        if (u.StartsWith("divide-", StringComparison.Ordinal)) return "'divide-*' is not supported in v0 — style children directly";
        if (u.StartsWith("space-", StringComparison.Ordinal)) return "'space-*' is not supported — use gap-* on the layout instead";
        if (u.StartsWith("ring-", StringComparison.Ordinal)) return "'ring-*' is not supported in v0 — use border-* or shadow-*";
        if (u.StartsWith("animate-", StringComparison.Ordinal))
            return "unknown animation — supported: animate-spin, animate-pulse, animate-bounce, animate-none";
        if (u.StartsWith("hover:", StringComparison.Ordinal)) return "variants go before the utility, e.g. hover:bg-blue-700";
        return null;
    }
}
