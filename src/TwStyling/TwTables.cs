namespace TwStyling;

/// <summary>
/// What little the engine still needs to know about Tailwind itself.
///
/// This file used to hold the whole default theme — font sizes, radii, shadows, tracking, the
/// variant table, and a 242-entry colour palette — because the engine parsed class names and so had
/// to know what each one meant. Tailwind now compiles the stylesheet and supplies all of it, and the
/// tables are gone. Keeping them would mean maintaining a second copy of Tailwind's theme, which can
/// only ever drift from the real one; it drifted twice before it was deleted.
/// </summary>
public static class TwTables
{
    /// <summary>
    /// Tailwind's preflight gives a bare <c>border</c> a visible colour (gray-200). The compiled CSS
    /// carries only the width, so the engine supplies that colour when a border has none.
    /// </summary>
    public const uint DefaultBorderColor = 0xFFE5E7EB;
}
