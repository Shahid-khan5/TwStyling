using TwStyling;
using TwStyling.Css;

namespace TwStyling.Tests;

/// <summary>
/// Builds engines backed by real Tailwind output — the same thing a real build produces.
///
/// The suite used to construct a bare <see cref="TwEngine"/>, which fell back to the built-in
/// class-name parser. That parser is gone: Tailwind is the only vocabulary now, so the tests must
/// speak it too. Fixtures/tailwind-tests.css is the CLI's output for every utility this suite
/// exercises (Fixtures/tailwind-tests.entry.css is the input that produced it).
/// </summary>
internal static class TwTestEngine
{
    public static readonly TwEnvironment Windows = new(TwPlatforms.Windows, TwIdioms.Desktop);
    public static readonly TwEnvironment AndroidPhone = new(TwPlatforms.Android, TwIdioms.Phone);

    /// <summary>Every platform and idiom — what tooling (the analyzer) validates against.</summary>
    public static readonly TwEnvironment Any = new(TwPlatforms.Any, TwIdioms.Any);

    private static readonly Lazy<TwCssPlanCompiler> Stylesheet = new(() =>
        TwCssPlanCompiler.FromCss(File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "tailwind-tests.css"))));

    public static TwCssPlanCompiler Compiler => Stylesheet.Value;

    public static TwEngine New(TwEnvironment? environment = null) =>
        new(environment ?? Windows) { Stylesheet = Stylesheet.Value };

    /// <summary>Validates a class string for all platforms, as the analyzer does.</summary>
    public static TwDiagnostic[] Validate(string classes) =>
        Stylesheet.Value.Compile(classes, Any).Diagnostics;
}
