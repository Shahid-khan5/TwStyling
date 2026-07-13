using TwStyling.Maui;

namespace Gallery.Pages;

/// <summary>Tiny helpers shared by the showcase pages — everything is styled with .Tw().</summary>
internal static class GalleryUi
{
    public static Label Section(string title) =>
        new Label { Text = title }.Tw("text-2xl font-bold text-slate-900 dark:text-white mt-4");

    public static Label Caption(string text) =>
        new Label { Text = text }.Tw("text-xs text-slate-400 dark:text-slate-500");

    public static Label Code(string text) =>
        new Label { Text = text }.Tw("text-xs text-slate-500 dark:text-slate-400 w-32");

    public static HorizontalStackLayout Row(string classes, params View[] children)
    {
        var row = new HorizontalStackLayout().Tw(classes);
        foreach (var child in children) row.Children.Add(child);
        return row;
    }

    public static VerticalStackLayout Column(string classes, params View[] children)
    {
        var column = new VerticalStackLayout().Tw(classes);
        foreach (var child in children) column.Children.Add(child);
        return column;
    }

    public static ScrollView Page(ContentPage page, params View[] children)
    {
        page.Tw("bg-slate-50 dark:bg-slate-950");
        return new ScrollView { Content = Column("p-6 gap-3 max-w-4xl", children) };
    }
}
