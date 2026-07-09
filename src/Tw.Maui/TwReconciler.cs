using Tw.Core;

namespace Tw.Maui;

/// <summary>
/// The single styling loop. Every dimension of change — class swap, breakpoint
/// crossing, interactive state enter/exit, IsActive toggle — updates this element
/// state vector and calls <see cref="Reconcile"/>:
///
///   targets = base entries ⊕ breakpoint overlays (≤ window tier) ⊕ active state overlays
///   diff against what's currently applied → set changed, clear removed, done.
///
/// There is no "restore" anywhere: exit paths don't remember old values, they just
/// recompute the full target set and diff. Theme is not part of the vector at all —
/// every theme-differing entry is applied as an AppThemeBinding (SetAppTheme), so
/// OS theme flips are handled by MAUI's own binding machinery per property.
/// When the plan carries transition-*, the changed subset tweens instead of snapping
/// (filtered by the spec's property kinds), then finalizes with a real apply so
/// bindings are re-established.
/// </summary>
internal static class TwReconciler
{
    private const string TweenHandle = "TwReconcile";

    /// <summary>The lowered plan this element is currently styled by.</summary>
    private static readonly BindableProperty PlanProperty =
        BindableProperty.CreateAttached("TwReconcilerPlan", typeof(TwMauiPlan), typeof(TwReconciler), null);

    /// <summary>What is currently applied — the diff base. Entry values are shared with the cached plan.</summary>
    private static readonly BindableProperty AppliedProperty =
        BindableProperty.CreateAttached("TwReconcilerApplied", typeof(TwMauiPlan.Entry[]), typeof(TwReconciler), null);

    /// <summary>Bitmask of active interactive states (bits = TwInteractionWiring.*Bit).</summary>
    private static readonly BindableProperty StatesProperty =
        BindableProperty.CreateAttached("TwReconcilerStates", typeof(int), typeof(TwReconciler), 0);

    /// <summary>Bumped on every reconcile; a delay-* scheduled tween whose generation
    /// is stale (the vector changed during the delay) must not commit old targets.</summary>
    private static readonly BindableProperty GenerationProperty =
        BindableProperty.CreateAttached("TwReconcilerGeneration", typeof(int), typeof(TwReconciler), 0);

    public static bool HasApplied(VisualElement element) => element.GetValue(AppliedProperty) is not null;

    public static void SetPlan(VisualElement element, TwMauiPlan plan, bool allowTween)
    {
        element.SetValue(PlanProperty, plan);
        Reconcile(element, allowTween);
    }

    public static void SetState(VisualElement element, int bit, bool active)
    {
        int states = (int)element.GetValue(StatesProperty);
        int updated = active ? states | bit : states & ~bit;
        if (updated == states)
            return;
        element.SetValue(StatesProperty, updated);
        Reconcile(element, allowTween: true);
    }

    public static void ClearStates(VisualElement element) => element.SetValue(StatesProperty, 0);

    /// <summary>Window crossed a breakpoint tier (or the element just got a window).</summary>
    public static void EnvironmentChanged(VisualElement element) => Reconcile(element, allowTween: true);

    // ---------------------------------------------------------------- targets

    /// <summary>base ⊕ breakpoints(≤width) ⊕ active states — later layers override per property.</summary>
    private static List<TwMauiPlan.Entry> ComputeTargets(VisualElement element, TwMauiPlan plan)
    {
        var targets = new List<TwMauiPlan.Entry>(plan.Setters.Length + 4);
        foreach (var entry in plan.Setters)
            Overlay(targets, entry);

        if (plan.Breakpoints.Length > 0)
        {
            double width = element.Window?.Width
                ?? Application.Current?.Windows.FirstOrDefault()?.Width
                ?? double.NaN;
            if (!double.IsNaN(width))
            {
                foreach (var (minWidth, entries) in plan.Breakpoints)
                    if (width >= minWidth)
                        foreach (var entry in entries)
                            Overlay(targets, entry);
            }
        }

        int states = (int)element.GetValue(StatesProperty);
        if (states != 0 && plan.States.Length > 0)
        {
            foreach (var state in plan.States)
                if ((states & TwInteractionWiring.BitOf(state.VisualState)) != 0)
                    foreach (var entry in state.Entries)
                        Overlay(targets, entry);
        }

        return targets;
    }

    private static void Overlay(List<TwMauiPlan.Entry> targets, TwMauiPlan.Entry entry)
    {
        for (int i = 0; i < targets.Count; i++)
        {
            if (targets[i].Property == entry.Property)
            {
                targets[i] = entry;
                return;
            }
        }
        targets.Add(entry);
    }

    // ---------------------------------------------------------------- reconcile

    public static void Reconcile(VisualElement element, bool allowTween)
    {
        if (element.GetValue(PlanProperty) is not TwMauiPlan plan)
            return;

        element.SetValue(GenerationProperty, (int)element.GetValue(GenerationProperty) + 1);

        var targets = ComputeTargets(element, plan);
        var applied = element.GetValue(AppliedProperty) as TwMauiPlan.Entry[] ?? [];

        // Removals: applied properties no target mentions go back to unset —
        // ClearValue, so style/binding-provided values resurface.
        foreach (var old in applied)
        {
            bool present = false;
            foreach (var target in targets)
                if (target.Property == old.Property) { present = true; break; }
            if (!present)
                element.ClearValue(old.Property);
        }

        // Changes: anything new or with different values.
        var changed = new List<TwMauiPlan.Entry>();
        foreach (var target in targets)
        {
            bool same = false;
            foreach (var old in applied)
            {
                if (old.Property == target.Property)
                {
                    same = Equals(old.Light, target.Light) && Equals(old.Dark, target.Dark);
                    break;
                }
            }
            if (!same)
                changed.Add(target);
        }

        element.SetValue(AppliedProperty, targets.ToArray());

        if (changed.Count == 0)
            return;

        if (allowTween && plan.Transition is { } spec && applied.Length > 0)
            TweenTo(element, changed, spec);
        else
            foreach (var entry in changed)
                ApplyEntry(element, entry);
    }

    /// <summary>Theme-differing entries ride an AppThemeBinding; the rest are plain values.</summary>
    private static void ApplyEntry(VisualElement element, in TwMauiPlan.Entry entry)
    {
        if (entry.Differs)
            element.SetAppTheme(entry.Property, TwMauiPlan.Materialize(entry.Light), TwMauiPlan.Materialize(entry.Dark));
        else
            element.SetValue(entry.Property, TwMauiPlan.Materialize(entry.Light));
    }

    // ---------------------------------------------------------------- tweening

    private static void TweenTo(VisualElement element, List<TwMauiPlan.Entry> changed, TwTransitionSpec spec)
    {
        bool dark = Application.Current?.RequestedTheme == AppTheme.Dark;
        var animation = new Animation();
        var finalize = new List<TwMauiPlan.Entry>();
        bool any = false;

        foreach (var entry in changed)
        {
            var property = entry.Property;
            object? current = element.GetValue(property);
            object? target = TwMauiPlan.Materialize(dark ? entry.Dark : entry.Light);

            if (Tweenable(spec.Props, property, current, target))
            {
                if (current is Color fromColor && target is Color toColor)
                {
                    var p = property;
                    animation.Add(0, 1, new Animation(v =>
                        element.SetValue(p, TwColorMath.Lerp(fromColor, toColor, (float)v))));
                }
                else
                {
                    var p = property;
                    animation.Add(0, 1, new Animation(v => element.SetValue(p, v), (double)current!, (double)target!));
                }
                finalize.Add(entry);
                any = true;
            }
            else
            {
                ApplyEntry(element, entry); // not animatable under this spec — snap
            }
        }

        if (!any)
            return;

        int generation = (int)element.GetValue(GenerationProperty);
        void Commit()
        {
            // A newer reconcile ran during the delay-* window — these targets are stale.
            if ((int)element.GetValue(GenerationProperty) != generation)
                return;
            element.AbortAnimation(TweenHandle);
            animation.Commit(element, TweenHandle, 16, (uint)spec.DurationMs, TwColorMath.EasingOf(spec.Easing),
                finished: (_, _) =>
                {
                    // Tweens wrote manual values; the real apply re-establishes
                    // AppThemeBindings and exact target values.
                    foreach (var entry in finalize)
                        ApplyEntry(element, entry);
                });
        }

        if (spec.DelayMs > 0)
            element.Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(spec.DelayMs), Commit);
        else
            Commit();
    }

    /// <summary>Which changed values animate, per the transition-* spec's kinds.</summary>
    private static bool Tweenable(TwTransitionProps props, BindableProperty property, object? current, object? target)
    {
        if (current is Color && target is Color)
            return (props & TwTransitionProps.Colors) != 0 && !Equals(current, target);
        if (current is double from && target is double to && !from.Equals(to))
        {
            if (property == VisualElement.OpacityProperty)
                return (props & TwTransitionProps.Opacity) != 0;
            if (property == VisualElement.TranslationXProperty || property == VisualElement.TranslationYProperty
                || property == VisualElement.RotationProperty || property == VisualElement.ScaleProperty)
                return (props & TwTransitionProps.Transform) != 0;
            return (props & TwTransitionProps.Sizes) != 0; // font size, width/height requests, …
        }
        return false;
    }
}
