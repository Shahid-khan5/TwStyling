using System.Diagnostics;
using Tw.Core;

namespace Tw.Maui;

/// <summary>
/// The XAML entry point. Usage:
/// <code>
/// &lt;Border tw:Tw.Class="bg-white dark:bg-slate-800 rounded-2xl shadow-md p-6" /&gt;
/// </code>
/// with <c>xmlns:tw="clr-namespace:Tw.Maui;assembly=Tw.Maui"</c> (or the URL form
/// <c>xmlns:tw="https://tw"</c>).
/// </summary>
public static class Tw
{
    /// <summary>Tailwind utility classes to apply to this element.</summary>
    public static readonly BindableProperty ClassProperty = BindableProperty.CreateAttached(
        "Class", typeof(string), typeof(Tw), null, propertyChanged: OnStylingChanged);

    /// <summary>
    /// Extra classes appended (last-wins) while <see cref="IsActiveProperty"/> is true.
    /// Bind IsActive to a view-model boolean for state-driven styling:
    /// <code>
    /// &lt;Label tw:Tw.Class="text-sm text-slate-500 transition-all duration-200"
    ///        tw:Tw.ActiveClass="text-2xl text-red-500 font-bold"
    ///        tw:Tw.IsActive="{Binding IsImportant}" /&gt;
    /// </code>
    /// With transition-* in the base classes the switch animates.
    /// </summary>
    public static readonly BindableProperty ActiveClassProperty = BindableProperty.CreateAttached(
        "ActiveClass", typeof(string), typeof(Tw), null, propertyChanged: OnStylingChanged);

    /// <summary>Toggles <see cref="ActiveClassProperty"/>. Bindable (e.g. to a view-model bool).</summary>
    public static readonly BindableProperty IsActiveProperty = BindableProperty.CreateAttached(
        "IsActive", typeof(bool), typeof(Tw), false, propertyChanged: OnStylingChanged);

    public static string? GetClass(BindableObject bindable) => (string?)bindable.GetValue(ClassProperty);
    public static void SetClass(BindableObject bindable, string? value) => bindable.SetValue(ClassProperty, value);

    public static string? GetActiveClass(BindableObject bindable) => (string?)bindable.GetValue(ActiveClassProperty);
    public static void SetActiveClass(BindableObject bindable, string? value) => bindable.SetValue(ActiveClassProperty, value);

    public static bool GetIsActive(BindableObject bindable) => (bool)bindable.GetValue(IsActiveProperty);
    public static void SetIsActive(BindableObject bindable, bool value) => bindable.SetValue(IsActiveProperty, value);

    /// <summary>Class + ActiveClass (when active). Both compositions are cached plans.</summary>
    internal static string? EffectiveClass(BindableObject bindable)
    {
        var classes = GetClass(bindable);
        if (!GetIsActive(bindable) || GetActiveClass(bindable) is not { Length: > 0 } active)
            return classes;
        return string.IsNullOrEmpty(classes) ? active : $"{classes} {active}";
    }

    private static void OnStylingChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is VisualElement element)
            TwRuntime.Apply(element, EffectiveClass(bindable));
        else
            TwRuntime.ReportDiagnostic(new TwDiagnostic(EffectiveClass(bindable) ?? "", "",
                $"Tw styling is only supported on VisualElement, not {bindable.GetType().Name}"));
    }
}

/// <summary>Fluent API for C# markup: <c>new Label().Tw("text-xl font-bold")</c>.</summary>
public static class TwExtensions
{
    public static T Tw<T>(this T element, string classes) where T : BindableObject
    {
        element.SetValue(Maui.Tw.ClassProperty, classes);
        return element;
    }
}

/// <summary>How compile-time problems in class strings surface at runtime.</summary>
public enum TwDiagnosticMode
{
    /// <summary>Write to <see cref="Debug"/> output (default).</summary>
    DebugOutput,
    /// <summary>Throw immediately — recommended for CI/UI-test builds so bad classes fail loudly.</summary>
    Throw,
    /// <summary>Diagnostics go only to <see cref="TwRuntime.DiagnosticSink"/>.</summary>
    Silent,
}

/// <summary>Engine host for the MAUI adapter. Configured via <see cref="TwAppBuilderExtensions.UseTw"/>.</summary>
public static class TwRuntime
{
    private static TwEngine? _engine;
    private static readonly object InitLock = new();

    public static TwDiagnosticMode DiagnosticMode { get; set; } = TwDiagnosticMode.DebugOutput;

    /// <summary>Optional extra sink for diagnostics (logging, telemetry).</summary>
    public static Action<TwDiagnostic>? DiagnosticSink { get; set; }

    /// <summary>The shared engine; environment is detected from the device on first use.</summary>
    public static TwEngine Engine
    {
        get
        {
            if (_engine is { } e) return e;
            lock (InitLock)
            {
                return _engine ??= new TwEngine(DetectEnvironment(), ReportDiagnostic);
            }
        }
    }

    private static TwEnvironment DetectEnvironment()
    {
        var platform = DeviceInfo.Platform;
        var p = platform == DevicePlatform.Android ? TwPlatforms.Android
            : platform == DevicePlatform.iOS ? TwPlatforms.Ios
            : platform == DevicePlatform.MacCatalyst || platform == DevicePlatform.macOS ? TwPlatforms.Mac
            : platform == DevicePlatform.WinUI ? TwPlatforms.Windows
            : platform == DevicePlatform.Tizen ? TwPlatforms.Tizen
            : TwPlatforms.Any;

        var idiom = DeviceInfo.Idiom;
        var i = idiom == DeviceIdiom.Phone ? TwIdioms.Phone
            : idiom == DeviceIdiom.Tablet ? TwIdioms.Tablet
            : idiom == DeviceIdiom.Desktop ? TwIdioms.Desktop
            : idiom == DeviceIdiom.TV ? TwIdioms.Tv
            : idiom == DeviceIdiom.Watch ? TwIdioms.Watch
            : TwIdioms.Any;

        return new TwEnvironment(p, i);
    }

    internal static void ReportDiagnostic(TwDiagnostic diagnostic)
    {
        DiagnosticSink?.Invoke(diagnostic);
        switch (DiagnosticMode)
        {
            case TwDiagnosticMode.Throw:
                throw new InvalidOperationException(diagnostic.ToString());
            case TwDiagnosticMode.DebugOutput:
                Debug.WriteLine(diagnostic.ToString());
                break;
        }
    }

    // ------------------------------------------------------------------ apply

    /// <summary>Tracks whether an element has been styled before (drives transitions).</summary>
    private static readonly BindableProperty AppliedProperty =
        BindableProperty.CreateAttached("TwApplied", typeof(bool), typeof(TwRuntime), false);

    internal static void Apply(VisualElement element, string? classes)
    {
        var plan = Engine.GetPlan(classes);
        if (ReferenceEquals(plan, StylePlan.Empty))
            return;

        var mauiPlan = TwMauiPlan.Get(plan, element.GetType());

        bool reapply = (bool)element.GetValue(AppliedProperty);
        element.SetValue(AppliedProperty, true);

        if (reapply && mauiPlan.Transition is { } transition)
            ApplyWithTransition(element, mauiPlan, transition);
        else
            ApplyPlan(element, mauiPlan);

        if (mauiPlan.Keyframes != TwKeyframes.None)
            TwKeyframeRunner.Run(element, mauiPlan.Keyframes);
        else if (reapply)
            TwKeyframeRunner.Run(element, TwKeyframes.None); // abort a loop the old classes started

        // VSM setters hold raw values (state setters AND the Normal-state restore
        // values), so any element that has visual states and any theme-dependent
        // value needs a full re-apply when the theme flips — otherwise leaving
        // Pressed after a theme change restores the old theme's color.
        if (mauiPlan.HasStates && (mauiPlan.AnyStateDiffersByTheme || mauiPlan.AnySetterDiffersByTheme))
            ThemeTracker.Track(element);

        if (mauiPlan.Breakpoints.Length > 0)
            SizeTracker.Track(element);
    }

    private static void ApplyPlan(VisualElement element, TwMauiPlan plan)
    {
        ApplyEntries(element, plan.Setters);

        // Responsive overlays: every tier at or below the current window width, in order.
        if (plan.Breakpoints.Length > 0)
        {
            double width = element.Window?.Width
                ?? Application.Current?.Windows.FirstOrDefault()?.Width
                ?? double.NaN;
            if (!double.IsNaN(width))
            {
                foreach (var (minWidth, entries) in plan.Breakpoints)
                    if (width >= minWidth)
                        ApplyEntries(element, entries);
            }
        }

        if (plan.HasStates)
        {
            // With transition-*, VSM would snap between states — use the event-driven
            // animator instead so pressed/hover/focus changes tween.
            if (plan.Transition is not null)
                TwInteractionAnimator.Wire(element, plan);
            else
                ApplyVisualStates(element, plan);
        }
    }

    private static void ApplyEntries(VisualElement element, TwMauiPlan.Entry[] entries)
    {
        foreach (var entry in entries)
        {
            // Element-typed values (shapes/brushes/shadows) are wrapped in FreshValue
            // factories so every element gets its own instance — never shared parents.
            if (entry.Differs)
                element.SetAppTheme(entry.Property, TwMauiPlan.Materialize(entry.Light), TwMauiPlan.Materialize(entry.Dark));
            else
                element.SetValue(entry.Property, TwMauiPlan.Materialize(entry.Light));
        }
    }

    /// <summary>
    /// Re-apply with animation: capture animatable values, apply the plan instantly,
    /// then run the changed values from old to new. Covers opacity, transforms, and
    /// background color — the properties transition-* names.
    /// </summary>
    private static void ApplyWithTransition(VisualElement element, TwMauiPlan plan, TwTransitionSpec spec)
    {
        var type = element.GetType();
        var textColorProperty = (spec.Props & TwTransitionProps.Colors) != 0 ? TwProps.For(type, TwProps.TextColor) : null;
        var fontSizeProperty = (spec.Props & TwTransitionProps.Sizes) != 0 ? TwProps.For(type, TwProps.FontSize) : null;

        double preOpacity = element.Opacity, preTx = element.TranslationX, preTy = element.TranslationY;
        double preScale = element.Scale, preRotation = element.Rotation;
        double preWidth = element.WidthRequest, preHeight = element.HeightRequest;
        var preBackground = element.BackgroundColor;
        var preTextColor = textColorProperty is null ? null : element.GetValue(textColorProperty) as Color;
        double preFontSize = fontSizeProperty is null ? 0 : (double)element.GetValue(fontSizeProperty)!;

        ApplyPlan(element, plan);

        var animation = new Animation();
        bool any = false;

        if ((spec.Props & TwTransitionProps.Opacity) != 0)
            any |= Tween(animation, preOpacity, element.Opacity, v => element.Opacity = v);
        if ((spec.Props & TwTransitionProps.Transform) != 0)
        {
            any |= Tween(animation, preTx, element.TranslationX, v => element.TranslationX = v);
            any |= Tween(animation, preTy, element.TranslationY, v => element.TranslationY = v);
            any |= Tween(animation, preScale, element.Scale, v => element.Scale = v);
            any |= Tween(animation, preRotation, element.Rotation, v => element.Rotation = v);
        }
        if ((spec.Props & TwTransitionProps.Colors) != 0)
        {
            any |= TweenColor(animation, element, null, preBackground, element.BackgroundColor);
            if (textColorProperty is not null)
                any |= TweenColor(animation, element, textColorProperty, preTextColor, element.GetValue(textColorProperty) as Color);
        }
        if (fontSizeProperty is not null)
        {
            double postFontSize = (double)element.GetValue(fontSizeProperty)!;
            any |= Tween(animation, preFontSize, postFontSize, v => element.SetValue(fontSizeProperty, v));
        }
        if ((spec.Props & TwTransitionProps.Sizes) != 0)
        {
            // Explicit size requests tween too (skip the -1 "unset" sentinel).
            if (preWidth >= 0 && element.WidthRequest >= 0)
                any |= Tween(animation, preWidth, element.WidthRequest, v => element.WidthRequest = v);
            if (preHeight >= 0 && element.HeightRequest >= 0)
                any |= Tween(animation, preHeight, element.HeightRequest, v => element.HeightRequest = v);
        }

        if (!any)
            return;

        void Commit() => animation.Commit(element, "TwTransition", 16, (uint)spec.DurationMs, TwColorMath.EasingOf(spec.Easing));
        if (spec.DelayMs > 0)
            element.Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(spec.DelayMs), Commit);
        else
            Commit();
    }

    private static bool Tween(Animation animation, double from, double to, Action<double> setter)
    {
        if (from.Equals(to))
            return false;
        setter(from);
        animation.Add(0, 1, new Animation(setter, from, to));
        return true;
    }

    /// <summary>Property null → BackgroundColor (a plain CLR property, not SetValue-able generically here).</summary>
    private static bool TweenColor(Animation animation, VisualElement element, BindableProperty? property, Color? from, Color? to)
    {
        if (from is null || to is null || from.Equals(to))
            return false;
        if (property is null)
        {
            element.BackgroundColor = from;
            animation.Add(0, 1, new Animation(v => element.BackgroundColor = TwColorMath.Lerp(from, to, (float)v)));
        }
        else
        {
            element.SetValue(property, from);
            animation.Add(0, 1, new Animation(v => element.SetValue(property, TwColorMath.Lerp(from, to, (float)v))));
        }
        return true;
    }

    private static void ApplyVisualStates(VisualElement element, TwMauiPlan plan)
    {
        bool dark = Application.Current?.RequestedTheme == AppTheme.Dark;

        var normal = new VisualState { Name = VisualStateManager.CommonStates.Normal };
        var group = new VisualStateGroup { Name = "CommonStates" };
        group.States.Add(normal);

        var restored = new HashSet<BindableProperty>();
        foreach (var state in plan.States)
        {
            var vs = new VisualState { Name = state.VisualState };
            foreach (var entry in state.Entries)
            {
                vs.Setters.Add(new Setter
                {
                    Property = entry.Property,
                    Value = TwMauiPlan.Materialize(dark ? entry.Dark : entry.Light),
                });

                // Normal must restore every property another state touches.
                if (restored.Add(entry.Property))
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
                    normal.Setters.Add(new Setter { Property = entry.Property, Value = baseValue });
                }
            }
            group.States.Add(vs);
        }

        var groups = new VisualStateGroupList { group };
        VisualStateManager.SetVisualStateGroups(element, groups);
    }

    /// <summary>Re-applies tracked elements when the OS theme flips (VSM values are raw, not bindings).</summary>
    private static class ThemeTracker
    {
        private static readonly List<WeakReference<VisualElement>> Tracked = [];
        private static bool _subscribed;

        public static void Track(VisualElement element)
        {
            lock (Tracked)
            {
                foreach (var wr in Tracked)
                    if (wr.TryGetTarget(out var t) && ReferenceEquals(t, element))
                        return;
                Tracked.Add(new WeakReference<VisualElement>(element));

                if (!_subscribed && Application.Current is { } app)
                {
                    app.RequestedThemeChanged += OnThemeChanged;
                    _subscribed = true;
                }
            }
        }

        private static void OnThemeChanged(object? sender, AppThemeChangedEventArgs e)
        {
            List<VisualElement> alive = [];
            lock (Tracked)
            {
                Tracked.RemoveAll(wr => !wr.TryGetTarget(out _));
                foreach (var wr in Tracked)
                    if (wr.TryGetTarget(out var el))
                        alive.Add(el);
            }
            foreach (var el in alive)
                Apply(el, Tw.EffectiveClass(el));
        }
    }

    /// <summary>
    /// Re-applies elements with responsive breakpoints when their window crosses a
    /// breakpoint tier. Cheap: re-apply is cached-plan lookups plus SetValue calls.
    /// </summary>
    private static class SizeTracker
    {
        private static readonly List<WeakReference<VisualElement>> Tracked = [];
        private static readonly HashSet<Window> Subscribed = [];
        private static int _lastTier = -1;

        private static readonly float[] Tiers = [640, 768, 1024, 1280, 1536];

        public static void Track(VisualElement element)
        {
            lock (Tracked)
            {
                bool known = false;
                foreach (var wr in Tracked)
                    if (wr.TryGetTarget(out var t) && ReferenceEquals(t, element))
                    {
                        known = true;
                        break;
                    }
                if (!known)
                    Tracked.Add(new WeakReference<VisualElement>(element));
            }

            if (element.Window is { } window)
                Subscribe(window);
            else
                element.Loaded += OnElementLoaded;
        }

        private static void OnElementLoaded(object? sender, EventArgs e)
        {
            if (sender is not VisualElement element)
                return;
            element.Loaded -= OnElementLoaded;
            if (element.Window is { } window)
                Subscribe(window);
            // Re-apply once attached: the first apply may have run without a window width.
            TwRuntime.Apply(element, Tw.EffectiveClass(element));
        }

        private static void Subscribe(Window window)
        {
            lock (Subscribed)
            {
                if (!Subscribed.Add(window))
                    return;
            }
            window.SizeChanged += (_, _) =>
            {
                int tier = TierOf(window.Width);
                if (tier == _lastTier)
                    return;
                _lastTier = tier;
                ReapplyAll();
            };
            _lastTier = TierOf(window.Width);
        }

        private static int TierOf(double width)
        {
            int tier = 0;
            foreach (float t in Tiers)
                if (width >= t)
                    tier++;
            return tier;
        }

        private static void ReapplyAll()
        {
            List<VisualElement> alive = [];
            lock (Tracked)
            {
                Tracked.RemoveAll(wr => !wr.TryGetTarget(out _));
                foreach (var wr in Tracked)
                    if (wr.TryGetTarget(out var el))
                        alive.Add(el);
            }
            foreach (var el in alive)
                TwRuntime.Apply(el, Tw.EffectiveClass(el));
        }
    }
}

public static class TwAppBuilderExtensions
{
    /// <summary>
    /// Enables Tw styling. Optional — the engine self-initializes on first use —
    /// but this is where you pick the diagnostic mode:
    /// <code>builder.UseTw(o => o.DiagnosticMode = TwDiagnosticMode.Throw);</code>
    /// </summary>
    public static MauiAppBuilder UseTw(this MauiAppBuilder builder, Action<TwOptions>? configure = null)
    {
        var options = new TwOptions();
        configure?.Invoke(options);
        TwRuntime.DiagnosticMode = options.DiagnosticMode;
        TwRuntime.DiagnosticSink = options.DiagnosticSink;
        return builder;
    }
}

public sealed class TwOptions
{
    public TwDiagnosticMode DiagnosticMode { get; set; } = TwDiagnosticMode.DebugOutput;
    public Action<TwDiagnostic>? DiagnosticSink { get; set; }
}
