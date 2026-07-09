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

    /// <summary>
    /// When true, the engine reports a diagnostic whenever it parses a class string at
    /// runtime (a cache miss not covered by build-time precompilation). Pair with
    /// <see cref="TwDiagnosticMode.Throw"/> to fail the build/tests on any runtime parse.
    /// </summary>
    public static bool WarnOnRuntimeParse { get; set; }

    private static Dictionary<string, StylePlan>? _pendingPreloads;

    /// <summary>The shared engine; environment is detected from the device on first use.</summary>
    public static TwEngine Engine
    {
        get
        {
            if (_engine is { } e) return e;
            lock (InitLock)
            {
                if (_engine is null)
                {
                    _engine = new TwEngine(DetectEnvironment(), ReportDiagnostic) { WarnOnRuntimeParse = WarnOnRuntimeParse };
                    if (_pendingPreloads is { } pending)
                    {
                        foreach (var kv in pending)
                            _engine.Preload(kv.Key, kv.Value);
                        _pendingPreloads = null;
                    }
                }
                return _engine;
            }
        }
    }

    /// <summary>
    /// Entry point for source-generated plans (module initializers run before the
    /// engine exists, so preloads are buffered until first use).
    /// </summary>
    public static void Preload(string classes, StylePlan plan)
    {
        lock (InitLock)
        {
            if (_engine is { } e)
            {
                e.Preload(classes, plan);
            }
            else
            {
                // First-come wins, matching TwEngine.Preload's TryAdd semantics —
                // which plan survives must not depend on engine-init timing.
                _pendingPreloads ??= new Dictionary<string, StylePlan>(StringComparer.Ordinal);
                if (!_pendingPreloads.ContainsKey(classes))
                    _pendingPreloads[classes] = plan;
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

    internal static void Apply(VisualElement element, string? classes)
    {
        var plan = Engine.GetPlan(classes);
        // An empty plan on a never-styled element is a no-op — but on a styled one
        // it must reconcile to empty targets so every applied property is cleared
        // (setting Class to null/"" means "remove my styling", not "keep it").
        if (ReferenceEquals(plan, StylePlan.Empty) && !TwReconciler.HasApplied(element))
            return;
        ApplyPlanFor(element, plan);
    }

    private static void ApplyPlanFor(VisualElement element, StylePlan plan)
    {
        var mauiPlan = TwMauiPlan.Get(plan, element.GetType());
        bool reapply = TwReconciler.HasApplied(element);

        // Stop (and reset) any running keyframe loop BEFORE reconciling, so
        // plan-declared rotate-*/opacity-* values land on top of the reset.
        if (reapply)
            TwKeyframeRunner.Stop(element);

        if (mauiPlan.HasStates)
            TwInteractionWiring.Wire(element, mauiPlan);
        else
            TwReconciler.ClearStates(element);

        // The reconciler is the whole styling loop: compute targets from the
        // element's state vector, diff against what's applied, set/clear/tween.
        TwReconciler.SetPlan(element, mauiPlan,
            allowTween: reapply && mauiPlan.Transition is not null);

        if (mauiPlan.Keyframes != TwKeyframes.None)
            TwKeyframeRunner.Run(element, mauiPlan.Keyframes);

        if (mauiPlan.Breakpoints.Length > 0)
            SizeTracker.Track(element);
    }

    /// <summary>
    /// Re-applies elements with responsive breakpoints when their window crosses a
    /// breakpoint tier. Cheap: re-apply is cached-plan lookups plus SetValue calls.
    /// </summary>
    private static class SizeTracker
    {
        private static readonly List<WeakReference<VisualElement>> Tracked = [];

        /// <summary>Per-window tier state; weak keys so closed windows can collect.</summary>
        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Window, TierState> Windows = new();

        private sealed class TierState { public int Tier; }

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
            {
                Subscribe(window);
            }
            else
            {
                element.Loaded -= OnElementLoaded; // idempotent under repeated Track calls
                element.Loaded += OnElementLoaded;
            }
        }

        private static void OnElementLoaded(object? sender, EventArgs e)
        {
            if (sender is not VisualElement element)
                return;
            element.Loaded -= OnElementLoaded;
            if (element.Window is { } window)
                Subscribe(window);
            // Reconcile once attached: the first apply may have run without a window width.
            TwReconciler.EnvironmentChanged(element);
        }

        private static void Subscribe(Window window)
        {
            lock (Windows)
            {
                if (Windows.TryGetValue(window, out _))
                    return;
                var state = new TierState { Tier = TierOf(window.Width) };
                Windows.Add(window, state);

                void OnSizeChanged(object? s, EventArgs e)
                {
                    int tier = TierOf(window.Width);
                    if (tier == state.Tier)
                        return;
                    state.Tier = tier;
                    ReapplyFor(window);
                }

                window.SizeChanged += OnSizeChanged;
                window.Destroying += (_, _) =>
                {
                    window.SizeChanged -= OnSizeChanged;
                    lock (Windows)
                    {
                        Windows.Remove(window);
                    }
                };
            }
        }

        private static int TierOf(double width)
        {
            int tier = 0;
            foreach (float t in Tiers)
                if (width >= t)
                    tier++;
            return tier;
        }

        private static void ReapplyFor(Window window)
        {
            List<VisualElement> alive = [];
            lock (Tracked)
            {
                Tracked.RemoveAll(wr => !wr.TryGetTarget(out _));
                foreach (var wr in Tracked)
                    if (wr.TryGetTarget(out var el) && ReferenceEquals(el.Window, window))
                        alive.Add(el);
            }
            foreach (var el in alive)
                TwReconciler.EnvironmentChanged(el);
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
        TwRuntime.WarnOnRuntimeParse = options.WarnOnRuntimeParse;
        return builder;
    }
}

public sealed class TwOptions
{
    public TwDiagnosticMode DiagnosticMode { get; set; } = TwDiagnosticMode.DebugOutput;
    public Action<TwDiagnostic>? DiagnosticSink { get; set; }

    /// <summary>
    /// Report a diagnostic whenever a class string is parsed at runtime instead of being
    /// served from a build-time-precompiled plan. Combine with
    /// <see cref="TwDiagnosticMode.Throw"/> to enforce "no runtime parsing".
    /// </summary>
    public bool WarnOnRuntimeParse { get; set; }
}
