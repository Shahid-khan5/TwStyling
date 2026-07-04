using Tw.Core;

namespace Tw.Maui;

/// <summary>Looping animate-* keyframes, implemented with MAUI's Animation API.</summary>
internal static class TwKeyframeRunner
{
    private const string Handle = "TwKeyframes";

    public static void Run(VisualElement element, TwKeyframes kind)
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
}

/// <summary>
/// Event-driven replacement for VisualStateManager when a plan carries transition-*:
/// VSM setters snap, so instead we listen to the interaction events directly and
/// tween between base and state values. Wired once per element; the current plan
/// travels on an attached property so re-applies just swap it.
/// </summary>
internal static class TwInteractionAnimator
{
    private const string Handle = "TwState";

    private static readonly BindableProperty PlanProperty =
        BindableProperty.CreateAttached("TwAnimatorPlan", typeof(TwMauiPlan), typeof(TwInteractionAnimator), null);

    private static readonly BindableProperty WiredProperty =
        BindableProperty.CreateAttached("TwAnimatorWired", typeof(bool), typeof(TwInteractionAnimator), false);

    public static void Wire(VisualElement element, TwMauiPlan plan)
    {
        element.SetValue(PlanProperty, plan);
        if ((bool)element.GetValue(WiredProperty))
            return;
        element.SetValue(WiredProperty, true);

        bool hasPressed = Has(plan, "Pressed");
        bool hasHover = Has(plan, VisualStateManager.CommonStates.PointerOver);
        bool hasFocus = Has(plan, VisualStateManager.CommonStates.Focused);
        bool hasDisabled = Has(plan, VisualStateManager.CommonStates.Disabled);

        if (hasPressed)
        {
            switch (element)
            {
                case Button button:
                    button.Pressed += (s, _) => Enter((VisualElement)s!, "Pressed");
                    button.Released += (s, _) => Exit((VisualElement)s!, "Pressed");
                    break;
                case ImageButton imageButton:
                    imageButton.Pressed += (s, _) => Enter((VisualElement)s!, "Pressed");
                    imageButton.Released += (s, _) => Exit((VisualElement)s!, "Pressed");
                    break;
                case View view:
                    var press = new PointerGestureRecognizer();
                    press.PointerPressed += (s, _) => Enter(element, "Pressed");
                    press.PointerReleased += (s, _) => Exit(element, "Pressed");
                    view.GestureRecognizers.Add(press);
                    break;
            }
        }

        if (hasHover && element is View hoverView)
        {
            var hover = new PointerGestureRecognizer();
            hover.PointerEntered += (_, _) => Enter(element, VisualStateManager.CommonStates.PointerOver);
            hover.PointerExited += (_, _) => Exit(element, VisualStateManager.CommonStates.PointerOver);
            hoverView.GestureRecognizers.Add(hover);
        }

        if (hasFocus)
        {
            element.Focused += (s, _) => Enter((VisualElement)s!, VisualStateManager.CommonStates.Focused);
            element.Unfocused += (s, _) => Exit((VisualElement)s!, VisualStateManager.CommonStates.Focused);
        }

        if (hasDisabled)
        {
            element.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName != nameof(VisualElement.IsEnabled) || s is not VisualElement el)
                    return;
                if (el.IsEnabled)
                    Exit(el, VisualStateManager.CommonStates.Disabled);
                else
                    Enter(el, VisualStateManager.CommonStates.Disabled);
            };
        }
    }

    private static bool Has(TwMauiPlan plan, string state)
    {
        foreach (var s in plan.States)
            if (s.VisualState == state)
                return true;
        return false;
    }

    private static void Enter(VisualElement element, string state)
    {
        if (element.GetValue(PlanProperty) is not TwMauiPlan plan)
            return;
        bool dark = Application.Current?.RequestedTheme == AppTheme.Dark;

        foreach (var stateEntries in plan.States)
        {
            if (stateEntries.VisualState != state)
                continue;
            var targets = new List<(BindableProperty, object?)>(stateEntries.Entries.Length);
            foreach (var entry in stateEntries.Entries)
                targets.Add((entry.Property, TwMauiPlan.Materialize(dark ? entry.Dark : entry.Light)));
            AnimateTo(element, targets, plan);
            return;
        }
    }

    private static void Exit(VisualElement element, string state)
    {
        if (element.GetValue(PlanProperty) is not TwMauiPlan plan)
            return;
        bool dark = Application.Current?.RequestedTheme == AppTheme.Dark;

        foreach (var stateEntries in plan.States)
        {
            if (stateEntries.VisualState != state)
                continue;
            // Restore each touched property to its base-plan value (or default).
            var targets = new List<(BindableProperty, object?)>(stateEntries.Entries.Length);
            foreach (var entry in stateEntries.Entries)
            {
                object? baseValue = entry.Property.DefaultValue;
                foreach (var s in plan.Setters)
                {
                    if (s.Property == entry.Property)
                    {
                        baseValue = TwMauiPlan.Materialize(dark ? s.Dark : s.Light);
                        break;
                    }
                }
                targets.Add((entry.Property, baseValue));
            }
            AnimateTo(element, targets, plan);
            return;
        }
    }

    /// <summary>Tween colors and doubles toward targets; snap everything else.</summary>
    private static void AnimateTo(VisualElement element, List<(BindableProperty Property, object? Target)> targets, TwMauiPlan plan)
    {
        var spec = plan.Transition!.Value;
        var animation = new Animation();
        bool any = false;

        foreach (var (property, target) in targets)
        {
            object? current = element.GetValue(property);
            if (current is Color from && target is Color to && !from.Equals(to)
                && (spec.Props & TwTransitionProps.Colors) != 0)
            {
                animation.Add(0, 1, new Animation(v =>
                    element.SetValue(property, TwColorMath.Lerp(from, to, (float)v))));
                any = true;
            }
            else if (current is double d0 && target is double d1 && !d0.Equals(d1))
            {
                animation.Add(0, 1, new Animation(v => element.SetValue(property, v), d0, d1));
                any = true;
            }
            else if (!Equals(current, target))
            {
                element.SetValue(property, target);
            }
        }

        if (!any)
            return;

        element.AbortAnimation(Handle);
        animation.Commit(element, Handle, 16, (uint)spec.DurationMs, TwColorMath.EasingOf(spec.Easing));
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
