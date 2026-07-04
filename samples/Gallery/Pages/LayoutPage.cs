using Tw.Maui;
using static Gallery.Pages.GalleryUi;

namespace Gallery.Pages;

/// <summary>Flexbox, grid, responsive breakpoints, and transitions.</summary>
public class LayoutPage : ContentPage
{
    public LayoutPage()
    {
        Title = "Layout";

        var rows = new List<View>
        {
            Section("Flexbox — justify-content"),
            Caption("flex justify-* on a FlexLayout"),
        };

        foreach (var justify in new[] { "start", "center", "end", "between", "around", "evenly" })
        {
            // fixed width: inside a horizontal stack there is no space to 'fill'
            var flex = new FlexLayout().Tw($"flex justify-{justify} bg-white dark:bg-slate-800 rounded-lg p-2 w-96");
            for (int i = 0; i < 3; i++)
                flex.Children.Add(new BoxView().Tw("bg-indigo-500 size-8 rounded"));
            rows.Add(Row("gap-3", Code($"justify-{justify}"), flex));
        }

        rows.Add(Section("Flexbox — items & grow"));
        var items = new FlexLayout().Tw("flex items-center bg-white dark:bg-slate-800 rounded-lg p-2 h-20 w-full");
        items.Children.Add(new BoxView().Tw("bg-emerald-400 w-10 h-6 rounded"));
        items.Children.Add(new BoxView().Tw("bg-emerald-500 w-10 h-10 rounded"));
        items.Children.Add(new BoxView().Tw("bg-emerald-600 h-8 rounded grow"));
        rows.Add(items);
        rows.Add(Caption("items-center + last box has 'grow'"));

        rows.Add(Section("Flexbox — wrap"));
        var wrap = new FlexLayout().Tw("flex flex-wrap bg-white dark:bg-slate-800 rounded-lg p-2 w-full gap-y-2");
        for (int i = 0; i < 14; i++)
            wrap.Children.Add(new BoxView().Tw("bg-purple-500 w-16 h-8 rounded m-1"));
        rows.Add(wrap);

        rows.Add(Section("Grid"));
        rows.Add(Caption("grid grid-cols-3 with col-span-2 — definitions come from the class string"));
        var grid = new Grid().Tw("grid grid-cols-3 gap-2 w-full");
        grid.Add(new BoxView().Tw("bg-sky-400 h-10 rounded col-span-2"), 0, 0);
        grid.Add(new BoxView().Tw("bg-sky-500 h-10 rounded col-start-3"), 0, 0);
        rows.Add(grid);

        rows.Add(Section("Responsive breakpoints"));
        rows.Add(Caption("resize the window: below 768 the bar is amber; md: turns it emerald, lg: indigo"));
        rows.Add(new BoxView().Tw("bg-amber-400 md:bg-emerald-500 lg:bg-indigo-500 h-10 w-full rounded-lg"));
        rows.Add(new Label { Text = "p-2 md:p-6 also grows this card's padding at md:" }
            .Tw("bg-white dark:bg-slate-800 text-sm text-slate-600 dark:text-slate-300 rounded-lg p-2 md:p-6 w-full"));

        rows.Add(Section("Transitions"));
        rows.Add(Caption("transition-all duration-300 — tap the button to toggle the box's classes"));
        var box = new BoxView().Tw("transition-all duration-300 ease-out bg-indigo-500 size-16 rounded-lg");
        bool toggled = false;
        var toggle = new Button { Text = "Toggle" }
            .Tw("bg-slate-900 dark:bg-white text-white dark:text-slate-900 font-medium rounded-lg px-4 py-2");
        toggle.Clicked += (_, _) =>
        {
            toggled = !toggled;
            box.Tw(toggled
                ? "transition-all duration-300 ease-out bg-pink-500 size-16 rounded-lg translate-x-32 rotate-45 scale-125"
                : "transition-all duration-300 ease-out bg-indigo-500 size-16 rounded-lg");
        };
        rows.Add(Row("gap-4 items-center", toggle, box));

        rows.Add(Section("Keyframe animations"));
        rows.Add(Caption("animate-spin / animate-pulse / animate-bounce — looping, engine-managed"));
        rows.Add(Row("gap-8 p-3",
            new BoxView().Tw("animate-spin bg-indigo-500 size-10 rounded-md"),
            new BoxView().Tw("animate-pulse bg-emerald-500 size-10 rounded-lg"),
            new BoxView().Tw("animate-bounce bg-rose-500 size-10 rounded-full")));

        rows.Add(Section("Animated pressed state"));
        rows.Add(Caption("transition-colors + pressed: — hold the button; the color eases instead of snapping"));
        rows.Add(new Button { Text = "Press and hold" }
            .Tw("transition-colors duration-200 ease-out bg-indigo-600 pressed:bg-rose-600 text-white font-semibold rounded-lg px-4 py-3"));

        rows.Add(Section("Binding-driven styling"));
        rows.Add(Caption("Tw.IsActive toggles Tw.ActiveClass (bind it to any view-model bool) — transition-all animates the change"));
        var bound = new Label { Text = "Different size and color when the bool flips" }
            .Tw("transition-all duration-300 ease-out text-sm text-slate-500 dark:text-slate-400 bg-white dark:bg-slate-800 rounded-lg p-3");
        Tw.Maui.Tw.SetActiveClass(bound, "text-xl text-white bg-indigo-600 font-bold p-5");
        var boolSwitch = new Switch();
        boolSwitch.Toggled += (_, e) => Tw.Maui.Tw.SetIsActive(bound, e.Value);
        rows.Add(Row("gap-4 items-center", boolSwitch, bound));

        Content = Page(this, rows.ToArray());
    }
}
