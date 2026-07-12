namespace Tw.Css;

/// <summary>Raised when a value cannot be resolved to a concrete result.</summary>
public sealed class CssEvalException : Exception
{
    public CssEvalException(string message) : base(message) { }
}

/// <summary>
/// The custom-property scope a value resolves against, plus the unit bases needed to make
/// <c>rem</c>/<c>em</c> concrete. Variables are parsed lazily and memoized.
/// </summary>
public sealed class CssEnvironment
{
    private readonly IReadOnlyList<IReadOnlyDictionary<string, string>> _layers;
    private readonly Dictionary<string, CssValue> _resolved = new(StringComparer.Ordinal);
    private readonly HashSet<string> _resolving = new(StringComparer.Ordinal);

    public CssEnvironment(IReadOnlyDictionary<string, string> variables) : this(new[] { variables }) { }

    /// <summary>
    /// Layers a custom-property scope over a base scope, later layers winning. A Tailwind rule
    /// declares its own <c>--tw-*</c> properties and reads them back in the same block
    /// (<c>--tw-scale-x: 95%; scale: var(--tw-scale-x) …</c>), so those locals must shadow the
    /// registered <c>@property</c> defaults or the utility silently resolves to its initial value.
    /// </summary>
    public CssEnvironment(params IReadOnlyDictionary<string, string>[] layers) => _layers = layers;

    private bool TryGetRaw(string name, out string text)
    {
        for (int i = _layers.Count - 1; i >= 0; i--)
            if (_layers[i].TryGetValue(name, out text!))
                return true;
        text = null!;
        return false;
    }

    /// <summary>Pixels per <c>rem</c>. Tailwind's default root font size.</summary>
    public double RemBase { get; set; } = 16;

    /// <summary>Pixels per <c>em</c>, i.e. the font size in effect for the element being styled.</summary>
    public double EmBase { get; set; } = 16;

    /// <summary>Substituted for the <c>currentcolor</c> keyword. Null means "cannot resolve".</summary>
    public CssColor? CurrentColor { get; set; }

    public bool IsDefined(string name) => TryGetRaw(name, out _);

    /// <summary>
    /// Resolves a custom property to a fully-evaluated value. Guards against reference cycles,
    /// which a hand-authored <c>@theme</c> block can easily introduce.
    /// </summary>
    public bool TryGetVariable(string name, out CssValue value)
    {
        if (_resolved.TryGetValue(name, out value!)) return true;
        if (!TryGetRaw(name, out var text)) { value = null!; return false; }

        if (!_resolving.Add(name))
            throw new CssEvalException($"cyclic custom property reference through '{name}'");

        try
        {
            var parsed = CssValueParser.Parse(text);
            value = CssEvaluator.Evaluate(parsed, this);
            _resolved[name] = value;
            return true;
        }
        finally
        {
            _resolving.Remove(name);
        }
    }
}
