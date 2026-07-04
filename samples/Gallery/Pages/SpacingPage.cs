using Tw.Maui;
using static Gallery.Pages.GalleryUi;

namespace Gallery.Pages;

public class SpacingPage : ContentPage
{
    public SpacingPage()
    {
        Title = "Spacing";

        var rows = new List<View>
        {
            Section("Padding"),
            Caption("p-N — the tinted area is the padding around the white chip"),
        };
        foreach (var p in new[] { "0", "1", "2", "3", "4", "6", "8", "12" })
            rows.Add(Row("gap-3",
                Code($"p-{p}"),
                new Border { Content = Chip() }.Tw($"bg-indigo-200 dark:bg-indigo-900 rounded-lg p-{p} border-0")));

        rows.Add(Section("Directional padding"));
        foreach (var cls in new[] { "px-8 py-2", "pt-8", "pl-12", "px-2.5 py-1.5", "p-[22px]" })
            rows.Add(Row("gap-3",
                Code(cls),
                new Border { Content = Chip() }.Tw($"bg-emerald-200 dark:bg-emerald-900 rounded-lg {cls} border-0")));

        rows.Add(Section("Gap"));
        rows.Add(Caption("gap-N on stack layouts"));
        foreach (var gap in new[] { "1", "2", "4", "8" })
        {
            var row = Row($"gap-{gap}", Code($"gap-{gap}"));
            for (int i = 0; i < 6; i++)
                row.Children.Add(new BoxView().Tw("bg-sky-500 size-6 rounded"));
            rows.Add(row);
        }

        rows.Add(Section("Margin"));
        rows.Add(Caption("ml-N pushes right; -mt pulls up (negative margins overlap)"));
        foreach (var m in new[] { "ml-0", "ml-6", "ml-16", "ml-32" })
            rows.Add(Row("gap-3", Code(m), new BoxView().Tw($"bg-rose-500 w-24 h-6 rounded {m}")));

        rows.Add(Column("gap-0 mt-2",
            new BoxView().Tw("bg-purple-300 w-40 h-10 rounded-lg"),
            new BoxView().Tw("bg-purple-600 w-40 h-10 rounded-lg -mt-4 ml-6")));
        rows.Add(Caption("second box: -mt-4 ml-6"));

        rows.Add(Section("Auto margins → alignment"));
        rows.Add(Caption("mx-auto centers, ml-auto pushes to the end — CSS semantics, native LayoutOptions"));
        rows.Add(new BoxView().Tw("bg-teal-500 w-24 h-6 rounded mx-auto"));
        rows.Add(new BoxView().Tw("bg-teal-600 w-24 h-6 rounded ml-auto"));

        Content = Page(this, rows.ToArray());
    }

    private static View Chip() =>
        new Label { Text = "content" }.Tw("bg-white dark:bg-slate-800 text-xs text-slate-600 dark:text-slate-300 rounded px-2 py-1");
}
