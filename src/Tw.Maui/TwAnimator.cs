using Tw.Core;

namespace Tw.Maui;

/// <summary>Looping animate-* keyframes, implemented with MAUI's Animation API.</summary>
internal static class TwKeyframeRunner
{
    private const string Handle = "TwKeyframes";

    private static readonly BindableProperty KindProperty =
        BindableProperty.CreateAttached("TwKeyframeKind", typeof(TwKeyframes), typeof(TwKeyframeRunner), TwKeyframes.None);

    /// <summary>True once Loaded/Unloaded lifecycle handlers are attached (wire once).</summary>
    private static readonly BindableProperty LifecycleWiredProperty =
        BindableProperty.CreateAttached("TwKeyframeLifecycle", typeof(bool), typeof(TwKeyframeRunner), false);

    /// <summary>
    /// Aborts a running loop and resets the property it was animating (a mid-cycle
    /// abort would otherwise leave Rotation/Opacity/TranslationY frozen at an
    /// arbitrary value — keyframe motion never appears in plan setters, so the
    /// reconciler can't clear it). Runs BEFORE the plan applies, so plan-declared
    /// rotate-*/opacity-* values land on top of the reset.
    /// </summary>
    public static TwKeyframes Stop(VisualElement element)
    {
        var previous = (TwKeyframes)element.GetValue(KindProperty);
        if (previous == TwKeyframes.None)
            return previous;

        element.AbortAnimation(Handle);
        switch (previous)
        {
            case TwKeyframes.Spin: element.Rotation = 0; break;
            case TwKeyframes.Pulse: element.Opacity = 1; break;
            case TwKeyframes.Bounce: element.TranslationY = 0; break;
        }
        element.SetValue(KindProperty, TwKeyframes.None);
        return previous;
    }

    public static void Run(VisualElement element, TwKeyframes kind)
    {
        element.SetValue(KindProperty, kind);
        EnsureLifecycleWired(element);
        Start(element, kind);
    }

    private static void Start(VisualElement element, TwKeyframes kind)
    {
        element.AbortAnimation(Handle);
        switch (kind)
        {
            case TwKeyframes.Spin:
                new Animation(v => element.Rotation = v, 0, 360)
                    .Commit(element, Handle, 16, 1000, Easing.Linear, repeat: () => true);
                break;

            case TwKeyframes.Pulse:
                var pulse = new Animation();
                pulse.Add(0, 0.5, new Animation(v => element.Opacity = v, 1, 0.5, Easing.CubicInOut));
                pulse.Add(0.5, 1, new Animation(v => element.Opacity = v, 0.5, 1, Easing.CubicInOut));
                pulse.Commit(element, Handle, 16, 2000, repeat: () => true);
                break;

            case TwKeyframes.Bounce:
                var bounce = new Animation();
                bounce.Add(0, 0.5, new Animation(v => element.TranslationY = v, 0, -10, Easing.CubicOut));
                bounce.Add(0.5, 1, new Animation(v => element.TranslationY = v, -10, 0, Easing.CubicIn));
                bounce.Commit(element, Handle, 16, 1000, repeat: () => true);
                break;
        }
    }

    /// <summary>
    /// A repeating animation's tick callback captures the element, so it would keep the
    /// element alive (and the ticker busy) after it leaves the visual tree. Abort on
    /// Unloaded to break that; resume on Loaded if the element is still meant to animate.
    /// Handlers live on the element itself, so they don't extend its lifetime.
    /// </summary>
    private static void EnsureLifecycleWired(VisualElement element)
    {
        if ((bool)element.GetValue(LifecycleWiredProperty))
            return;
        element.SetValue(LifecycleWiredProperty, true);

        element.Unloaded += (s, _) =>
        {
            if (s is VisualElement el)
                el.AbortAnimation(Handle); // stop ticking; KindProperty remembers what to resume
        };
        element.Loaded += (s, _) =>
        {
            if (s is VisualElement el && (TwKeyframes)el.GetValue(KindProperty) is var k and not TwKeyframes.None)
                Start(el, k);
        };
    }
}

/// <summary>
/// Interactive state events, reduced to their essence: each event flips one bit in
/// the reconciler's state vector and reconciles. No snapshots, no restore logic,
/// no parallel styling path — enter and exit are the same operation. Whether the
/// change tweens or snaps is the reconciler's call (plan has transition-* or not).
/// </summary>
internal static class TwInteractionWiring
{
    public const int PressedBit = 1, HoverBit = 2, FocusBit = 4, DisabledBit = 8;

    /// <summary>Bitmask of events already subscribed on this element.</summary>
    private static readonly BindableProperty WiredProperty =
        BindableProperty.CreateAttached("TwWired", typeof(int), typeof(TwInteractionWiring), 0);

    public static int BitOf(string visualState) => visualState switch
    {
        "Pressed" => PressedBit,
        "PointerOver" => HoverBit,
        "Focused" => FocusBit,
        _ => DisabledBit,
    };

    public static void Wire(VisualElement element, TwMauiPlan plan)
    {
        int needed = 0;
        foreach (var state in plan.States)
            needed |= BitOf(state.VisualState);

        int wired = (int)element.GetValue(WiredProperty);
        int missing = needed & ~wired;
        if (missing == 0)
            return;
        element.SetValue(WiredProperty, wired | needed);

        if ((missing & PressedBit) != 0)
        {
            switch (element)
            {
                case Button button:
                    button.Pressed += (s, _) => TwReconciler.SetState((VisualElement)s!, PressedBit, true);
                    button.Released += (s, _) => TwReconciler.SetState((VisualElement)s!, PressedBit, false);
                    break;
                case ImageButton imageButton:
                    imageButton.Pressed += (s, _) => TwReconciler.SetState((VisualElement)s!, PressedBit, true);
                    imageButton.Released += (s, _) => TwReconciler.SetState((VisualElement)s!, PressedBit, false);
                    break;
                case View view:
                    var press = new PointerGestureRecognizer();
                    press.PointerPressed += (_, _) => TwReconciler.SetState(element, PressedBit, true);
                    press.PointerReleased += (_, _) => TwReconciler.SetState(element, PressedBit, false);
                    view.GestureRecognizers.Add(press);
                    break;
            }
        }

        if ((missing & HoverBit) != 0 && element is View hoverView)
        {
            var hover = new PointerGestureRecognizer();
            hover.PointerEntered += (_, _) => TwReconciler.SetState(element, HoverBit, true);
            hover.PointerExited += (_, _) => TwReconciler.SetState(element, HoverBit, false);
            hoverView.GestureRecognizers.Add(hover);
        }

        if ((missing & FocusBit) != 0)
        {
            element.Focused += (s, _) => TwReconciler.SetState((VisualElement)s!, FocusBit, true);
            element.Unfocused += (s, _) => TwReconciler.SetState((VisualElement)s!, FocusBit, false);
        }

        if ((missing & DisabledBit) != 0)
        {
            element.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(VisualElement.IsEnabled) && s is VisualElement el)
                    TwReconciler.SetState(el, DisabledBit, !el.IsEnabled);
            };
            // Seed the current state: an initially-disabled element must get its
            // disabled: styling without waiting for a PropertyChanged that never comes.
            if (!element.IsEnabled)
                TwReconciler.SetState(element, DisabledBit, true);
        }
    }
}

internal static class TwColorMath
{
    public static Color Lerp(Color from, Color to, float t) => new(
        from.Red + (to.Red - from.Red) * t,
        from.Green + (to.Green - from.Green) * t,
        from.Blue + (to.Blue - from.Blue) * t,
        from.Alpha + (to.Alpha - from.Alpha) * t);

    public static Easing EasingOf(TwEasing easing) => easing switch
    {
        TwEasing.Linear => Easing.Linear,
        TwEasing.In => Easing.CubicIn,
        TwEasing.Out => Easing.CubicOut,
        _ => Easing.CubicInOut,
    };
}
