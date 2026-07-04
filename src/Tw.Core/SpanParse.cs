using System.Globalization;

namespace Tw.Core;

/// <summary>
/// Span number parsing that works on netstandard2.0 (no span TryParse overloads there).
/// Only used on the cold path, so the ToString fallback cost is irrelevant.
/// </summary>
internal static class SpanParse
{
    public static bool Float(ReadOnlySpan<char> s, out float value) =>
#if NETSTANDARD2_0
        float.TryParse(s.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
#else
        float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
#endif

    public static bool HexUInt(ReadOnlySpan<char> s, out uint value) =>
#if NETSTANDARD2_0
        uint.TryParse(s.ToString(), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);
#else
        uint.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);
#endif

    public static bool PositiveInt(ReadOnlySpan<char> s, out int value) =>
#if NETSTANDARD2_0
        int.TryParse(s.ToString(), NumberStyles.None, CultureInfo.InvariantCulture, out value);
#else
        int.TryParse(s, NumberStyles.None, CultureInfo.InvariantCulture, out value);
#endif
}
