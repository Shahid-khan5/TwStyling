using TwStyling.Maui;
using static Gallery.Pages.GalleryUi;

namespace Gallery.Pages;

/// <summary>
/// The capabilities that only exist because the project is compiled by the real Tailwind CLI.
/// Every class string on this page is a literal, so it is precompiled — none of it would resolve
/// through the built-in class-name parser, which knows only the standard scale.
/// </summary>
public class TailwindCssPage : ContentPage
{
    public TailwindCssPage()
    {
        Title = "Tailwind CSS";

        Content = Page(this,
            Section("Arbitrary values"),
            Caption("Any value, not just the scale — Tailwind resolves these, we lower them."),
            Row("gap-2 items-center",
                Code("w-[137px]"),
                new Border().Tw("w-[137px] h-8 bg-indigo-500 rounded-md border-0")),
            Row("gap-2 items-center",
                Code("text-[13px]"),
                new Label { Text = "thirteen pixels exactly" }.Tw("text-[13px] text-slate-700 dark:text-slate-300")),
            Row("gap-2 items-center",
                Code("rounded-[7px]"),
                new Border().Tw("size-8 bg-emerald-500 rounded-[7px] border-0")),
            Row("gap-2 items-center",
                Code("bg-[#ff6b6b]"),
                new Border().Tw("size-8 bg-[#ff6b6b] rounded-md border-0")),

            Section("Custom @theme tokens"),
            Caption("--color-brand-600 is declared in tw.css. The engine never hardcoded it."),
            Row("gap-2 items-center",
                Code("bg-brand-600"),
                new Border().Tw("size-8 bg-brand-600 rounded-md border-0"),
                new Label { Text = "text-brand-600" }.Tw("text-brand-600 font-semibold")),

            Section("Opacity modifiers"),
            Caption("Resolved through color-mix() in oklab, exactly as Tailwind defines it."),
            Row("gap-1",
                new Border().Tw("size-8 bg-brand-600 rounded-md border-0"),
                new Border().Tw("size-8 bg-brand-600/75 rounded-md border-0"),
                new Border().Tw("size-8 bg-brand-600/50 rounded-md border-0"),
                new Border().Tw("size-8 bg-brand-600/25 rounded-md border-0")),

            Section("Gradients"),
            Caption("background-image composes across three rules through custom properties."),
            new Border().Tw("h-12 rounded-lg border-0 bg-linear-to-r from-sky-400 via-indigo-500 to-fuchsia-500"),

            Section("The v4 palette"),
            Caption("bg-blue-500 is oklch(62.3% 0.214 259.815) — #2B7FFF in v4, not v3's #3B82F6."),
            Row("gap-2 items-center",
                new Border().Tw("size-8 bg-blue-500 rounded-md border-0"),
                new Label { Text = "#2B7FFF" }.Tw("text-sm text-slate-600 dark:text-slate-400")),

            Section("Custom variants"),
            Caption("pressed: and windows:/android: are declared with @custom-variant in tw.css."),
            new Button { Text = "Press me" }
                .Tw("bg-indigo-600 pressed:bg-indigo-800 text-white font-semibold rounded-lg px-4 py-3 transition-all duration-150"),
            new Label { Text = "This padding differs per platform head (windows:p-5 android:p-2)." }
                .Tw("windows:p-5 android:p-2 bg-slate-100 dark:bg-slate-800 rounded-md text-sm text-slate-700 dark:text-slate-300"));
    }
}
