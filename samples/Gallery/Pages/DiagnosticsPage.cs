using System.Text;
using TwStyling;
using TwStyling.Maui;
using static Gallery.Pages.GalleryUi;

namespace Gallery.Pages;

/// <summary>
/// Memory diagnostics: runs the in-app leak probe (create → load → unload → GC → count
/// survivors) and shows live managed-heap / GC / engine-cache stats. Run the probe a few
/// times — flat survivor counts across runs mean no leak; growth means one.
/// </summary>
public sealed class DiagnosticsPage : ContentPage
{
    private readonly Label _leakResult = new Label { Text = "Not run yet." }
        .Tw("text-xs text-slate-600 dark:text-slate-300");
    private readonly Label _stats = new Label().Tw("text-xs text-slate-500 dark:text-slate-400");

    // Live tree so probed elements actually load/unload (and engage animations/trackers).
    private readonly VerticalStackLayout _host = new() { HeightRequest = 24 };
    private readonly VerticalStackLayout _log = new VerticalStackLayout().Tw("gap-1");
    private int _run;

    public DiagnosticsPage()
    {
        Title = "Diagnostics";
        this.Tw("bg-slate-50 dark:bg-slate-950");

        var runLeak = new Button { Text = "Run leak probe" }
            .Tw("bg-blue-600 text-white rounded-lg px-4 py-2 font-semibold")
            .Also(b => b.Clicked += async (_, _) => await RunLeakProbe());
        var refresh = new Button { Text = "Refresh stats" }
            .Tw("bg-slate-700 text-white rounded-lg px-4 py-2 font-semibold")
            .Also(b => b.Clicked += (_, _) => RefreshStats());

        RefreshStats();

        Content = new ScrollView
        {
            Content = Column("p-6 gap-3 max-w-4xl",
                Section("Memory diagnostics"),
                Caption("Runs on the real platform — a headless test can't drive MAUI's animation manager, gesture recognizers, or handler teardown."),
                Row("gap-2 flex-wrap", runLeak, refresh),
                Section("Leak probe"),
                _leakResult,
                _log,
                new BoxView().Tw("h-px bg-slate-200 dark:bg-slate-700 my-2"),
                Section("Live stats"),
                _stats,
                _host),
        };
    }

    /// <summary>Headless self-test entry: runs the probe N times and returns a compact report.</summary>
    public async Task<string> RunProbeAsync(int runs = 3)
    {
        var sb = new StringBuilder();
        for (int r = 0; r < runs; r++)
        {
            var results = await TwLeakProbe.RunAsync(_host);
            sb.Append($"  run {r + 1}: ");
            foreach (var x in results)
                sb.Append($"{x.Scenario.Split(' ')[0]}={x.Survived}/{x.Created}  ");
            sb.AppendLine();
        }
        sb.AppendLine($"  heap={GC.GetTotalMemory(false) / 1024.0 / 1024.0:N1}MB  plans={TwRuntime.Engine.CachedPlanCount}");
        sb.Append(OneVocabularyReport());
        return sb.ToString();
    }

    /// <summary>
    /// Checks on the real device that a class string built at runtime carries the Tailwind v4
    /// palette. Before the compiled stylesheet was embedded, literals resolved through Tailwind but
    /// interpolated strings fell through to the built-in parser and its v3 tables, so one app
    /// rendered two different <c>bg-blue-500</c>s.
    ///
    /// The strings below are assembled at runtime on purpose: the generator cannot precompile them,
    /// so they exercise exactly the path that used to diverge.
    /// </summary>
    public static string OneVocabularyReport()
    {
        var engine = TwRuntime.Engine;
        var sb = new StringBuilder();

        sb.AppendLine($"  stylesheet={(engine.Stylesheet is null ? "ABSENT — parser fallback" : "embedded")}");

        // (family, shade, expected v4 hex). The v3 value is noted where it differs, which is the
        // value these very strings produced before the fix.
        var expected = new (string Family, string Shade, uint V4, uint V3)[]
        {
            ("blue", "500", 0xFF2B7FFF, 0xFF3B82F6),
            ("red", "500", 0xFFFB2C36, 0xFFEF4444),
            ("indigo", "600", 0xFF4F39F6, 0xFF4F46E5),
            ("emerald", "500", 0xFF00BC7D, 0xFF10B981),
        };

        bool ok = true;
        foreach (var (family, shade, v4, v3) in expected)
        {
            var classes = $"bg-{family}-{shade}";   // built at runtime — never a literal
            uint actual = Rgba(engine.GetPlan(classes));

            string verdict = actual == v4 ? "v4 ok" : actual == v3 ? "v3 — STALE PALETTE" : "unexpected";
            ok &= actual == v4;
            sb.AppendLine($"  {classes,-18} #{actual:X8}  {verdict}");
        }

        sb.AppendLine(ok
            ? "  ONE VOCABULARY: ok — runtime strings resolve through Tailwind"
            : "  ONE VOCABULARY: FAILED — the app is rendering two palettes");
        return sb.ToString();

        static uint Rgba(StylePlan plan)
        {
            foreach (var d in plan.Light)
                if (d.Property == TwPropertyId.Background)
                    return d.Value.Rgba;
            return 0;
        }
    }

    private async Task RunLeakProbe()
    {
        _leakResult.Text = "Running…";
        var results = await TwLeakProbe.RunAsync(_host);
        _run++;

        var sb = new StringBuilder($"Run #{_run}:  ");
        bool clean = true;
        foreach (var r in results)
        {
            if (r.Survived > 0) clean = false;
            sb.Append($"{r.Scenario}: {r.Survived}/{r.Created}    ");
        }

        _log.Add(new Label { Text = sb.ToString() }
            .Tw(clean ? "text-xs text-emerald-600" : "text-xs text-amber-600"));
        _leakResult.Text = clean
            ? "✓ 0 survivors this run. Run again — counts should stay flat."
            : "Some survivors — re-run; a real leak grows across runs, platform recycling stays flat.";
        RefreshStats();
    }

    private void RefreshStats()
    {
        _stats.Text =
            $"Managed heap: {GC.GetTotalMemory(false) / 1024.0 / 1024.0:N1} MB\n" +
            $"GC collections — gen0: {GC.CollectionCount(0)}, gen1: {GC.CollectionCount(1)}, gen2: {GC.CollectionCount(2)}\n" +
            $"Engine cached plans: {TwRuntime.Engine.CachedPlanCount}";
    }
}
