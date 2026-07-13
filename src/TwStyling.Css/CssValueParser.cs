namespace TwStyling.Css;

/// <summary>Turns a declaration's right-hand side into a <see cref="CssValue"/> tree.</summary>
internal static class CssValueParser
{
    public static CssValue Parse(string input)
    {
        var tokens = CssTokenizer.Tokenize(input);
        int i = 0;
        var value = ParseCommaList(tokens, ref i);
        return value;
    }

    /// <summary>Parses comma-separated sequences until EOF or an unmatched <c>)</c>.</summary>
    private static CssValue ParseCommaList(List<CssToken> tokens, ref int i)
    {
        var items = new List<CssValue>(2);
        while (true)
        {
            items.Add(ParseSequence(tokens, ref i));

            if (Peek(tokens, i).Type == CssTokenType.Comma) { i++; continue; }
            break;
        }
        return items.Count == 1 ? items[0] : new CssCommaList(items);
    }

    /// <summary>Parses a whitespace-separated run of component values.</summary>
    private static CssValue ParseSequence(List<CssToken> tokens, ref int i)
    {
        var parts = new List<CssValue>(4);

        while (true)
        {
            var t = Peek(tokens, i);
            if (t.Type is CssTokenType.Eof or CssTokenType.Comma or CssTokenType.CloseParen) break;

            if (t.Type == CssTokenType.Whitespace) { i++; continue; }

            parts.Add(ParseComponent(tokens, ref i));
        }

        if (parts.Count == 0) return new CssIdent("");
        return parts.Count == 1 ? parts[0] : new CssList(parts);
    }

    private static CssValue ParseComponent(List<CssToken> tokens, ref int i)
    {
        var t = tokens[i];
        switch (t.Type)
        {
            case CssTokenType.Number:
                i++;
                return new CssNumber(t.Number, t.Unit);

            case CssTokenType.Ident:
                i++;
                return new CssIdent(t.Text);

            case CssTokenType.String:
                i++;
                return new CssString(t.Text);

            case CssTokenType.Hash:
                i++;
                return CssColorParser.TryParseHex(t.Text, out var color)
                    ? color
                    : new CssIdent("#" + t.Text);

            case CssTokenType.Delim:
                i++;
                return new CssDelim(t.Delim);

            case CssTokenType.Function:
            {
                i++; // consume "name("
                var args = ParseArgs(tokens, ref i);
                Expect(tokens, ref i, CssTokenType.CloseParen);
                return new CssFunction(t.Text.ToLowerInvariant(), args);
            }

            case CssTokenType.OpenParen:
            {
                i++;
                var inner = ParseSequence(tokens, ref i);
                Expect(tokens, ref i, CssTokenType.CloseParen);
                return new CssParenGroup(inner);
            }

            default:
                i++;
                return new CssIdent("");
        }
    }

    /// <summary>Function arguments: comma-separated, each a whitespace-separated sequence.</summary>
    private static List<CssValue> ParseArgs(List<CssToken> tokens, ref int i)
    {
        var args = new List<CssValue>(3);

        // Empty arg list: `foo()`
        SkipWhitespace(tokens, ref i);
        if (Peek(tokens, i).Type == CssTokenType.CloseParen) return args;

        while (true)
        {
            args.Add(ParseSequence(tokens, ref i));
            if (Peek(tokens, i).Type == CssTokenType.Comma) { i++; continue; }
            break;
        }
        return args;
    }

    private static void SkipWhitespace(List<CssToken> tokens, ref int i)
    {
        while (Peek(tokens, i).Type == CssTokenType.Whitespace) i++;
    }

    private static void Expect(List<CssToken> tokens, ref int i, CssTokenType type)
    {
        if (Peek(tokens, i).Type == type) i++;
        // Tolerate truncated input rather than throwing: a malformed declaration should
        // degrade to "unmappable", not take down the whole stylesheet.
    }

    private static CssToken Peek(List<CssToken> tokens, int i) =>
        i < tokens.Count ? tokens[i] : new CssToken(CssTokenType.Eof);
}
