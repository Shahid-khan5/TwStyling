namespace Tw.Css;

/// <summary>
/// Color parsing and the CSS Color 4 conversions Tailwind v4 depends on:
/// <c>oklch()</c>, <c>oklab()</c>, and <c>color-mix()</c>.
/// </summary>
public static class CssColorParser
{
    public static bool TryParseHex(string digits, out CssColor color)
    {
        color = CssColor.Transparent;
        static double H(char a, char b) => (Hex(a) * 16 + Hex(b)) / 255.0;
        static double H1(char a) => (Hex(a) * 16 + Hex(a)) / 255.0;

        switch (digits.Length)
        {
            case 3:
                color = new CssColor(H1(digits[0]), H1(digits[1]), H1(digits[2]));
                return true;
            case 4:
                color = new CssColor(H1(digits[0]), H1(digits[1]), H1(digits[2]), H1(digits[3]));
                return true;
            case 6:
                color = new CssColor(H(digits[0], digits[1]), H(digits[2], digits[3]), H(digits[4], digits[5]));
                return true;
            case 8:
                color = new CssColor(H(digits[0], digits[1]), H(digits[2], digits[3]), H(digits[4], digits[5]), H(digits[6], digits[7]));
                return true;
            default:
                return false;
        }
    }

    private static int Hex(char c) =>
        c >= '0' && c <= '9' ? c - '0' :
        c >= 'a' && c <= 'f' ? c - 'a' + 10 :
        c >= 'A' && c <= 'F' ? c - 'A' + 10 : 0;

    public static bool TryParseNamed(string name, out CssColor color)
    {
        if (NamedColors.TryGetValue(name, out uint argb))
        {
            color = new CssColor(
                ((argb >> 16) & 0xFF) / 255.0,
                ((argb >> 8) & 0xFF) / 255.0,
                (argb & 0xFF) / 255.0,
                ((argb >> 24) & 0xFF) / 255.0);
            return true;
        }
        color = CssColor.Transparent;
        return false;
    }

    // ---------------------------------------------------------------- oklab

    private static double Cbrt(double x) => x < 0 ? -Math.Pow(-x, 1.0 / 3.0) : Math.Pow(x, 1.0 / 3.0);

    private static double LinearToSrgb(double c) =>
        c <= 0.0031308 ? 12.92 * c : 1.055 * Math.Pow(c, 1.0 / 2.4) - 0.055;

    private static double SrgbToLinear(double c) =>
        c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);

    /// <summary>Oklab (L, a, b) to gamma-encoded sRGB, unclamped.</summary>
    public static (double R, double G, double B) OklabToSrgb(double L, double a, double b)
    {
        double l_ = L + 0.3963377774 * a + 0.2158037573 * b;
        double m_ = L - 0.1055613458 * a - 0.0638541728 * b;
        double s_ = L - 0.0894841775 * a - 1.2914855480 * b;

        double l = l_ * l_ * l_, m = m_ * m_ * m_, s = s_ * s_ * s_;

        double r = 4.0767416621 * l - 3.3077115913 * m + 0.2309699292 * s;
        double g = -1.2684380046 * l + 2.6097574011 * m - 0.3413193965 * s;
        double bb = -0.0041960863 * l - 0.7034186147 * m + 1.7076147010 * s;

        return (LinearToSrgb(r), LinearToSrgb(g), LinearToSrgb(bb));
    }

    /// <summary>Gamma-encoded sRGB to Oklab (L, a, b).</summary>
    public static (double L, double A, double B) SrgbToOklab(double r, double g, double b)
    {
        double lr = SrgbToLinear(r), lg = SrgbToLinear(g), lb = SrgbToLinear(b);

        double l = 0.4122214708 * lr + 0.5363325363 * lg + 0.0514459929 * lb;
        double m = 0.2119034982 * lr + 0.6806995451 * lg + 0.1073969566 * lb;
        double s = 0.0883024619 * lr + 0.2817188376 * lg + 0.6299787005 * lb;

        double l_ = Cbrt(l), m_ = Cbrt(m), s_ = Cbrt(s);

        return (
            0.2104542553 * l_ + 0.7936177850 * m_ - 0.0040720468 * s_,
            1.9779984951 * l_ - 2.4285922050 * m_ + 0.4505937099 * s_,
            0.0259040371 * l_ + 0.7827717662 * m_ - 0.8086757660 * s_);
    }

    public static CssColor FromOklch(double l, double c, double hDeg, double alpha)
    {
        double h = hDeg * Math.PI / 180.0;
        var (r, g, b) = OklabToSrgb(l, c * Math.Cos(h), c * Math.Sin(h));
        return new CssColor(Clamp01(r), Clamp01(g), Clamp01(b), Clamp01(alpha));
    }

    public static CssColor FromOklab(double l, double a, double b, double alpha)
    {
        var (r, g, bb) = OklabToSrgb(l, a, b);
        return new CssColor(Clamp01(r), Clamp01(g), Clamp01(bb), Clamp01(alpha));
    }

    public static CssColor FromHsl(double hDeg, double s, double l, double alpha)
    {
        hDeg = ((hDeg % 360) + 360) % 360;
        double c = (1 - Math.Abs(2 * l - 1)) * s;
        double x = c * (1 - Math.Abs((hDeg / 60.0) % 2 - 1));
        double m = l - c / 2;
        double r, g, b;
        if (hDeg < 60) { r = c; g = x; b = 0; }
        else if (hDeg < 120) { r = x; g = c; b = 0; }
        else if (hDeg < 180) { r = 0; g = c; b = x; }
        else if (hDeg < 240) { r = 0; g = x; b = c; }
        else if (hDeg < 300) { r = x; g = 0; b = c; }
        else { r = c; g = 0; b = x; }
        return new CssColor(Clamp01(r + m), Clamp01(g + m), Clamp01(b + m), Clamp01(alpha));
    }

    // ------------------------------------------------------------- color-mix

    /// <summary>
    /// Interpolates two colors per CSS Color 5 §3: weights are normalized, interpolation happens on
    /// premultiplied components, and when the raw weights sum below 100% the result alpha is scaled down.
    /// </summary>
    public static CssColor Mix(string space, CssColor c1, double? p1, CssColor c2, double? p2)
    {
        // Resolve omitted percentages.
        double w1, w2;
        if (p1 is null && p2 is null) { w1 = 0.5; w2 = 0.5; }
        else if (p1 is null) { w2 = p2!.Value; w1 = 1.0 - w2; }
        else if (p2 is null) { w1 = p1.Value; w2 = 1.0 - w1; }
        else { w1 = p1.Value; w2 = p2.Value; }

        double sum = w1 + w2;
        if (sum == 0) return CssColor.Transparent;

        // Alpha multiplier applies only when the author's weights sum below 100%.
        double alphaMultiplier = 1.0;
        if (p1 is not null && p2 is not null && sum < 1.0) alphaMultiplier = sum;

        w1 /= sum;
        w2 /= sum;

        double a = c1.A * w1 + c2.A * w2;

        if (a == 0) return new CssColor(0, 0, 0, 0);

        double r, g, b;
        if (space == "srgb")
        {
            // Premultiply, lerp, un-premultiply.
            r = (c1.R * c1.A * w1 + c2.R * c2.A * w2) / a;
            g = (c1.G * c1.A * w1 + c2.G * c2.A * w2) / a;
            b = (c1.B * c1.A * w1 + c2.B * c2.A * w2) / a;
        }
        else // "oklab" (and oklch, which we normalize to oklab before mixing)
        {
            var (l1, a1, b1) = SrgbToOklab(c1.R, c1.G, c1.B);
            var (l2, a2, b2) = SrgbToOklab(c2.R, c2.G, c2.B);

            double L = (l1 * c1.A * w1 + l2 * c2.A * w2) / a;
            double A = (a1 * c1.A * w1 + a2 * c2.A * w2) / a;
            double B = (b1 * c1.A * w1 + b2 * c2.A * w2) / a;

            (r, g, b) = OklabToSrgb(L, A, B);
        }

        return new CssColor(Clamp01(r), Clamp01(g), Clamp01(b), Clamp01(a * alphaMultiplier));
    }

    private static double Clamp01(double v) => v < 0 ? 0 : v > 1 ? 1 : v;

    // ---------------------------------------------------------------- names

    /// <summary>The CSS named colors, packed as 0xAARRGGBB.</summary>
    internal static readonly Dictionary<string, uint> NamedColors = new(StringComparer.OrdinalIgnoreCase)
    {
        ["transparent"] = 0x00000000,
        ["aliceblue"] = 0xFFF0F8FF, ["antiquewhite"] = 0xFFFAEBD7, ["aqua"] = 0xFF00FFFF,
        ["aquamarine"] = 0xFF7FFFD4, ["azure"] = 0xFFF0FFFF, ["beige"] = 0xFFF5F5DC,
        ["bisque"] = 0xFFFFE4C4, ["black"] = 0xFF000000, ["blanchedalmond"] = 0xFFFFEBCD,
        ["blue"] = 0xFF0000FF, ["blueviolet"] = 0xFF8A2BE2, ["brown"] = 0xFFA52A2A,
        ["burlywood"] = 0xFFDEB887, ["cadetblue"] = 0xFF5F9EA0, ["chartreuse"] = 0xFF7FFF00,
        ["chocolate"] = 0xFFD2691E, ["coral"] = 0xFFFF7F50, ["cornflowerblue"] = 0xFF6495ED,
        ["cornsilk"] = 0xFFFFF8DC, ["crimson"] = 0xFFDC143C, ["cyan"] = 0xFF00FFFF,
        ["darkblue"] = 0xFF00008B, ["darkcyan"] = 0xFF008B8B, ["darkgoldenrod"] = 0xFFB8860B,
        ["darkgray"] = 0xFFA9A9A9, ["darkgreen"] = 0xFF006400, ["darkgrey"] = 0xFFA9A9A9,
        ["darkkhaki"] = 0xFFBDB76B, ["darkmagenta"] = 0xFF8B008B, ["darkolivegreen"] = 0xFF556B2F,
        ["darkorange"] = 0xFFFF8C00, ["darkorchid"] = 0xFF9932CC, ["darkred"] = 0xFF8B0000,
        ["darksalmon"] = 0xFFE9967A, ["darkseagreen"] = 0xFF8FBC8F, ["darkslateblue"] = 0xFF483D8B,
        ["darkslategray"] = 0xFF2F4F4F, ["darkslategrey"] = 0xFF2F4F4F, ["darkturquoise"] = 0xFF00CED1,
        ["darkviolet"] = 0xFF9400D3, ["deeppink"] = 0xFFFF1493, ["deepskyblue"] = 0xFF00BFFF,
        ["dimgray"] = 0xFF696969, ["dimgrey"] = 0xFF696969, ["dodgerblue"] = 0xFF1E90FF,
        ["firebrick"] = 0xFFB22222, ["floralwhite"] = 0xFFFFFAF0, ["forestgreen"] = 0xFF228B22,
        ["fuchsia"] = 0xFFFF00FF, ["gainsboro"] = 0xFFDCDCDC, ["ghostwhite"] = 0xFFF8F8FF,
        ["gold"] = 0xFFFFD700, ["goldenrod"] = 0xFFDAA520, ["gray"] = 0xFF808080,
        ["green"] = 0xFF008000, ["greenyellow"] = 0xFFADFF2F, ["grey"] = 0xFF808080,
        ["honeydew"] = 0xFFF0FFF0, ["hotpink"] = 0xFFFF69B4, ["indianred"] = 0xFFCD5C5C,
        ["indigo"] = 0xFF4B0082, ["ivory"] = 0xFFFFFFF0, ["khaki"] = 0xFFF0E68C,
        ["lavender"] = 0xFFE6E6FA, ["lavenderblush"] = 0xFFFFF0F5, ["lawngreen"] = 0xFF7CFC00,
        ["lemonchiffon"] = 0xFFFFFACD, ["lightblue"] = 0xFFADD8E6, ["lightcoral"] = 0xFFF08080,
        ["lightcyan"] = 0xFFE0FFFF, ["lightgoldenrodyellow"] = 0xFFFAFAD2, ["lightgray"] = 0xFFD3D3D3,
        ["lightgreen"] = 0xFF90EE90, ["lightgrey"] = 0xFFD3D3D3, ["lightpink"] = 0xFFFFB6C1,
        ["lightsalmon"] = 0xFFFFA07A, ["lightseagreen"] = 0xFF20B2AA, ["lightskyblue"] = 0xFF87CEFA,
        ["lightslategray"] = 0xFF778899, ["lightslategrey"] = 0xFF778899, ["lightsteelblue"] = 0xFFB0C4DE,
        ["lightyellow"] = 0xFFFFFFE0, ["lime"] = 0xFF00FF00, ["limegreen"] = 0xFF32CD32,
        ["linen"] = 0xFFFAF0E6, ["magenta"] = 0xFFFF00FF, ["maroon"] = 0xFF800000,
        ["mediumaquamarine"] = 0xFF66CDAA, ["mediumblue"] = 0xFF0000CD, ["mediumorchid"] = 0xFFBA55D3,
        ["mediumpurple"] = 0xFF9370DB, ["mediumseagreen"] = 0xFF3CB371, ["mediumslateblue"] = 0xFF7B68EE,
        ["mediumspringgreen"] = 0xFF00FA9A, ["mediumturquoise"] = 0xFF48D1CC, ["mediumvioletred"] = 0xFFC71585,
        ["midnightblue"] = 0xFF191970, ["mintcream"] = 0xFFF5FFFA, ["mistyrose"] = 0xFFFFE4E1,
        ["moccasin"] = 0xFFFFE4B5, ["navajowhite"] = 0xFFFFDEAD, ["navy"] = 0xFF000080,
        ["oldlace"] = 0xFFFDF5E6, ["olive"] = 0xFF808000, ["olivedrab"] = 0xFF6B8E23,
        ["orange"] = 0xFFFFA500, ["orangered"] = 0xFFFF4500, ["orchid"] = 0xFFDA70D6,
        ["palegoldenrod"] = 0xFFEEE8AA, ["palegreen"] = 0xFF98FB98, ["paleturquoise"] = 0xFFAFEEEE,
        ["palevioletred"] = 0xFFDB7093, ["papayawhip"] = 0xFFFFEFD5, ["peachpuff"] = 0xFFFFDAB9,
        ["peru"] = 0xFFCD853F, ["pink"] = 0xFFFFC0CB, ["plum"] = 0xFFDDA0DD,
        ["powderblue"] = 0xFFB0E0E6, ["purple"] = 0xFF800080, ["rebeccapurple"] = 0xFF663399,
        ["red"] = 0xFFFF0000, ["rosybrown"] = 0xFFBC8F8F, ["royalblue"] = 0xFF4169E1,
        ["saddlebrown"] = 0xFF8B4513, ["salmon"] = 0xFFFA8072, ["sandybrown"] = 0xFFF4A460,
        ["seagreen"] = 0xFF2E8B57, ["seashell"] = 0xFFFFF5EE, ["sienna"] = 0xFFA0522D,
        ["silver"] = 0xFFC0C0C0, ["skyblue"] = 0xFF87CEEB, ["slateblue"] = 0xFF6A5ACD,
        ["slategray"] = 0xFF708090, ["slategrey"] = 0xFF708090, ["snow"] = 0xFFFFFAFA,
        ["springgreen"] = 0xFF00FF7F, ["steelblue"] = 0xFF4682B4, ["tan"] = 0xFFD2B48C,
        ["teal"] = 0xFF008080, ["thistle"] = 0xFFD8BFD8, ["tomato"] = 0xFFFF6347,
        ["turquoise"] = 0xFF40E0D0, ["violet"] = 0xFFEE82EE, ["wheat"] = 0xFFF5DEB3,
        ["white"] = 0xFFFFFFFF, ["whitesmoke"] = 0xFFF5F5F5, ["yellow"] = 0xFFFFFF00,
        ["yellowgreen"] = 0xFF9ACD32,
    };
}
