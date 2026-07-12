namespace Tw.Css;

/// <summary>
/// Folds a parsed CSS value into a concrete one: substitutes <c>var()</c>, evaluates <c>calc()</c>
/// and the math functions, and resolves the color functions to <see cref="CssColor"/>.
/// Functions it does not understand are returned with their arguments evaluated, so a caller can
/// still inspect e.g. <c>repeat(3, minmax(0, 1fr))</c>.
/// </summary>
public static class CssEvaluator
{
    public static CssValue Evaluate(CssValue value, CssEnvironment env)
    {
        switch (value)
        {
            case CssNumber or CssColor or CssString or CssDelim:
                return value;

            case CssIdent ident:
                return ident.Name.Equals("currentcolor", StringComparison.OrdinalIgnoreCase) && env.CurrentColor is { } cc
                    ? cc
                    : ident;

            case CssParenGroup group:
                return new CssParenGroup(Evaluate(group.Inner, env));

            case CssList list:
                return new CssList(EvaluateEach(list.Items, env));

            case CssCommaList commaList:
                return new CssCommaList(EvaluateEach(commaList.Items, env));

            case CssFunction fn:
                return EvaluateFunction(fn, env);

            default:
                return value;
        }
    }

    private static List<CssValue> EvaluateEach(IReadOnlyList<CssValue> items, CssEnvironment env)
    {
        var result = new List<CssValue>(items.Count);
        for (int i = 0; i < items.Count; i++) result.Add(Evaluate(items[i], env));
        return result;
    }

    private static CssValue EvaluateFunction(CssFunction fn, CssEnvironment env)
    {
        switch (fn.Name)
        {
            case "var":
                return EvaluateVar(fn, env);

            case "calc":
                if (fn.Args.Count != 1) throw new CssEvalException("calc() takes exactly one expression");
                return EvalMath(Evaluate(fn.Args[0], env), env);

            case "min":
            case "max":
                return EvalMinMax(fn, env);

            case "clamp":
                return EvalClamp(fn, env);

            case "rgb":
            case "rgba":
                return EvalRgb(fn, env);

            case "hsl":
            case "hsla":
                return EvalHsl(fn, env);

            case "oklch":
                return EvalOklch(fn, env);

            case "oklab":
                return EvalOklab(fn, env);

            case "color-mix":
                return EvalColorMix(fn, env);

            default:
                // Unknown function: evaluate the arguments, keep the shape.
                return new CssFunction(fn.Name, EvaluateEach(fn.Args, env));
        }
    }

    // ------------------------------------------------------------------ var()

    private static CssValue EvaluateVar(CssFunction fn, CssEnvironment env)
    {
        if (fn.Args.Count == 0 || fn.Args[0] is not CssIdent name || !name.Name.StartsWith("--", StringComparison.Ordinal))
            throw new CssEvalException("var() expects a custom property name");

        if (env.TryGetVariable(name.Name, out var resolved)) return resolved;

        if (fn.Args.Count == 1)
            throw new CssEvalException($"undefined custom property '{name.Name}' with no fallback");

        // Per spec the fallback is everything after the first comma, commas included.
        var fallback = fn.Args.Count == 2
            ? fn.Args[1]
            : new CssCommaList(Slice(fn.Args, 1));

        return Evaluate(fallback, env);
    }

    private static List<CssValue> Slice(IReadOnlyList<CssValue> items, int start)
    {
        var list = new List<CssValue>(items.Count - start);
        for (int i = start; i < items.Count; i++) list.Add(items[i]);
        return list;
    }

    // ----------------------------------------------------------------- calc()

    /// <summary>Evaluates an already-substituted arithmetic expression to a single number.</summary>
    private static CssNumber EvalMath(CssValue value, CssEnvironment env)
    {
        var items = value switch
        {
            CssList l => l.Items,
            _ => new[] { value },
        };

        int i = 0;
        var result = ParseSum(items, ref i, env);
        if (i != items.Count) throw new CssEvalException("trailing tokens in calc()");
        return result;
    }

    private static CssNumber ParseSum(IReadOnlyList<CssValue> items, ref int i, CssEnvironment env)
    {
        var left = ParseProduct(items, ref i, env);

        while (i < items.Count && items[i] is CssDelim { Char: '+' or '-' } op)
        {
            i++;
            var right = ParseProduct(items, ref i, env);
            left = AddOrSub(left, right, op.Char == '+', env);
        }
        return left;
    }

    private static CssNumber ParseProduct(IReadOnlyList<CssValue> items, ref int i, CssEnvironment env)
    {
        var left = ParseOperand(items, ref i, env);

        while (i < items.Count && items[i] is CssDelim { Char: '*' or '/' } op)
        {
            i++;
            var right = ParseOperand(items, ref i, env);
            left = op.Char == '*' ? Multiply(left, right) : Divide(left, right);
        }
        return left;
    }

    private static CssNumber ParseOperand(IReadOnlyList<CssValue> items, ref int i, CssEnvironment env)
    {
        if (i >= items.Count) throw new CssEvalException("calc() ended early");

        var item = items[i++];
        return item switch
        {
            CssNumber n => n,
            CssIdent id when MathConstant(id.Name) is { } constant => new CssNumber(constant),
            CssParenGroup g => EvalMath(g.Inner, env),
            CssList l => EvalMath(l, env),
            CssFunction f => Evaluate(f, env) as CssNumber ?? throw new CssEvalException($"{f.Name}() is not a number in calc()"),
            _ => throw new CssEvalException($"'{item}' is not a valid calc() operand"),
        };
    }

    /// <summary>
    /// The CSS Values 4 math constants. Tailwind leans on <c>infinity</c> for its pill radius:
    /// <c>rounded-full</c> is <c>border-radius: calc(infinity * 1px)</c>.
    /// </summary>
    private static double? MathConstant(string name) => name.ToLowerInvariant() switch
    {
        "infinity" => double.PositiveInfinity,
        "-infinity" => double.NegativeInfinity,
        "nan" => double.NaN,
        "e" => Math.E,
        "pi" => Math.PI,
        _ => null,
    };

    private static CssNumber AddOrSub(CssNumber a, CssNumber b, bool add, CssEnvironment env)
    {
        // Zero is unit-agnostic, which keeps `calc(0 + 4px)` working.
        if (a.Value == 0 && a.Unit == CssUnit.None) return add ? b : new CssNumber(-b.Value, b.Unit);
        if (b.Value == 0 && b.Unit == CssUnit.None) return a;

        var (av, au) = Canonicalize(a, env);
        var (bv, bu) = Canonicalize(b, env);

        if (au != bu)
            throw new CssEvalException($"cannot {(add ? "add" : "subtract")} {a} and {b}: incompatible units");

        return new CssNumber(add ? av + bv : av - bv, au);
    }

    private static CssNumber Multiply(CssNumber a, CssNumber b)
    {
        if (a.Unit == CssUnit.None) return new CssNumber(a.Value * b.Value, b.Unit);
        if (b.Unit == CssUnit.None) return new CssNumber(a.Value * b.Value, a.Unit);
        throw new CssEvalException($"cannot multiply two dimensions: {a} * {b}");
    }

    private static CssNumber Divide(CssNumber a, CssNumber b)
    {
        if (b.Value == 0) throw new CssEvalException("division by zero in calc()");
        if (b.Unit == CssUnit.None) return new CssNumber(a.Value / b.Value, a.Unit);
        if (a.Unit == b.Unit) return new CssNumber(a.Value / b.Value, CssUnit.None);
        throw new CssEvalException($"cannot divide {a} by {b}");
    }

    /// <summary>Reduces a dimension to its canonical unit so two operands can be compared.</summary>
    private static (double Value, CssUnit Unit) Canonicalize(CssNumber n, CssEnvironment env) => n.Unit switch
    {
        CssUnit.Rem => (n.Value * env.RemBase, CssUnit.Px),
        CssUnit.Em => (n.Value * env.EmBase, CssUnit.Px),
        CssUnit.Pt => (n.Value * 96.0 / 72.0, CssUnit.Px),
        CssUnit.Rad => (n.Value * 180.0 / Math.PI, CssUnit.Deg),
        CssUnit.Turn => (n.Value * 360.0, CssUnit.Deg),
        CssUnit.Grad => (n.Value * 0.9, CssUnit.Deg),
        CssUnit.S => (n.Value * 1000.0, CssUnit.Ms),
        _ => (n.Value, n.Unit),
    };

    /// <summary>Reduces a number to pixels. Throws for units that are not lengths.</summary>
    public static double ToPixels(CssNumber n, CssEnvironment env)
    {
        var (v, u) = Canonicalize(n, env);
        return u switch
        {
            CssUnit.Px => v,
            CssUnit.None when v == 0 => 0,
            _ => throw new CssEvalException($"'{n}' is not a length"),
        };
    }

    private static CssValue EvalMinMax(CssFunction fn, CssEnvironment env)
    {
        if (fn.Args.Count == 0) throw new CssEvalException($"{fn.Name}() needs arguments");

        var first = EvalMath(Evaluate(fn.Args[0], env), env);
        var (bestV, unit) = Canonicalize(first, env);

        for (int i = 1; i < fn.Args.Count; i++)
        {
            var (v, u) = Canonicalize(EvalMath(Evaluate(fn.Args[i], env), env), env);
            if (u != unit) throw new CssEvalException($"{fn.Name}() arguments have incompatible units");
            bestV = fn.Name == "min" ? Math.Min(bestV, v) : Math.Max(bestV, v);
        }
        return new CssNumber(bestV, unit);
    }

    private static CssValue EvalClamp(CssFunction fn, CssEnvironment env)
    {
        if (fn.Args.Count != 3) throw new CssEvalException("clamp() takes three arguments");

        var (lo, u1) = Canonicalize(EvalMath(Evaluate(fn.Args[0], env), env), env);
        var (mid, u2) = Canonicalize(EvalMath(Evaluate(fn.Args[1], env), env), env);
        var (hi, u3) = Canonicalize(EvalMath(Evaluate(fn.Args[2], env), env), env);

        if (u1 != u2 || u2 != u3) throw new CssEvalException("clamp() arguments have incompatible units");
        return new CssNumber(Math.Max(lo, Math.Min(mid, hi)), u2);
    }

    // ---------------------------------------------------------------- colors

    /// <summary>Coerces an evaluated value to a color, resolving keywords.</summary>
    public static CssColor ToColor(CssValue value, CssEnvironment env)
    {
        switch (value)
        {
            case CssColor c: return c;
            case CssIdent id when id.Name.Equals("currentcolor", StringComparison.OrdinalIgnoreCase):
                return env.CurrentColor ?? throw new CssEvalException("currentcolor has no value in this scope");
            case CssIdent id when CssColorParser.TryParseNamed(id.Name, out var named):
                return named;
            default:
                throw new CssEvalException($"'{value}' is not a color");
        }
    }

    /// <summary>
    /// Flattens a color function's arguments into a component list. Handles both the legacy
    /// comma form <c>rgb(1, 2, 3)</c> and the modern space form <c>rgb(1 2 3 / 0.5)</c>.
    /// </summary>
    private static List<CssValue> Components(CssFunction fn, CssEnvironment env, out CssValue? alpha)
    {
        alpha = null;
        var flat = new List<CssValue>(4);

        foreach (var arg in fn.Args)
        {
            var evaluated = Evaluate(arg, env);
            if (evaluated is CssList list)
            {
                bool afterSlash = false;
                foreach (var item in list.Items)
                {
                    if (item is CssDelim { Char: '/' }) { afterSlash = true; continue; }
                    if (afterSlash) alpha = item;
                    else flat.Add(item);
                }
            }
            else flat.Add(evaluated);
        }

        // Legacy `rgba(r, g, b, a)` puts alpha in the 4th comma slot.
        if (alpha is null && flat.Count == 4) { alpha = flat[3]; flat.RemoveAt(3); }
        return flat;
    }

    private static double Alpha(CssValue? v) => v switch
    {
        null => 1.0,
        CssNumber { Unit: CssUnit.Percent } p => p.Value / 100.0,
        CssNumber n => n.Value,
        CssIdent { Name: "none" } => 0.0,
        _ => 1.0,
    };

    private static double Num(CssValue v) => v is CssNumber n ? n.Value
        : throw new CssEvalException($"'{v}' is not a number");

    /// <summary>An rgb channel: 0-255, or a percentage of 255.</summary>
    private static double Channel(CssValue v) => v switch
    {
        CssNumber { Unit: CssUnit.Percent } p => p.Value / 100.0,
        CssNumber n => n.Value / 255.0,
        _ => throw new CssEvalException($"'{v}' is not an rgb channel"),
    };

    private static CssValue EvalRgb(CssFunction fn, CssEnvironment env)
    {
        var c = Components(fn, env, out var alpha);
        if (c.Count < 3) throw new CssEvalException("rgb() needs three channels");
        return new CssColor(Channel(c[0]), Channel(c[1]), Channel(c[2]), Alpha(alpha));
    }

    private static CssValue EvalHsl(CssFunction fn, CssEnvironment env)
    {
        var c = Components(fn, env, out var alpha);
        if (c.Count < 3) throw new CssEvalException("hsl() needs three components");

        double h = c[0] is CssNumber hn ? Canonicalize(hn, env).Value : throw new CssEvalException("hsl() hue must be a number");
        return CssColorParser.FromHsl(h, Pct(c[1]), Pct(c[2]), Alpha(alpha));
    }

    /// <summary>A saturation/lightness component: a percentage, or a bare 0..1 number.</summary>
    private static double Pct(CssValue v) => v switch
    {
        CssNumber { Unit: CssUnit.Percent } p => p.Value / 100.0,
        CssNumber n => n.Value,
        _ => throw new CssEvalException($"'{v}' is not a percentage"),
    };

    private static CssValue EvalOklch(CssFunction fn, CssEnvironment env)
    {
        var c = Components(fn, env, out var alpha);
        if (c.Count < 3) throw new CssEvalException("oklch() needs lightness, chroma and hue");

        // Lightness: percentage of 1.0, or a bare 0..1 number.
        double l = Pct(c[0]);
        double chroma = c[1] is CssNumber { Unit: CssUnit.Percent } cp ? cp.Value / 100.0 * 0.4 : Num(c[1]);
        double hue = c[2] is CssNumber hn ? Canonicalize(hn, env).Value : 0;

        return CssColorParser.FromOklch(l, chroma, hue, Alpha(alpha));
    }

    private static CssValue EvalOklab(CssFunction fn, CssEnvironment env)
    {
        var c = Components(fn, env, out var alpha);
        if (c.Count < 3) throw new CssEvalException("oklab() needs lightness and two axes");

        double l = Pct(c[0]);
        double a = c[1] is CssNumber { Unit: CssUnit.Percent } ap ? ap.Value / 100.0 * 0.4 : Num(c[1]);
        double b = c[2] is CssNumber { Unit: CssUnit.Percent } bp ? bp.Value / 100.0 * 0.4 : Num(c[2]);

        return CssColorParser.FromOklab(l, a, b, Alpha(alpha));
    }

    private static CssValue EvalColorMix(CssFunction fn, CssEnvironment env)
    {
        if (fn.Args.Count != 3) throw new CssEvalException("color-mix() takes an interpolation method and two colors");

        // Arg 0 is `in <colorspace>`, possibly with a hue interpolation method we ignore.
        string space = "oklab";
        if (Evaluate(fn.Args[0], env) is CssList method)
        {
            for (int i = 0; i < method.Items.Count; i++)
                if (method.Items[i] is CssIdent { Name: "in" } && i + 1 < method.Items.Count && method.Items[i + 1] is CssIdent s)
                    space = s.Name.ToLowerInvariant();
        }

        // Oklch mixes are done in oklab; the hue-angle difference only shows up for
        // interpolations that cross a hue arc, which Tailwind never emits.
        if (space == "oklch") space = "oklab";
        if (space != "srgb" && space != "oklab")
            throw new CssEvalException($"unsupported color-mix() space '{space}'");

        var (c1, p1) = ColorAndPercent(fn.Args[1], env);
        var (c2, p2) = ColorAndPercent(fn.Args[2], env);

        return CssColorParser.Mix(space, c1, p1, c2, p2);
    }

    private static (CssColor Color, double? Percent) ColorAndPercent(CssValue arg, CssEnvironment env)
    {
        var evaluated = Evaluate(arg, env);

        if (evaluated is CssList list)
        {
            CssValue? colorPart = null;
            double? percent = null;

            foreach (var item in list.Items)
            {
                if (item is CssNumber { Unit: CssUnit.Percent } p) percent = p.Value / 100.0;
                else colorPart = item;
            }
            if (colorPart is null) throw new CssEvalException("color-mix() argument has no color");
            return (ToColor(colorPart, env), percent);
        }

        return (ToColor(evaluated, env), null);
    }
}
