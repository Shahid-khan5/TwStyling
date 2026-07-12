namespace Tw.Css;

/// <summary>
/// Folds an <c>@media</c> prelude into a <see cref="CssContext"/>. Everything Tailwind emits is
/// statically decidable at build time; anything else makes the block un-applicable and is skipped.
/// </summary>
public static class CssMediaQuery
{
    /// <summary>Pixels per rem when resolving breakpoint bounds. Tailwind's breakpoints are in rem.</summary>
    public const double RemBase = 16.0;

    /// <summary>
    /// Returns false when the query names a condition we cannot evaluate, in which case the caller
    /// must skip the block rather than apply its declarations unconditionally.
    /// </summary>
    public static bool TryApply(string prelude, CssContext context, out CssContext result)
    {
        result = context;

        var body = prelude.Substring("@media".Length).Trim();
        if (body.Length == 0) return true;

        // We cannot statically satisfy a negated or comma-separated (union) query.
        if (body.StartsWith("not ", StringComparison.OrdinalIgnoreCase) || body.Contains(","))
            return false;

        double minWidth = context.MinWidth;
        double maxWidth = context.MaxWidth;
        string? scheme = context.ColorScheme;
        string? platform = context.Platform;

        foreach (var raw in SplitConditions(body))
        {
            var condition = raw.Trim();
            if (condition.Length == 0) continue;

            if (!condition.StartsWith("(", StringComparison.Ordinal))
            {
                // A bare identifier. `screen`/`all` are no-ops; anything else is a custom variant
                // media type that Tailwind passed through, e.g. `@media windows`.
                if (condition.Equals("screen", StringComparison.OrdinalIgnoreCase)
                    || condition.Equals("all", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (condition.Equals("print", StringComparison.OrdinalIgnoreCase))
                    return false;

                platform = condition.ToLowerInvariant();
                continue;
            }

            var inner = condition.Substring(1, condition.Length - 2).Trim();

            // Range syntax: `width >= 40rem`, `width < 48rem`.
            if (TryRange(inner, ref minWidth, ref maxWidth)) continue;

            int colon = inner.IndexOf(':');
            if (colon < 0) return false;

            var feature = inner.Substring(0, colon).Trim().ToLowerInvariant();
            var value = inner.Substring(colon + 1).Trim().ToLowerInvariant();

            switch (feature)
            {
                case "prefers-color-scheme":
                    scheme = value;
                    break;

                case "min-width":
                    minWidth = Math.Max(minWidth, ToPixels(value));
                    break;

                case "max-width":
                    maxWidth = Math.Min(maxWidth, ToPixels(value));
                    break;

                // Capability gates. Tailwind wraps `hover:` in `(hover: hover)` to keep touch
                // devices from sticking hover styles; a native element always has real states,
                // so these are satisfied rather than skipped.
                case "hover":
                case "pointer":
                case "any-hover":
                case "any-pointer":
                    break;

                default:
                    return false;
            }
        }

        result = new CssContext(minWidth, maxWidth, scheme, platform, context.Pseudo);
        return true;
    }

    private static bool TryRange(string inner, ref double minWidth, ref double maxWidth)
    {
        foreach (var op in new[] { ">=", "<=", ">", "<" })
        {
            int at = inner.IndexOf(op, StringComparison.Ordinal);
            if (at < 0) continue;

            var left = inner.Substring(0, at).Trim().ToLowerInvariant();
            var right = inner.Substring(at + op.Length).Trim();
            if (left != "width") continue;

            double px = ToPixels(right);
            switch (op)
            {
                case ">=": minWidth = Math.Max(minWidth, px); return true;
                case ">": minWidth = Math.Max(minWidth, px + 1); return true;
                case "<=": maxWidth = Math.Min(maxWidth, px); return true;
                case "<": maxWidth = Math.Min(maxWidth, px - 1); return true;
            }
        }
        return false;
    }

    private static double ToPixels(string value)
    {
        var parsed = CssValueParser.Parse(value);
        if (parsed is not CssNumber n) return 0;
        return n.Unit switch
        {
            CssUnit.Rem or CssUnit.Em => n.Value * RemBase,
            CssUnit.Pt => n.Value * 96.0 / 72.0,
            _ => n.Value,
        };
    }

    /// <summary>Splits on top-level <c>and</c>, leaving parenthesised groups intact.</summary>
    private static List<string> SplitConditions(string body)
    {
        var parts = new List<string>();
        int depth = 0, start = 0;

        for (int i = 0; i < body.Length; i++)
        {
            char c = body[i];
            if (c == '(') depth++;
            else if (c == ')') depth--;
            else if (depth == 0
                && i + 4 < body.Length
                && (i == 0 || char.IsWhiteSpace(body[i - 1]))
                && body.Substring(i, 3).Equals("and", StringComparison.OrdinalIgnoreCase)
                && char.IsWhiteSpace(body[i + 3]))
            {
                parts.Add(body.Substring(start, i - start));
                i += 3;
                start = i + 1;
            }
        }
        parts.Add(body.Substring(start));
        return parts;
    }
}
