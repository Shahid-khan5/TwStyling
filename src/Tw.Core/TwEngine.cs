using System.Collections.Concurrent;

namespace Tw.Core;

/// <summary>
/// Compiles class strings into cached <see cref="StylePlan"/>s.
/// The hot path is a single dictionary lookup; all parsing happens once per
/// unique class string for the lifetime of the process.
/// </summary>
public sealed class TwEngine
{
    private readonly ConcurrentDictionary<string, StylePlan> _cache = new(StringComparer.Ordinal);

    public TwEnvironment Environment { get; }

    /// <summary>
    /// Receives every problem found while compiling class strings — unknown
    /// utilities, invalid values, unsupported web-isms. Wire this to logging,
    /// debugger output, or throw in CI. Never silently ignored: if unset,
    /// diagnostics still travel on <see cref="StylePlan.Diagnostics"/>.
    /// </summary>
    public Action<TwDiagnostic>? DiagnosticSink { get; set; }

    public TwEngine(TwEnvironment environment, Action<TwDiagnostic>? diagnosticSink = null)
    {
        Environment = environment;
        DiagnosticSink = diagnosticSink;
    }

    /// <summary>Get the compiled plan for a class string. Cached; thread-safe.</summary>
    public StylePlan GetPlan(string? classes)
    {
        if (string.IsNullOrWhiteSpace(classes))
            return StylePlan.Empty;

        if (_cache.TryGetValue(classes!, out var hit))
            return hit;

        var plan = StylePlanCompiler.Compile(classes!, Environment);
        plan = _cache.GetOrAdd(classes!, plan);

        if (plan.Diagnostics.Length > 0 && DiagnosticSink is { } sink)
        {
            foreach (var d in plan.Diagnostics)
                sink(d);
        }
        return plan;
    }

    /// <summary>
    /// Validates a class string without an environment-filtered plan — used by
    /// tooling (analyzer, tests) to report problems for ALL platforms.
    /// Returns the diagnostics; empty means the string is fully valid.
    /// </summary>
    public static TwDiagnostic[] Validate(string classes)
    {
        var diagnostics = new List<TwDiagnostic>();
        TwParser.Parse(classes, diagnostics.Add);
        return diagnostics.ToArray();
    }

    /// <summary>Number of unique class strings compiled so far (observability/tests).</summary>
    public int CachedPlanCount => _cache.Count;
}
