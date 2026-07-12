using System.Collections.Concurrent;

namespace TwStyling;

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

    /// <summary>
    /// When true, <see cref="GetPlan"/> reports a diagnostic every time it has to parse a
    /// class string at runtime — i.e. a cache miss that was not preloaded at build time.
    /// With the source generator running, all literal XAML/C# class strings are preloaded,
    /// so a runtime parse means a dynamic string (interpolation, binding) or a device-
    /// dependent platform:/idiom: variant. Combine with a throwing sink to hard-enforce
    /// "no runtime parsing" in CI. Off by default.
    /// </summary>
    public bool WarnOnRuntimeParse { get; set; }

    public TwEngine(TwEnvironment environment, Action<TwDiagnostic>? diagnosticSink = null)
    {
        Environment = environment;
        DiagnosticSink = diagnosticSink;
    }

    /// <summary>
    /// The stylesheet Tailwind compiled for this app — the single source of truth for what every
    /// utility means. The build embeds it (see TwStyling.Maui.targets), so class strings the
    /// generator could not precompile (an interpolated string, or an <c>idiom:</c> variant that
    /// depends on the device) resolve through the same Tailwind vocabulary as the literals.
    ///
    /// There is deliberately no second vocabulary to fall back to. The engine used to carry its own
    /// class-name parser and token tables, and the two disagreed — the same palette bug had to be
    /// fixed in both. One front end cannot drift from itself.
    /// </summary>
    public TwCssPlanCompiler? Stylesheet { get; set; }

    /// <summary>
    /// Get the compiled plan for a class string. Cached; thread-safe. With no stylesheet attached
    /// there is no vocabulary, so every plan is empty — a build that skipped the CSS pipeline.
    /// </summary>
    public StylePlan GetPlan(string? classes)
    {
        if (string.IsNullOrWhiteSpace(classes))
            return StylePlan.Empty;

        if (_cache.TryGetValue(classes!, out var hit))
            return hit;

        if (Stylesheet is not { } stylesheet)
            return StylePlan.Empty;

        var compiled = stylesheet.Compile(classes!, Environment);
        var plan = _cache.GetOrAdd(classes!, compiled);

        // Report only from the thread that won the insert race — losers would
        // duplicate every diagnostic (and double-throw in Throw mode).
        if (ReferenceEquals(plan, compiled) && DiagnosticSink is { } sink)
        {
            if (WarnOnRuntimeParse)
                sink(new TwDiagnostic(classes!, "",
                    "compiled at runtime — this class string was not precompiled (an interpolated string, or an idiom: variant the generator cannot resolve at build time)"));
            foreach (var d in plan.Diagnostics)
                sink(d);
        }
        return plan;
    }

    /// <summary>
    /// Seeds the cache with a build-time-compiled plan (source generator output).
    /// First-come wins; a preload never overwrites an existing entry.
    /// </summary>
    public void Preload(string classes, StylePlan plan) => _cache.TryAdd(classes, plan);

    /// <summary>Number of unique class strings compiled so far (observability/tests).</summary>
    public int CachedPlanCount => _cache.Count;
}
