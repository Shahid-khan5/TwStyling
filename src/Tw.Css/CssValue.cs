using System.Globalization;

namespace Tw.Css;

/// <summary>A CSS dimension unit. <see cref="None"/> is a plain number.</summary>
public enum CssUnit : byte
{
    None = 0,
    Percent,
    // lengths
    Px, Rem, Em, Pt, Vw, Vh, Ch, Ex,
    // angles
    Deg, Rad, Grad, Turn,
    // times
    S, Ms,
    // grid
    Fr,
}

/// <summary>Unit classification, used to decide which <c>calc()</c> operations are legal.</summary>
public enum CssUnitKind : byte { Number, Percent, Length, Angle, Time, Flex }

public static class CssUnits
{
    public static CssUnitKind KindOf(CssUnit u) => u switch
    {
        CssUnit.None => CssUnitKind.Number,
        CssUnit.Percent => CssUnitKind.Percent,
        CssUnit.Px or CssUnit.Rem or CssUnit.Em or CssUnit.Pt or CssUnit.Vw or CssUnit.Vh or CssUnit.Ch or CssUnit.Ex => CssUnitKind.Length,
        CssUnit.Deg or CssUnit.Rad or CssUnit.Grad or CssUnit.Turn => CssUnitKind.Angle,
        CssUnit.S or CssUnit.Ms => CssUnitKind.Time,
        CssUnit.Fr => CssUnitKind.Flex,
        _ => CssUnitKind.Number,
    };

    public static string Suffix(CssUnit u) => u switch
    {
        CssUnit.None => "",
        CssUnit.Percent => "%",
        CssUnit.Px => "px", CssUnit.Rem => "rem", CssUnit.Em => "em", CssUnit.Pt => "pt",
        CssUnit.Vw => "vw", CssUnit.Vh => "vh", CssUnit.Ch => "ch", CssUnit.Ex => "ex",
        CssUnit.Deg => "deg", CssUnit.Rad => "rad", CssUnit.Grad => "grad", CssUnit.Turn => "turn",
        CssUnit.S => "s", CssUnit.Ms => "ms",
        CssUnit.Fr => "fr",
        _ => "",
    };

    public static bool TryParse(ReadOnlySpan<char> s, out CssUnit unit)
    {
        unit = CssUnit.None;
        if (s.Length == 0) return true;
        if (s.Length == 1 && s[0] == '%') { unit = CssUnit.Percent; return true; }

        Span<char> lower = stackalloc char[8];
        if (s.Length > lower.Length) return false;
        for (int i = 0; i < s.Length; i++) lower[i] = char.ToLowerInvariant(s[i]);
        var l = lower.Slice(0, s.Length);

        if (l.SequenceEqual("px".AsSpan())) { unit = CssUnit.Px; return true; }
        if (l.SequenceEqual("rem".AsSpan())) { unit = CssUnit.Rem; return true; }
        if (l.SequenceEqual("em".AsSpan())) { unit = CssUnit.Em; return true; }
        if (l.SequenceEqual("pt".AsSpan())) { unit = CssUnit.Pt; return true; }
        if (l.SequenceEqual("vw".AsSpan())) { unit = CssUnit.Vw; return true; }
        if (l.SequenceEqual("vh".AsSpan())) { unit = CssUnit.Vh; return true; }
        if (l.SequenceEqual("ch".AsSpan())) { unit = CssUnit.Ch; return true; }
        if (l.SequenceEqual("ex".AsSpan())) { unit = CssUnit.Ex; return true; }
        if (l.SequenceEqual("deg".AsSpan())) { unit = CssUnit.Deg; return true; }
        if (l.SequenceEqual("rad".AsSpan())) { unit = CssUnit.Rad; return true; }
        if (l.SequenceEqual("grad".AsSpan())) { unit = CssUnit.Grad; return true; }
        if (l.SequenceEqual("turn".AsSpan())) { unit = CssUnit.Turn; return true; }
        if (l.SequenceEqual("s".AsSpan())) { unit = CssUnit.S; return true; }
        if (l.SequenceEqual("ms".AsSpan())) { unit = CssUnit.Ms; return true; }
        if (l.SequenceEqual("fr".AsSpan())) { unit = CssUnit.Fr; return true; }
        return false;
    }
}

/// <summary>Base of the CSS component-value tree.</summary>
public abstract class CssValue
{
    public abstract override string ToString();
}

/// <summary>A number, percentage, or dimension.</summary>
public sealed class CssNumber : CssValue
{
    public readonly double Value;
    public readonly CssUnit Unit;

    public CssNumber(double value, CssUnit unit = CssUnit.None) { Value = value; Unit = unit; }

    public CssUnitKind Kind => CssUnits.KindOf(Unit);

    public override string ToString() =>
        Value.ToString("0.######", CultureInfo.InvariantCulture) + CssUnits.Suffix(Unit);
}

/// <summary>A resolved color, held as straight (non-premultiplied) gamma-encoded sRGB in 0..1.</summary>
public sealed class CssColor : CssValue
{
    public readonly double R, G, B, A;

    public CssColor(double r, double g, double b, double a = 1.0) { R = r; G = g; B = b; A = a; }

    public static readonly CssColor Transparent = new(0, 0, 0, 0);

    /// <summary>Packs to 0xAARRGGBB, matching <c>TwValue.Rgba</c>.</summary>
    public uint ToRgba()
    {
        static uint C(double v) => (uint)Math.Round(Math.Max(0, Math.Min(1, v)) * 255.0, MidpointRounding.AwayFromZero);
        return (C(A) << 24) | (C(R) << 16) | (C(G) << 8) | C(B);
    }

    public override string ToString() => $"#{ToRgba():X8}";
}

/// <summary>A bare identifier, e.g. <c>auto</c>, <c>currentcolor</c>, <c>transparent</c>.</summary>
public sealed class CssIdent : CssValue
{
    public readonly string Name;
    public CssIdent(string name) => Name = name;
    public override string ToString() => Name;
}

/// <summary>A quoted string.</summary>
public sealed class CssString : CssValue
{
    public readonly string Value;
    public CssString(string value) => Value = value;
    public override string ToString() => "\"" + Value + "\"";
}

/// <summary>A single-character operator or separator: <c>+ - * / ,</c>.</summary>
public sealed class CssDelim : CssValue
{
    public readonly char Char;
    public CssDelim(char c) => Char = c;
    public override string ToString() => Char.ToString();
}

/// <summary>A function call. Args are the comma-separated top-level arguments.</summary>
public sealed class CssFunction : CssValue
{
    public readonly string Name;
    public readonly IReadOnlyList<CssValue> Args;

    public CssFunction(string name, IReadOnlyList<CssValue> args) { Name = name; Args = args; }

    public override string ToString() => Name + "(" + string.Join(", ", Args) + ")";
}

/// <summary>
/// A whitespace-separated sequence of values, e.g. the <c>0 0 0 / 0.1</c> inside <c>rgb()</c>,
/// or a shadow's <c>0 4px 6px -4px &lt;color&gt;</c>.
/// </summary>
public sealed class CssList : CssValue
{
    public readonly IReadOnlyList<CssValue> Items;
    public CssList(IReadOnlyList<CssValue> items) => Items = items;
    public override string ToString() => string.Join(" ", Items);
}

/// <summary>A comma-separated sequence, e.g. the two shadows in <c>box-shadow: a, b</c>.</summary>
public sealed class CssCommaList : CssValue
{
    public readonly IReadOnlyList<CssValue> Items;
    public CssCommaList(IReadOnlyList<CssValue> items) => Items = items;
    public override string ToString() => string.Join(", ", Items);
}

/// <summary>A parenthesised sub-expression, e.g. the inner group in <c>calc((1 + 2) * 3)</c>.</summary>
public sealed class CssParenGroup : CssValue
{
    public readonly CssValue Inner;
    public CssParenGroup(CssValue inner) => Inner = inner;
    public override string ToString() => "(" + Inner + ")";
}
