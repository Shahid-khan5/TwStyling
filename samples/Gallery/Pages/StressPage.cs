using System.Diagnostics;
using TwStyling.Maui;

namespace Gallery.Pages;

/// <summary>
/// Perf stress surface: 1000 tw-styled cells in a virtualized CollectionView (scroll it
/// to feel recycling cost), plus an in-app micro-benchmark of the real-MAUI hot path
/// (GetPlan cache hit → lowering → reconcile) with allocation-per-op measured via
/// GC.GetAllocatedBytesForCurrentThread. This is the "does it hold 60fps like hand-
/// written XAML" acceptance surface from the design.
/// </summary>
public sealed class StressPage : ContentPage
{
    internal sealed record Cell(string Title, string Subtitle, string DotClass);

    private readonly Label _result = new Label { Text = "Tap “Micro-bench” to measure the styling hot path." }
        .Tw("text-xs text-slate-500 dark:text-slate-400 px-4 pb-2");

    public StressPage()
    {
        Title = "Stress";
        this.Tw("bg-slate-50 dark:bg-slate-950");

        string[] hues = ["bg-blue-500", "bg-emerald-500", "bg-amber-500", "bg-rose-500", "bg-violet-500"];
        var items = new List<Cell>(1000);
        for (int i = 0; i < 1000; i++)
            items.Add(new Cell($"Item {i + 1}", "styled by tw:Tw.Class — recycled on scroll", $"{hues[i % hues.Length]} w-3 h-3 rounded-full"));

        var list = new CollectionView { ItemsSource = items, ItemTemplate = new DataTemplate(BuildCell) };

        var controls = new HorizontalStackLayout { Spacing = 8 }.Tw("gap-2 p-4 flex-wrap");
        controls.Add(new Button { Text = "Toggle theme" }
            .Tw("bg-slate-800 dark:bg-white text-white dark:text-slate-900 rounded-lg px-4 py-2 font-semibold")
            .Also(b => b.Clicked += (_, _) => ToggleTheme()));
        controls.Add(new Button { Text = "Micro-bench" }
            .Tw("bg-blue-600 text-white rounded-lg px-4 py-2 font-semibold")
            .Also(b => b.Clicked += (_, _) => RunMicroBench()));

        Content = new Grid
        {
            RowDefinitions = { new(GridLength.Auto), new(GridLength.Auto), new(GridLength.Star) },
            Children = { controls, _result.Row(1), list.Row(2) },
        };
    }

    private static View BuildCell()
    {
        var dot = new BoxView { WidthRequest = 12, HeightRequest = 12 };
        dot.SetBinding(global::TwStyling.Maui.Tw.ClassProperty, static (Cell c) => c.DotClass);

        var title = new Label().Tw("text-sm font-semibold text-slate-900 dark:text-white");
        title.SetBinding(Label.TextProperty, static (Cell c) => c.Title);
        var subtitle = new Label().Tw("text-xs text-slate-500 dark:text-slate-400");
        subtitle.SetBinding(Label.TextProperty, static (Cell c) => c.Subtitle);

        var text = new VerticalStackLayout { Children = { title, subtitle } }.Tw("gap-0.5");
        var row = new HorizontalStackLayout { Children = { dot, text } }.Tw("gap-3 items-center");
        return new Border { Content = row }
            .Tw("bg-white dark:bg-slate-800 rounded-xl shadow-sm border border-slate-200 dark:border-slate-700 p-4 mx-4 my-1");
    }

    private static void ToggleTheme()
    {
        if (Application.Current is { } app)
            app.UserAppTheme = app.RequestedTheme == AppTheme.Dark ? AppTheme.Light : AppTheme.Dark;
    }

    public string RunMicroBench()
    {
        // "lean" = only boxed-once values (color/double/thickness) → isolates the engine's
        // own reconcile allocation. "full" adds shadow, which is a per-apply fresh instance
        // (a MAUI Shadow object can't be shared across elements). Alternating two classes
        // makes every SetValue actually change, driving the full GetPlan→lower→reconcile path.
        var lean = Measure(["bg-white p-4 text-sm font-semibold text-slate-700",
                            "bg-slate-100 p-6 text-base font-bold text-blue-600"]);
        var full = Measure(["bg-white shadow-md p-4 text-sm font-semibold text-slate-700",
                            "bg-slate-100 shadow-lg p-6 text-base font-bold text-blue-600"]);

        _result.Text =
            $"Re-style hot path (real MAUI, 20k restyles):\n" +
            $"  lean (engine only): {lean.Ns:N0} ns/op, {lean.Bytes:N0} B/op\n" +
            $"  full (+ shadow):    {full.Ns:N0} ns/op, {full.Bytes:N0} B/op\n" +
            $"Cached plans: {TwRuntime.Engine.CachedPlanCount}. Heap: {GC.GetTotalMemory(false) / 1024 / 1024} MB.";
        return _result.Text;
    }

    private static (double Ns, double Bytes) Measure(string[] classes)
    {
        var label = new Label();
        for (int i = 0; i < 200; i++) label.Tw(classes[i & 1]); // warm caches + JIT

        const int n = 20_000;
        long before = GC.GetAllocatedBytesForCurrentThread();
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < n; i++) label.Tw(classes[i & 1]);
        sw.Stop();
        long bytes = GC.GetAllocatedBytesForCurrentThread() - before;
        return (sw.Elapsed.TotalMilliseconds * 1_000_000 / n, (double)bytes / n);
    }
}

internal static class FluentExtensions
{
    public static T Also<T>(this T self, Action<T> configure) { configure(self); return self; }
    public static T Row<T>(this T view, int row) where T : BindableObject { Grid.SetRow(view, row); return view; }
}
