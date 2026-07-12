using TwStyling;

namespace TwStyling.Maui;

/// <summary>
/// The single styling loop. Every dimension of change — class swap, breakpoint
/// crossing, interactive state enter/exit, IsActive toggle — updates one per-element
/// state object and calls <see cref="Reconcile"/>:
///
///   targets = base entries ⊕ breakpoint overlays (≤ window tier) ⊕ active state overlays
///   diff against what's currently applied → set changed, clear removed, done.
///
/// The steady-state path (same properties, changed values) allocates nothing of ours:
/// all mutable per-element data lives in one <see cref="State"/> object mutated in place
/// (no boxed-int attached-property writes for the generation/state vector, no ToArray
/// diff base), and the transient target/changed buffers are thread-static and reused.
/// Remaining per-restyle allocation is MAUI's own SetValue machinery.
///
/// There is no "restore" anywhere: exit paths don't remember old values, they just
/// recompute the full target set and diff. Theme is not part of the vector — every
/// theme-differing entry is applied as an AppThemeBinding (SetAppTheme), so OS theme
/// flips are handled by MAUI's binding machinery per property. When the plan carries
/// transition-*, the changed subset tweens instead of snapping (filtered by the spec's
/// property kinds), then finalizes with a real apply so bindings are re-established.
/// </summary>
internal static class TwReconciler
{
    private const string TweenHandle = "TwReconcile";

    /// <summary>All mutable per-element reconcile data, mutated in place.</summary>
    private sealed class State
    {
        public TwMauiPlan? Plan;
        /// <summary>The diff base — what is currently applied. Reused across reconciles.</summary>
        public readonly List<TwMauiPlan.Entry> Applied = new(8);
        /// <summary>Active interactive-state bitmask (TwInteractionWiring.*Bit).</summary>
        public int States;
        /// <summary>Bumped every reconcile; a delayed tween whose generation is stale
        /// (the vector changed during its delay) must not commit old targets.</summary>
        public int Generation;
        public bool HasApplied;
    }

    private static readonly BindableProperty StateProperty =
        BindableProperty.CreateAttached("TwReconcilerState", typeof(State), typeof(TwReconciler), null);

    // Transient scratch, reused across reconciles. Reconcile runs on the UI thread and
    // never re-enters itself (no property it sets re-triggers a reconcile), and these are
    // read only synchronously, so a single shared buffer per thread is safe and alloc-free.
    [ThreadStatic] private static List<TwMauiPlan.Entry>? _targets;
    [ThreadStatic] private static List<TwMauiPlan.Entry>? _changed;

    private static State GetOrCreate(VisualElement element)
    {
        if (element.GetValue(StateProperty) is State existing)
            return existing;
        var created = new State();
        element.SetValue(StateProperty, created);
        return created;
    }

    public static bool HasApplied(VisualElement element) =>
        element.GetValue(StateProperty) is State { HasApplied: true };

    public static void SetPlan(VisualElement element, TwMauiPlan plan, bool allowTween)
    {
        GetOrCreate(element).Plan = plan;
        Reconcile(element, allowTween);
    }

    public static void SetState(VisualElement element, int bit, bool active)
    {
        var state = GetOrCreate(element);
        int updated = active ? state.States | bit : state.States & ~bit;
        if (updated == state.States)
            return;
        state.States = updated;
        Reconcile(element, allowTween: true);
    }

    public static void ClearStates(VisualElement element)
    {
        if (element.GetValue(StateProperty) is State state)
            state.States = 0;
    }

    /// <summary>Window crossed a breakpoint tier (or the element just got a window).</summary>
    public static void EnvironmentChanged(VisualElement element) => Reconcile(element, allowTween: true);

    // ---------------------------------------------------------------- targets

    /// <summary>base ⊕ breakpoints(≤width) ⊕ active states — later layers override per property.</summary>
    private static void ComputeTargets(VisualElement element, TwMauiPlan plan, int states, List<TwMauiPlan.Entry> targets)
    {
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

        if (states != 0 && plan.States.Length > 0)
        {
            foreach (var state in plan.States)
                if ((states & TwInteractionWiring.BitOf(state.VisualState)) != 0)
                    foreach (var entry in state.Entries)
                        Overlay(targets, entry);
        }
    }

    private static void Overlay(List<TwMauiPlan.Entry> targets, TwMauiPlan.Entry entry)
    {
        for (int i = 0; i < targets.Count; i++)
        {
            if (ReferenceEquals(targets[i].Property, entry.Property))
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
        if (element.GetValue(StateProperty) is not State state || state.Plan is not { } plan)
            return;

        state.Generation++;

        var targets = _targets ??= new List<TwMauiPlan.Entry>(16);
        targets.Clear();
        ComputeTargets(element, plan, state.States, targets);

        var applied = state.Applied;

        // Removals: applied properties no target mentions go back to unset — ClearValue,
        // so style/binding-provided values resurface.
        for (int i = 0; i < applied.Count; i++)
        {
            var old = applied[i];
            if (IndexOfProperty(targets, old.Property) < 0)
                element.ClearValue(old.Property);
        }

        if (allowTween && state.HasApplied && plan.Transition is { } spec)
        {
            var changed = _changed ??= new List<TwMauiPlan.Entry>(16);
            changed.Clear();
            for (int i = 0; i < targets.Count; i++)
            {
                var target = targets[i];
                int j = IndexOfProperty(applied, target.Property);
                if (j < 0 || !SameValue(applied[j], target))
                    changed.Add(target);
            }
            if (changed.Count > 0)
                TweenTo(element, changed, spec, state);
        }
        else
        {
            for (int i = 0; i < targets.Count; i++)
            {
                var target = targets[i];
                int j = IndexOfProperty(applied, target.Property);
                if (j < 0 || !SameValue(applied[j], target))
                    ApplyEntry(element, target);
            }
        }

        // Targets become the new diff base — copy into the persistent, reused buffer.
        applied.Clear();
        applied.AddRange(targets);
        state.HasApplied = true;
    }

    private static int IndexOfProperty(List<TwMauiPlan.Entry> list, BindableProperty property)
    {
        for (int i = 0; i < list.Count; i++)
            if (ReferenceEquals(list[i].Property, property))
                return i;
        return -1;
    }

    private static bool SameValue(in TwMauiPlan.Entry a, in TwMauiPlan.Entry b) =>
        Equals(a.Light, b.Light) && Equals(a.Dark, b.Dark);

    /// <summary>Theme-differing entries ride an AppThemeBinding; the rest are plain values.</summary>
    private static void ApplyEntry(VisualElement element, in TwMauiPlan.Entry entry)
    {
        if (entry.Differs)
            element.SetAppTheme(entry.Property, TwMauiPlan.Materialize(entry.Light), TwMauiPlan.Materialize(entry.Dark));
        else
            element.SetValue(entry.Property, TwMauiPlan.Materialize(entry.Light));
    }

    // ---------------------------------------------------------------- tweening

    private static void TweenTo(VisualElement element, List<TwMauiPlan.Entry> changed, TwTransitionSpec spec, State state)
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

        int generation = state.Generation;
        void Commit()
        {
            // A newer reconcile ran during the delay-* window — these targets are stale.
            if (state.Generation != generation)
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
