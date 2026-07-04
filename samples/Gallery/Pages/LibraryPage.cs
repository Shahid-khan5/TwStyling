using Tw.Components;
using Tw.Maui;
using static Gallery.Pages.GalleryUi;

namespace Gallery.Pages;

/// <summary>The Tw.Components library — variants, sizes, and composition.</summary>
public class LibraryPage : ContentPage
{
    public LibraryPage()
    {
        Title = "Library";

                var rows = new List<View>
        {
            Section("TwButton"),
            Caption("Variant × Size — pressed states, dark mode, and press feedback are built in"),
        };

        foreach (var variant in Enum.GetValues<TwButtonVariant>())
        {
            var row = Row("gap-3", Code(variant.ToString()));
            foreach (var size in Enum.GetValues<TwSize>())
                row.Children.Add(new TwButton { Text = $"{size}", Variant = variant, Size = size });
            rows.Add(row);
        }

        rows.Add(Section("TwCard"));
        foreach (var variant in Enum.GetValues<TwCardVariant>())
        {
            rows.Add(new TwCard
            {
                Variant = variant,
                Content = Column("gap-2",
                    new Label { Text = variant.ToString() }.Tw("text-lg font-semibold text-slate-900 dark:text-white"),
                    new Label { Text = "Cards compose: set Variant for the preset, add Class for overrides." }
                        .Tw("text-sm text-slate-500 dark:text-slate-400")),
            });
        }
        rows.Add(new TwCard
        {
            Variant = TwCardVariant.Elevated,
            Class = "bg-indigo-600 rounded-3xl",  // overrides win, exactly like Tailwind
            Content = new Label { Text = "Elevated + Class=\"bg-indigo-600 rounded-3xl\"" }.Tw("text-sm font-medium text-white"),
        });

        rows.Add(Section("TwBadge"));
        var badges = Row("gap-2");
        foreach (var variant in Enum.GetValues<TwBadgeVariant>())
            badges.Children.Add(new TwBadge { Text = variant.ToString(), Variant = variant });
        rows.Add(badges);

        rows.Add(Section("TwInput"));
        rows.Add(Caption("focus ring is a cached class swap — click into it"));
        rows.Add(new TwInput { Placeholder = "you@example.com" }.Tw("max-w-md"));

        rows.Add(Section("TwAvatar + TwDivider"));
        rows.Add(Row("gap-3 items-center",
            new TwAvatar { Text = "CF" },
            new TwAvatar { Text = "TW" },
            new TwAvatar { Text = "AI" }));
        rows.Add(new TwDivider());
        rows.Add(Caption("everything above is Tw.Components — zero XAML styles, zero resource dictionaries"));

        Content = Page(this, rows.ToArray());
    }
}
