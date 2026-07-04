using Tw.Maui;
using static Gallery.Pages.GalleryUi;

namespace Gallery.Pages;

public class TypographyPage : ContentPage
{
    public TypographyPage()
    {
        Title = "Typography";
        const string Pangram = "The quick brown fox jumps over the lazy dog";

        var rows = new List<View> { Section("Font sizes"), Caption("text-xs → text-6xl (scale continues to 9xl)") };
        foreach (var size in new[] { "xs", "sm", "base", "lg", "xl", "2xl", "3xl", "4xl", "5xl", "6xl" })
            rows.Add(Row("gap-3",
                Code($"text-{size}"),
                new Label { Text = "Aa — the quick brown fox" }.Tw($"text-{size} text-slate-900 dark:text-white")));

        rows.Add(Section("Font weights"));
        rows.Add(Caption("v0 caveat: native FontAttributes is binary, so 100–500 render normal and 600–900 render bold"));
        foreach (var weight in new[] { "thin", "light", "normal", "medium", "semibold", "bold", "extrabold", "black" })
            rows.Add(Row("gap-3",
                Code($"font-{weight}"),
                new Label { Text = Pangram }.Tw($"font-{weight} text-base text-slate-900 dark:text-white")));

        rows.Add(Section("Letter spacing"));
        foreach (var tracking in new[] { "tighter", "tight", "normal", "wide", "wider", "widest" })
            rows.Add(Row("gap-3",
                Code($"tracking-{tracking}"),
                new Label { Text = "TRACKING SAMPLE" }.Tw($"tracking-{tracking} text-sm text-slate-900 dark:text-white")));

        rows.Add(Section("Decoration and transform"));
        rows.Add(Row("gap-3", Code("underline"), new Label { Text = Pangram }.Tw("underline text-slate-900 dark:text-white")));
        rows.Add(Row("gap-3", Code("line-through"), new Label { Text = Pangram }.Tw("line-through text-slate-900 dark:text-white")));
        rows.Add(Row("gap-3", Code("uppercase"), new Label { Text = Pangram }.Tw("uppercase text-sm text-slate-900 dark:text-white")));
        rows.Add(Row("gap-3", Code("lowercase"), new Label { Text = "SHOUTING INTO LOWERCASE" }.Tw("lowercase text-sm text-slate-900 dark:text-white")));
        rows.Add(Row("gap-3", Code("italic"), new Label { Text = Pangram }.Tw("italic text-slate-900 dark:text-white")));

        rows.Add(Section("Overflow"));
        rows.Add(Caption("truncate = one line with ellipsis; line-clamp-2 = two lines"));
        rows.Add(new Label { Text = string.Concat(Enumerable.Repeat(Pangram + ". ", 6)) }
            .Tw("truncate text-sm text-slate-900 dark:text-white max-w-md"));
        rows.Add(new Label { Text = string.Concat(Enumerable.Repeat(Pangram + ". ", 6)) }
            .Tw("line-clamp-2 text-sm text-slate-900 dark:text-white max-w-md"));

        rows.Add(Section("Alignment"));
        foreach (var align in new[] { "left", "center", "right" })
            rows.Add(new Label { Text = $"text-{align}" }
                .Tw($"text-{align} text-sm text-slate-900 dark:text-white bg-white dark:bg-slate-800 rounded-md p-2 w-full border border-slate-200 dark:border-slate-700"));

        rows.Add(Section("Line height"));
        foreach (var leading in new[] { "none", "tight", "normal", "loose" })
            rows.Add(Row("gap-3",
                Code($"leading-{leading}"),
                new Label { Text = Pangram + ". " + Pangram + "." }.Tw($"leading-{leading} text-sm text-slate-900 dark:text-white max-w-md")));

        Content = Page(this, rows.ToArray());
    }
}
