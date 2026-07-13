using TwStyling.Maui;
using static Gallery.Pages.GalleryUi;

namespace Gallery.Pages;

public class UtilitiesPage : ContentPage
{
    public UtilitiesPage()
    {
        Title = "Utilities";

        var rows = new List<View> { Section("Border radius") };
        var radiusRow = Row("gap-3");
        foreach (var r in new[] { "rounded-none", "rounded", "rounded-md", "rounded-lg", "rounded-xl", "rounded-2xl", "rounded-3xl", "rounded-full" })
            radiusRow.Children.Add(Column("gap-1",
                new BoxView().Tw($"bg-blue-500 size-14 {r}"),
                Caption(r.Replace("rounded", "r"))));
        rows.Add(radiusRow);

        rows.Add(Caption("per-corner: rounded-t-2xl / rounded-br-3xl"));
        rows.Add(Row("gap-3",
            new BoxView().Tw("bg-cyan-500 size-14 rounded-t-2xl"),
            new BoxView().Tw("bg-cyan-600 size-14 rounded-br-3xl"),
            new BoxView().Tw("bg-cyan-700 size-14 rounded-lg rounded-tl-none")));

        rows.Add(Section("Borders"));
        var borderRow = Row("gap-3");
        foreach (var w in new[] { "border", "border-2", "border-4", "border-8" })
            borderRow.Children.Add(Column("gap-1",
                new Border().Tw($"{w} border-indigo-500 bg-white dark:bg-slate-800 rounded-lg size-14"),
                Caption(w)));
        rows.Add(borderRow);

        rows.Add(Section("Shadows"));
        var shadowRow = Row("gap-6 p-2");
        foreach (var s in new[] { "shadow-sm", "shadow", "shadow-md", "shadow-lg", "shadow-xl", "shadow-2xl" })
            shadowRow.Children.Add(Column("gap-1",
                new Border().Tw($"{s} bg-white dark:bg-slate-800 rounded-xl size-16 border-0"),
                Caption(s)));
        rows.Add(shadowRow);

        rows.Add(Section("Opacity"));
        var opacityRow = Row("gap-3");
        foreach (var o in new[] { "100", "75", "50", "25", "10" })
            opacityRow.Children.Add(Column("gap-1",
                new BoxView().Tw($"bg-indigo-600 size-12 rounded-lg opacity-{o}"),
                Caption($"op-{o}")));
        rows.Add(opacityRow);

        rows.Add(Section("Gradients"));
        rows.Add(Caption("bg-gradient-to-* with from- / via- / to-"));
        rows.Add(new Border().Tw("bg-gradient-to-r from-indigo-500 via-purple-500 to-pink-500 rounded-xl h-12 w-full border-0"));
        rows.Add(new Border().Tw("bg-gradient-to-br from-emerald-400 to-cyan-500 rounded-xl h-12 w-full border-0"));
        rows.Add(new Border().Tw("bg-gradient-to-t from-amber-300 to-red-500 rounded-xl h-12 w-full border-0"));

        rows.Add(Section("Transforms"));
        rows.Add(Caption("rotate-12 / -rotate-12 / scale-125 / translate-y-2"));
        rows.Add(Row("gap-8 p-4",
            new BoxView().Tw("bg-fuchsia-500 size-12 rounded-lg rotate-12"),
            new BoxView().Tw("bg-fuchsia-600 size-12 rounded-lg -rotate-12"),
            new BoxView().Tw("bg-fuchsia-700 size-12 rounded-lg scale-125"),
            new BoxView().Tw("bg-fuchsia-800 size-12 rounded-lg translate-y-2")));

        rows.Add(Section("Z-index"));
        rows.Add(Caption("three overlapping boxes; z-* decides who wins"));
        var overlap = new Grid { HeightRequest = 90 };
        overlap.Children.Add(new BoxView().Tw("bg-orange-400 size-16 rounded-xl z-30 ml-0 mt-0 mr-auto mb-auto"));
        overlap.Children.Add(new BoxView().Tw("bg-orange-500 size-16 rounded-xl z-20 ml-10 mt-4 mr-auto mb-auto"));
        overlap.Children.Add(new BoxView().Tw("bg-orange-600 size-16 rounded-xl z-10 ml-20 mt-8 mr-auto mb-auto"));
        rows.Add(overlap);

        rows.Add(Section("Sizing"));
        rows.Add(Row("gap-3",
            Column("gap-1", new BoxView().Tw("bg-lime-500 size-10 rounded"), Caption("size-10")),
            Column("gap-1", new BoxView().Tw("bg-lime-600 w-24 h-10 rounded"), Caption("w-24 h-10")),
            Column("gap-1", new BoxView().Tw("bg-lime-700 w-[137px] h-10 rounded"), Caption("w-[137px]"))));
        rows.Add(new BoxView().Tw("bg-lime-800 w-full h-3 rounded-full"));
        rows.Add(Caption("w-full"));

        Content = Page(this, rows.ToArray());
    }
}
