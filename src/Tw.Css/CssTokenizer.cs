using System.Globalization;

namespace Tw.Css;

public enum CssTokenType : byte
{
    Eof, Whitespace, Ident, Function, Number, Hash, String, Delim, Comma, OpenParen, CloseParen,
}

public readonly struct CssToken
{
    public readonly CssTokenType Type;
    /// <summary>Ident/Function name, Hash digits, or String contents.</summary>
    public readonly string Text;
    public readonly double Number;
    public readonly CssUnit Unit;
    public readonly char Delim;

    public CssToken(CssTokenType type, string text = "", double number = 0, CssUnit unit = CssUnit.None, char delim = '\0')
    { Type = type; Text = text; Number = number; Unit = unit; Delim = delim; }

    public override string ToString() => Type switch
    {
        CssTokenType.Number => Number.ToString(CultureInfo.InvariantCulture) + CssUnits.Suffix(Unit),
        CssTokenType.Delim => Delim.ToString(),
        CssTokenType.Function => Text + "(",
        CssTokenType.Hash => "#" + Text,
        _ => Text.Length > 0 ? Text : Type.ToString(),
    };
}

/// <summary>
/// A CSS component-value tokenizer, scoped to what appears on the right-hand side of a declaration.
/// </summary>
public static class CssTokenizer
{
    public static List<CssToken> Tokenize(string input)
    {
        var tokens = new List<CssToken>(16);
        int i = 0;
        var s = input.AsSpan();

        while (i < s.Length)
        {
            char c = s[i];

            if (IsWhitespace(c))
            {
                while (i < s.Length && IsWhitespace(s[i])) i++;
                // Collapse runs, and never emit a leading whitespace token.
                if (tokens.Count > 0) tokens.Add(new CssToken(CssTokenType.Whitespace));
                continue;
            }

            if (c == '/' && i + 1 < s.Length && s[i + 1] == '*')
            {
                i += 2;
                while (i + 1 < s.Length && !(s[i] == '*' && s[i + 1] == '/')) i++;
                i = Math.Min(i + 2, s.Length);
                continue;
            }

            if (c == '(') { tokens.Add(new CssToken(CssTokenType.OpenParen)); i++; continue; }
            if (c == ')') { tokens.Add(new CssToken(CssTokenType.CloseParen)); i++; continue; }
            if (c == ',') { tokens.Add(new CssToken(CssTokenType.Comma)); i++; continue; }

            if (c == '"' || c == '\'')
            {
                char quote = c;
                i++;
                int start = i;
                while (i < s.Length && s[i] != quote) i++;
                tokens.Add(new CssToken(CssTokenType.String, s.Slice(start, i - start).ToString()));
                if (i < s.Length) i++; // closing quote
                continue;
            }

            if (c == '#')
            {
                i++;
                int start = i;
                while (i < s.Length && IsHex(s[i])) i++;
                tokens.Add(new CssToken(CssTokenType.Hash, s.Slice(start, i - start).ToString()));
                continue;
            }

            if (StartsNumber(s, i))
            {
                i = ReadNumber(s, i, tokens);
                continue;
            }

            if (IsIdentStart(c) || (c == '-' && i + 1 < s.Length && (IsIdentStart(s[i + 1]) || s[i + 1] == '-')))
            {
                int start = i;
                while (i < s.Length && IsIdentChar(s[i])) i++;
                string name = s.Slice(start, i - start).ToString();

                if (i < s.Length && s[i] == '(')
                {
                    i++; // consume '('
                    tokens.Add(new CssToken(CssTokenType.Function, name));
                }
                else
                {
                    tokens.Add(new CssToken(CssTokenType.Ident, name));
                }
                continue;
            }

            tokens.Add(new CssToken(CssTokenType.Delim, delim: c));
            i++;
        }

        tokens.Add(new CssToken(CssTokenType.Eof));
        return tokens;
    }

    /// <summary>
    /// Decides whether a <c>+</c>/<c>-</c> at <paramref name="i"/> is a number's sign or a binary operator.
    /// Per CSS Syntax §4.3.1 a sign is part of the number only when a digit follows it *immediately*.
    /// That single rule separates <c>0 10px -3px</c> (a negative dimension) from <c>calc(4px - 2px)</c>
    /// (a subtraction), and is precisely why CSS mandates whitespace around binary +/- inside calc().
    /// </summary>
    private static bool StartsNumber(ReadOnlySpan<char> s, int i)
    {
        char c = s[i];
        if (char.IsDigit(c)) return true;
        if (c == '.' && i + 1 < s.Length && char.IsDigit(s[i + 1])) return true;

        if (c == '+' || c == '-')
            return i + 1 < s.Length &&
                (char.IsDigit(s[i + 1]) || (s[i + 1] == '.' && i + 2 < s.Length && char.IsDigit(s[i + 2])));

        return false;
    }

    private static int ReadNumber(ReadOnlySpan<char> s, int i, List<CssToken> tokens)
    {
        int start = i;
        if (s[i] == '+' || s[i] == '-') i++;
        while (i < s.Length && char.IsDigit(s[i])) i++;
        if (i < s.Length && s[i] == '.' && i + 1 < s.Length && char.IsDigit(s[i + 1]))
        {
            i++;
            while (i < s.Length && char.IsDigit(s[i])) i++;
        }
        if (i < s.Length && (s[i] == 'e' || s[i] == 'E'))
        {
            int save = i;
            i++;
            if (i < s.Length && (s[i] == '+' || s[i] == '-')) i++;
            if (i < s.Length && char.IsDigit(s[i])) { while (i < s.Length && char.IsDigit(s[i])) i++; }
            else i = save;
        }

        double value = double.Parse(s.Slice(start, i - start).ToString(), NumberStyles.Float, CultureInfo.InvariantCulture);

        // unit or percent
        int unitStart = i;
        if (i < s.Length && s[i] == '%') i++;
        else while (i < s.Length && IsIdentChar(s[i])) i++;

        var unitSpan = s.Slice(unitStart, i - unitStart);
        if (!CssUnits.TryParse(unitSpan, out var unit))
        {
            // Unknown unit: keep the number, drop the unit rather than fail the whole declaration.
            unit = CssUnit.None;
        }

        tokens.Add(new CssToken(CssTokenType.Number, number: value, unit: unit));
        return i;
    }

    private static bool IsWhitespace(char c) => c is ' ' or '\t' or '\n' or '\r' or '\f';
    private static bool IsHex(char c) => (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
    /// <summary>
    /// Excludes <c>-</c> on purpose: a leading hyphen only starts an identifier when another
    /// ident character or a second hyphen follows it (<c>--spacing</c>, <c>-webkit-x</c>). A lone
    /// <c>-</c> must fall through to a delimiter so <c>calc(4px - 2px)</c> keeps its operator.
    /// </summary>
    private static bool IsIdentStart(char c) => char.IsLetter(c) || c == '_' || c >= 0x80;
    private static bool IsIdentChar(char c) => char.IsLetterOrDigit(c) || c == '_' || c == '-' || c >= 0x80;
}
