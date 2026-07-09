namespace Tw.Core;

/// <summary>
/// A compiled, immutable class string: everything the adapter needs to style an
/// element, with all cold work (parsing, platform filtering, merging) already done.
/// One instance is shared by every element using the same class string.
/// </summary>
public sealed class StylePlan
{
    public static readonly StylePlan Empty = new([], [], [], [], []);

    /// <summary>
    /// Construction surface for source-generated (pre-lowered) plans. Generated code
    /// was validated at build time, so it carries no diagnostics.
    /// </summary>
    public static StylePlan Precompiled(
        TwDeclaration[] light, TwDeclaration[] dark, StatePlan[] states, BreakpointPlan[] breakpoints) =>
        new(light, dark, states, breakpoints, []);

    /// <summary>Declarations for the normal state, light theme.</summary>
    public readonly TwDeclaration[] Light;

    /// <summary>Declarations for the normal state, dark theme (full set, not a delta).</summary>
    public readonly TwDeclaration[] Dark;

    /// <summary>Interactive state deltas. Empty for purely static class strings.</summary>
    public readonly StatePlan[] States;

    /// <summary>
    /// Responsive overlays sorted by ascending MinWidth; the adapter applies every
    /// overlay whose MinWidth ≤ current window width, in order, on top of the base.
    /// </summary>
    public readonly BreakpointPlan[] Breakpoints;

    /// <summary>Problems found at compile time; also pushed to the engine's diagnostic sink.</summary>
    public readonly TwDiagnostic[] Diagnostics;

    /// <summary>False → the adapter can skip theme-change tracking for this plan.</summary>
    public bool DiffersByTheme { get; }

    public bool HasStates => States.Length > 0;

    internal StylePlan(TwDeclaration[] light, TwDeclaration[] dark, StatePlan[] states, BreakpointPlan[] breakpoints, TwDiagnostic[] diagnostics)
    {
        Light = light;
        Dark = dark;
        States = states;
        Breakpoints = breakpoints;
        Diagnostics = diagnostics;
        bool breakpointsDiffer = false;
        foreach (var bp in breakpoints)
            breakpointsDiffer |= !SameDeclarations(bp.Light, bp.Dark);
        DiffersByTheme = !SameDeclarations(light, dark) || StatesDifferByTheme(states) || breakpointsDiffer;
    }

    private static bool SameDeclarations(TwDeclaration[] a, TwDeclaration[] b)
    {
        if (a.Length != b.Length) return false;
        for (int i = 0; i < a.Length; i++)
        {
            if (a[i].Property != b[i].Property) return false;
            var (x, y) = (a[i].Value, b[i].Value);
            if (x.Kind != y.Kind || x.Rgba != y.Rgba
                || !FloatEq(x.X, y.X) || !FloatEq(x.Y, y.Y) || !FloatEq(x.Z, y.Z) || !FloatEq(x.W, y.W))
                return false;
        }
        return true;
    }

    private static bool FloatEq(float a, float b) =>
        a.Equals(b) || (float.IsNaN(a) && float.IsNaN(b));

    private static bool StatesDifferByTheme(StatePlan[] states)
    {
        foreach (var s in states)
            if (!SameDeclarations(s.Light, s.Dark))
                return true;
        return false;
    }
}

/// <summary>Delta declarations for one interactive state (on top of the normal-state plan).</summary>
public readonly struct StatePlan(TwInteractiveState state, TwDeclaration[] light, TwDeclaration[] dark)
{
    public readonly TwInteractiveState State = state;
    public readonly TwDeclaration[] Light = light;
    public readonly TwDeclaration[] Dark = dark;
}

/// <summary>Delta declarations applied when the window is at least MinWidth wide.</summary>
public readonly struct BreakpointPlan(float minWidth, TwDeclaration[] light, TwDeclaration[] dark)
{
    public readonly float MinWidth = minWidth;
    public readonly TwDeclaration[] Light = light;
    public readonly TwDeclaration[] Dark = dark;
}

internal static class StylePlanCompiler
{
    public static StylePlan Compile(string classes, in TwEnvironment env)
    {
        if (string.IsNullOrWhiteSpace(classes))
            return StylePlan.Empty;

        var diagnostics = new List<TwDiagnostic>();
        var items = TwParser.Parse(classes, diagnostics.Add);

        // Static filtering: utilities for other platforms/idioms vanish here and
        // never exist at runtime — platform variants cost nothing per element.
        var environment = env;
        items.RemoveAll(i => !i.Variants.AppliesTo(environment));

        // v1 restriction: breakpoints don't stack with interactive states yet.
        foreach (var item in items)
        {
            if (item.Variants.BreakpointMinWidth > 0 && item.Variants.State != TwInteractiveState.None)
                diagnostics.Add(new TwDiagnostic(classes, "",
                    "breakpoint variants (sm:/md:/…) cannot combine with interactive variants yet — the utility was ignored"));
        }
        items.RemoveAll(i => i.Variants.BreakpointMinWidth > 0 && i.Variants.State != TwInteractiveState.None);

        var light = MergeBucket(items, TwTheme.Light, TwInteractiveState.None, 0);
        var dark = MergeBucket(items, TwTheme.Dark, TwInteractiveState.None, 0);
        light = WithBorderDefault(light, baseBucket: null);
        dark = WithBorderDefault(dark, baseBucket: null);

        var states = new List<StatePlan>(4);
        foreach (var state in StateOrder)
        {
            var sLight = WithBorderDefault(MergeBucket(items, TwTheme.Light, state, 0), light);
            var sDark = WithBorderDefault(MergeBucket(items, TwTheme.Dark, state, 0), dark);
            if (sLight.Length > 0 || sDark.Length > 0)
                states.Add(new StatePlan(state, sLight, sDark));
        }

        var breakpointWidths = new List<float>();
        foreach (var item in items)
        {
            float w = item.Variants.BreakpointMinWidth;
            if (w > 0 && !breakpointWidths.Contains(w))
                breakpointWidths.Add(w);
        }
        breakpointWidths.Sort();

        var breakpoints = new List<BreakpointPlan>(breakpointWidths.Count);
        foreach (float width in breakpointWidths)
        {
            var bLight = MergeBucket(items, TwTheme.Light, TwInteractiveState.None, width);
            var bDark = MergeBucket(items, TwTheme.Dark, TwInteractiveState.None, width);
            if (bLight.Length > 0 || bDark.Length > 0)
                breakpoints.Add(new BreakpointPlan(width, bLight, bDark));
        }

        if (light.Length == 0 && dark.Length == 0 && states.Count == 0 && breakpoints.Count == 0 && diagnostics.Count == 0)
            return StylePlan.Empty;

        return new StylePlan(light, dark, states.ToArray(), breakpoints.ToArray(), diagnostics.ToArray());
    }

    /// <summary>
    /// Tailwind preflight: a border width without any border color gets gray-200.
    /// Applies to state buckets too — 'hover:border' must be visible — but only
    /// when neither the bucket nor its base bucket names a color.
    /// </summary>
    private static TwDeclaration[] WithBorderDefault(TwDeclaration[] bucket, TwDeclaration[]? baseBucket)
    {
        if (bucket.Length == 0
            || !Has(bucket, TwPropertyId.BorderWidth)
            || Has(bucket, TwPropertyId.BorderColor)
            || (baseBucket is not null && Has(baseBucket, TwPropertyId.BorderColor)))
            return bucket;

        var result = new TwDeclaration[bucket.Length + 1];
        bucket.CopyTo(result, 0);
        result[^1] = new TwDeclaration(TwPropertyId.BorderColor, TwValue.Color(TwTables.DefaultBorderColor));
        return result;

        static bool Has(TwDeclaration[] set, TwPropertyId id)
        {
            foreach (var d in set)
                if (d.Property == id)
                    return true;
            return false;
        }
    }

    /// <summary>
    /// Composition precedence (later wins when states are simultaneously active):
    /// on desktop, pressing implies hovering, so Pressed must come AFTER Hover or
    /// hover:bg-* would override pressed:bg-* while the mouse is down. This matches
    /// Tailwind's own emission order (active after hover). Disabled trumps all.
    /// </summary>
    private static readonly TwInteractiveState[] StateOrder =
    [
        TwInteractiveState.Hover,
        TwInteractiveState.Focus,
        TwInteractiveState.Pressed,
        TwInteractiveState.Disabled,
    ];

    /// <summary>
    /// Merges the declarations that apply for (theme, state). Qualified utilities
    /// beat unqualified ones regardless of token order, matching Tailwind's
    /// media-query semantics: unqualified first, then platform/idiom-qualified,
    /// then theme-qualified on top (dark:x beats x in dark mode; android:pt-6
    /// beats pt-2 on Android). Within a tier, source order applies and partial
    /// Edges/Corners values merge side-wise; everything else last-wins.
    /// </summary>
    private static TwDeclaration[] MergeBucket(List<ParsedItem> items, TwTheme theme, TwInteractiveState state, float breakpoint)
    {
        var merged = new List<TwDeclaration>(items.Count);

        AppendMatching(items, TwTheme.Any, state, breakpoint, merged, platformQualified: false);
        AppendMatching(items, TwTheme.Any, state, breakpoint, merged, platformQualified: true);
        AppendMatching(items, theme, state, breakpoint, merged, platformQualified: false);
        AppendMatching(items, theme, state, breakpoint, merged, platformQualified: true);

        return merged.Count == 0 ? [] : merged.ToArray();
    }

    private static void AppendMatching(List<ParsedItem> items, TwTheme theme, TwInteractiveState state, float breakpoint, List<TwDeclaration> merged, bool platformQualified)
    {
        foreach (var item in items)
        {
            if (item.Variants.Theme != theme || item.Variants.State != state
                || item.Variants.BreakpointMinWidth != breakpoint)
                continue;
            bool qualified = item.Variants.Platforms != TwPlatforms.Any || item.Variants.Idioms != TwIdioms.Any;
            if (qualified != platformQualified)
                continue;

            var decl = item.Declaration;
            int existing = IndexOf(merged, decl.Property);
            if (existing < 0)
            {
                merged.Add(decl);
            }
            else if (decl.Value.Kind is TwValueKind.Edges or TwValueKind.Corners)
            {
                merged[existing] = new TwDeclaration(decl.Property, MergeSides(merged[existing].Value, decl.Value));
            }
            else
            {
                merged[existing] = decl; // last wins
            }
        }
    }

    private static int IndexOf(List<TwDeclaration> list, TwPropertyId property)
    {
        for (int i = 0; i < list.Count; i++)
            if (list[i].Property == property)
                return i;
        return -1;
    }

    /// <summary>Overlay <paramref name="next"/> onto <paramref name="current"/>, keeping components next leaves NaN.</summary>
    private static TwValue MergeSides(TwValue current, TwValue next)
    {
        float x = float.IsNaN(next.X) ? current.X : next.X;
        float y = float.IsNaN(next.Y) ? current.Y : next.Y;
        float z = float.IsNaN(next.Z) ? current.Z : next.Z;
        float w = float.IsNaN(next.W) ? current.W : next.W;
        return current.Kind == TwValueKind.Edges
            ? TwValue.Edges(x, y, z, w)
            : TwValue.Corners(x, y, z, w);
    }
}
