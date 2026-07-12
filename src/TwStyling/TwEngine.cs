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
    /// The stylesheet Tailwind compiled for this app, when the build embedded one. It is the same
    /// source of truth the generator used, so a class string that could not be precompiled — a
    /// dynamic string, or an <c>idiom:</c> variant that depends on the device — still resolves
    /// through Tailwind rather than through the built-in parser and its own tables.
    ///
    /// Without this, one app would carry two vocabularies: the CSS one for literals and the parser
    /// one for everything else, disagreeing on what <c>bg-blue-500</c> means.
    /// </summary>
    public TwCssPlanCompiler? Stylesheet { get; set; }

    /// <summary>Get the compiled plan for a class string. Cached; thread-safe.</summary>
    public StylePlan GetPlan(string? classes)
    {
        if (string.IsNullOrWhiteSpace(classes))
            return StylePlan.Empty;

        if (_cache.TryGetValue(classes!, out var hit))
            return hit;

        var compiled = Stylesheet is { } stylesheet
            ? stylesheet.Compile(classes!, Environment)
            : StylePlanCompiler.Compile(classes!, Environment);
        var plan = _cache.GetOrAdd(classes!, compiled);

        // Report only from the thread that won the insert race — losers would
        // duplicate every diagnostic (and double-throw in Throw mode).
        if (ReferenceEquals(plan, compiled) && DiagnosticSink is { } sink)
        {
            if (WarnOnRuntimeParse)
                sink(new TwDiagnostic(classes!, "",
                    "parsed at runtime — this class string was not precompiled (dynamic string, or a platform:/idiom: variant the generator skips)"));
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

    /// <summary>
    /// Seeds the cache with a build-time-compiled plan (source generator output).
    /// First-come wins; a preload never overwrites an existing entry.
    /// </summary>
    public void Preload(string classes, StylePlan plan) => _cache.TryAdd(classes, plan);

    /// <summary>Number of unique class strings compiled so far (observability/tests).</summary>
    public int CachedPlanCount => _cache.Count;
}
