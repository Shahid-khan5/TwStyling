using TwStyling.Maui;

namespace Gallery;

/// <summary>
/// The fluent C# markup path. The TwStyling.Analyzers project validates these literals
/// at build time — try changing a class to "bg-nope-500" and rebuild.
/// </summary>
public static class CSharpMarkupDemo
{
    public static View BuildCard() =>
        new Border
        {
            Content = new VerticalStackLayout
            {
                Children =
                {
                    new Label { Text = "Built in C#" }
                        .Tw("text-lg font-semibold text-slate-900 dark:text-white"),
                    new Label { Text = "Same engine, same cache, no XAML." }
                        .Tw("text-sm text-slate-500 dark:text-slate-400"),
                },
            }.Tw("gap-2"),
        }.Tw("bg-white dark:bg-slate-800 rounded-xl shadow-sm p-5");
}
