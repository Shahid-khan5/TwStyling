using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using Tw.Core;

BenchmarkRunner.Run<PlanBenchmarks>();

/// <summary>
/// The performance contract:
/// - CacheHit is the hot path — must be a single dictionary lookup, zero allocations.
/// - ColdCompile runs once per unique class string per process; single-digit µs is the budget.
/// </summary>
[MemoryDiagnoser]
public class PlanBenchmarks
{
    private const string TenUtilities =
        "bg-white dark:bg-slate-800 rounded-2xl shadow-md p-6 mx-4 text-sm font-semibold pressed:bg-slate-100 opacity-90";

    private static readonly TwEnvironment Env = new(TwPlatforms.Windows, TwIdioms.Desktop);

    private TwEngine _warmEngine = null!;
    private string[] _uniqueStrings = null!;
    private int _cold;

    [GlobalSetup]
    public void Setup()
    {
        _warmEngine = new TwEngine(Env);
        _warmEngine.GetPlan(TenUtilities);

        // Pre-generate unique strings so cold-path measurement excludes string building.
        _uniqueStrings = new string[200_000];
        for (int i = 0; i < _uniqueStrings.Length; i++)
            _uniqueStrings[i] = $"bg-blue-{(i % 9 + 1) * 100} p-{i % 12} rounded-lg text-sm shadow-sm mx-{i % 8}";
    }

    [Benchmark(Baseline = true)]
    public StylePlan CacheHit() => _warmEngine.GetPlan(TenUtilities);

    [Benchmark]
    public StylePlan ColdCompile()
    {
        // Measures compile+cache-insert of a fresh string each invocation (engine per call
        // keeps the cache from turning this into a hit).
        var s = _uniqueStrings[_cold++ % _uniqueStrings.Length];
        return new TwEngine(Env).GetPlan(s);
    }

    [Benchmark]
    public TwDiagnostic[] ValidateTenUtilities() => TwEngine.Validate(TenUtilities);
}
