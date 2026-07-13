using TwStyling.Maui;
using static Gallery.Pages.GalleryUi;

namespace Gallery.Pages;

/// <summary>The entire Tailwind palette — 22 families × 11 shades, every swatch a class string.</summary>
public class ColorsPage : ContentPage
{
    private static readonly string[] Families =
    [
        "slate", "gray", "zinc", "neutral", "stone",
        "red", "orange", "amber", "yellow", "lime",
        "green", "emerald", "teal", "cyan", "sky",
        "blue", "indigo", "violet", "purple", "fuchsia",
        "pink", "rose",
    ];

    private static readonly string[] Shades =
        ["50", "100", "200", "300", "400", "500", "600", "700", "800", "900", "950"];

    public ColorsPage()
    {
        Title = "Colors";

        var rows = new List<View>
        {
            Section("Color palette"),
            // These swatches are built from interpolated strings, which the generator cannot
            // precompile — so they resolve through the legacy parser and still show the v3 hexes.
            // Literal class strings elsewhere in the app now come from Tailwind v4 via the CSS
            // pipeline, where the palette is defined in oklch (bg-blue-500 is #2B7FFF, not #3B82F6).
            // The two agree again once the runtime fallback is CSS-driven.
            Caption("bg-{family}-{shade} — all 242 entries (v3 hexes: built from interpolated strings)"),
            HeaderRow(),
        };

        foreach (var family in Families)
        {
            var row = Row("gap-1", Code(family));
            foreach (var shade in Shades)
                row.Children.Add(new BoxView().Tw($"bg-{family}-{shade} size-7 rounded-md"));
            rows.Add(row);
        }

        rows.Add(Section("Opacity modifier"));
        rows.Add(Caption("bg-indigo-600/N over a checkerboard-ish background"));
        var alphaRow = Row("gap-1", Code("/100 → /10"));
        foreach (var alpha in new[] { "100", "90", "75", "60", "50", "40", "25", "10" })
            alphaRow.Children.Add(new BoxView().Tw($"bg-indigo-600/{alpha} size-7 rounded-md"));
        rows.Add(alphaRow);

        rows.Add(Section("Arbitrary colors"));
        rows.Add(Row("gap-1",
            Code("bg-[#hex]"),
            new BoxView().Tw("bg-[#ff6b6b] size-7 rounded-md"),
            new BoxView().Tw("bg-[#4ecdc4] size-7 rounded-md"),
            new BoxView().Tw("bg-[#1a535c] size-7 rounded-md"),
            new BoxView().Tw("bg-[#ffe66d] size-7 rounded-md")));

        Content = Page(this, rows.ToArray());
    }

    private static View HeaderRow()
    {
        var header = Row("gap-1", Code(""));
        foreach (var shade in Shades)
            header.Children.Add(new Label { Text = shade }.Tw("text-xs text-slate-400 w-7 text-center"));
        return header;
    }
}
