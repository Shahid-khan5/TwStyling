#if NETSTANDARD2_0
// Minimal System.Index/System.Range so range operators compile on netstandard2.0.
// Mirrors the BCL implementation, trimmed to what Tw.Core uses.

namespace System;

internal readonly struct Index : IEquatable<Index>
{
    private readonly int _value;

    public Index(int value, bool fromEnd = false)
    {
        if (value < 0) throw new ArgumentOutOfRangeException(nameof(value));
        _value = fromEnd ? ~value : value;
    }

    private Index(int rawValue) => _value = rawValue;

    public static Index Start => new(0);
    public static Index End => new(~0);

    public static Index FromStart(int value)
    {
        if (value < 0) throw new ArgumentOutOfRangeException(nameof(value));
        return new Index(value);
    }

    public static Index FromEnd(int value)
    {
        if (value < 0) throw new ArgumentOutOfRangeException(nameof(value));
        return new Index(~value);
    }

    public int Value => _value < 0 ? ~_value : _value;
    public bool IsFromEnd => _value < 0;

    public int GetOffset(int length) => IsFromEnd ? length + _value + 1 : _value;

    public bool Equals(Index other) => _value == other._value;
    public override bool Equals(object? obj) => obj is Index other && Equals(other);
    public override int GetHashCode() => _value;

    public static implicit operator Index(int value) => FromStart(value);
}

internal readonly struct Range(Index start, Index end) : IEquatable<Range>
{
    public Index Start { get; } = start;
    public Index End { get; } = end;

    public static Range StartAt(Index start) => new(start, Index.End);
    public static Range EndAt(Index end) => new(Index.Start, end);
    public static Range All => new(Index.Start, Index.End);

    public (int Offset, int Length) GetOffsetAndLength(int length)
    {
        int start = Start.GetOffset(length);
        int end = End.GetOffset(length);
        if ((uint)end > (uint)length || (uint)start > (uint)end)
            throw new ArgumentOutOfRangeException(nameof(length));
        return (start, end - start);
    }

    public bool Equals(Range other) => Start.Equals(other.Start) && End.Equals(other.End);
    public override bool Equals(object? obj) => obj is Range other && Equals(other);
    public override int GetHashCode() => Start.GetHashCode() * 31 + End.GetHashCode();
}
#endif
