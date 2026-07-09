using Tw.Maui;

namespace Gallery.Pages;

/// <summary>
/// In-app memory-leak probe. Creates styled elements in a live container so Loaded/
/// Unloaded fire (wiring events, engaging keyframe animations and the breakpoint
/// tracker), then removes them, forces GC, and reports how many survive.
///
/// Runs on the real platform on purpose — a headless unit test can't exercise MAUI's
/// animation manager, gesture recognizers, or handler teardown. Interpretation: 0 is
/// ideal; a small transient count can be the platform's own view recycling, so the
/// signal that matters is whether survivors GROW across repeated runs (a real leak)
/// versus staying flat.
/// </summary>
internal static class TwLeakProbe
{
    public readonly record struct Result(string Scenario, int Created, int Survived);

    private static readonly (string Name, Func<View> Factory)[] Scenarios =
    [
        ("stateful (hover/pressed/disabled)", static () =>
            new Button { Text = "x" }.Tw("bg-blue-500 hover:bg-blue-600 pressed:bg-blue-700 disabled:bg-gray-300 rounded-lg p-2")),
        ("keyframe (animate-spin)", static () =>
            new Label { Text = "spin" }.Tw("animate-spin text-blue-500")),
        ("breakpoint (md:/lg:)", static () =>
            new Label { Text = "bp" }.Tw("p-2 md:p-4 lg:p-8 bg-white dark:bg-slate-800")),
        ("transition + state", static () =>
            new Border { Content = new Label { Text = "t" } }.Tw("bg-white transition-all duration-200 hover:bg-slate-100 rounded-xl p-4")),
    ];

    public static async Task<IReadOnlyList<Result>> RunAsync(Layout host, int perScenario = 50)
    {
        var results = new List<Result>(Scenarios.Length);
        foreach (var (name, factory) in Scenarios)
            results.Add(await ProbeAsync(name, host, factory, perScenario));
        return results;
    }

    private static async Task<Result> ProbeAsync(string name, Layout host, Func<View> factory, int count)
    {
        var refs = new List<WeakReference>(count);
        for (int i = 0; i < count; i++)
        {
            var element = factory();
            host.Add(element);
            refs.Add(new WeakReference(element));
        }

        // Let Loaded fire so animations/trackers/handlers engage, then tear down.
        await Task.Delay(60);
        host.Clear();
        await Task.Delay(60);

        for (int i = 0; i < 4; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            await Task.Delay(15);
        }

        int survived = 0;
        foreach (var r in refs)
            if (r.IsAlive)
                survived++;
        return new Result(name, count, survived);
    }
}
