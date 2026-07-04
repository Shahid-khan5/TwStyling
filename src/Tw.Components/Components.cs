using Tw.Maui;

namespace Tw.Components;

public enum TwButtonVariant { Primary, Secondary, Outline, Ghost, Destructive }

public enum TwCardVariant { Elevated, Outlined, Filled }

public enum TwBadgeVariant { Neutral, Info, Success, Warning, Danger }

public enum TwSize { Sm, Md, Lg }

/// <summary>
/// Shared plumbing: components compose their look from preset utility-class
/// fragments plus the user's <c>Class</c> overrides (last wins, exactly like
/// Tailwind). Every unique composition compiles once and is cached by the engine,
/// so components cost the same as hand-written class strings.
/// </summary>
internal static class Compose
{
    public static string Join(string preset, string? size, string? extra) =>
        size is null or ""
            ? (string.IsNullOrEmpty(extra) ? preset : $"{preset} {extra}")
            : (string.IsNullOrEmpty(extra) ? $"{preset} {size}" : $"{preset} {size} {extra}");
}

public class TwButton : Button
{
    public static readonly BindableProperty VariantProperty = BindableProperty.Create(
        nameof(Variant), typeof(TwButtonVariant), typeof(TwButton), TwButtonVariant.Primary,
        propertyChanged: (b, _, _) => ((TwButton)b).Recompose());

    public static readonly BindableProperty SizeProperty = BindableProperty.Create(
        nameof(Size), typeof(TwSize), typeof(TwButton), TwSize.Md,
        propertyChanged: (b, _, _) => ((TwButton)b).Recompose());

    /// <summary>Extra utility classes appended after the preset (they win conflicts).</summary>
    public static readonly BindableProperty ClassProperty = BindableProperty.Create(
        nameof(Class), typeof(string), typeof(TwButton), null,
        propertyChanged: (b, _, _) => ((TwButton)b).Recompose());

    public TwButtonVariant Variant { get => (TwButtonVariant)GetValue(VariantProperty); set => SetValue(VariantProperty, value); }
    public TwSize Size { get => (TwSize)GetValue(SizeProperty); set => SetValue(SizeProperty, value); }
    public string? Class { get => (string?)GetValue(ClassProperty); set => SetValue(ClassProperty, value); }

    public TwButton() => Recompose();

    private static readonly Dictionary<TwButtonVariant, string> Variants = new()
    {
        [TwButtonVariant.Primary] = "bg-indigo-600 pressed:bg-indigo-800 text-white font-semibold rounded-lg",
        [TwButtonVariant.Secondary] = "bg-slate-100 dark:bg-slate-800 pressed:bg-slate-300 text-slate-900 dark:text-white font-medium rounded-lg",
        [TwButtonVariant.Outline] = "bg-transparent border-2 border-slate-300 dark:border-slate-600 pressed:border-indigo-500 text-slate-700 dark:text-slate-200 font-medium rounded-lg",
        [TwButtonVariant.Ghost] = "bg-transparent pressed:bg-slate-200 text-slate-700 dark:text-slate-200 font-medium rounded-lg",
        [TwButtonVariant.Destructive] = "bg-red-600 pressed:bg-red-800 text-white font-semibold rounded-lg",
    };

    private static readonly Dictionary<TwSize, string> Sizes = new()
    {
        [TwSize.Sm] = "px-3 py-1.5 text-sm",
        [TwSize.Md] = "px-4 py-2.5 text-sm",
        [TwSize.Lg] = "px-5 py-3 text-base",
    };

    private void Recompose() => Maui.Tw.SetClass(this, Compose.Join(Variants[Variant], Sizes[Size], Class));
}

public class TwCard : Border
{
    public static readonly BindableProperty VariantProperty = BindableProperty.Create(
        nameof(Variant), typeof(TwCardVariant), typeof(TwCard), TwCardVariant.Elevated,
        propertyChanged: (b, _, _) => ((TwCard)b).Recompose());

    public static readonly BindableProperty ClassProperty = BindableProperty.Create(
        nameof(Class), typeof(string), typeof(TwCard), null,
        propertyChanged: (b, _, _) => ((TwCard)b).Recompose());

    public TwCardVariant Variant { get => (TwCardVariant)GetValue(VariantProperty); set => SetValue(VariantProperty, value); }
    public string? Class { get => (string?)GetValue(ClassProperty); set => SetValue(ClassProperty, value); }

    public TwCard() => Recompose();

    private static readonly Dictionary<TwCardVariant, string> Variants = new()
    {
        [TwCardVariant.Elevated] = "bg-white dark:bg-slate-800 rounded-2xl shadow-md border-0 p-6",
        [TwCardVariant.Outlined] = "bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-700 rounded-xl p-6",
        [TwCardVariant.Filled] = "bg-slate-100 dark:bg-slate-800 rounded-xl border-0 p-6",
    };

    private void Recompose() => Maui.Tw.SetClass(this, Compose.Join(Variants[Variant], null, Class));
}

public class TwBadge : Label
{
    public static readonly BindableProperty VariantProperty = BindableProperty.Create(
        nameof(Variant), typeof(TwBadgeVariant), typeof(TwBadge), TwBadgeVariant.Neutral,
        propertyChanged: (b, _, _) => ((TwBadge)b).Recompose());

    public static readonly BindableProperty ClassProperty = BindableProperty.Create(
        nameof(Class), typeof(string), typeof(TwBadge), null,
        propertyChanged: (b, _, _) => ((TwBadge)b).Recompose());

    public TwBadgeVariant Variant { get => (TwBadgeVariant)GetValue(VariantProperty); set => SetValue(VariantProperty, value); }
    public string? Class { get => (string?)GetValue(ClassProperty); set => SetValue(ClassProperty, value); }

    public TwBadge() => Recompose();

    private const string Base = "text-xs font-medium rounded-full px-3 py-1";

    private static readonly Dictionary<TwBadgeVariant, string> Variants = new()
    {
        [TwBadgeVariant.Neutral] = "bg-slate-100 text-slate-700 dark:bg-slate-800 dark:text-slate-300",
        [TwBadgeVariant.Info] = "bg-sky-100 text-sky-700 dark:bg-sky-950 dark:text-sky-300",
        [TwBadgeVariant.Success] = "bg-emerald-100 text-emerald-700 dark:bg-emerald-950 dark:text-emerald-300",
        [TwBadgeVariant.Warning] = "bg-amber-100 text-amber-700 dark:bg-amber-950 dark:text-amber-300",
        [TwBadgeVariant.Danger] = "bg-red-100 text-red-700 dark:bg-red-950 dark:text-red-300",
    };

    private void Recompose() => Maui.Tw.SetClass(this, Compose.Join($"{Base} {Variants[Variant]}", null, Class));
}

public class TwDivider : BoxView
{
    public TwDivider() => this.Tw("h-px w-full bg-slate-200 dark:bg-slate-700 my-2");
}

/// <summary>Initials avatar: <c>new TwAvatar { Text = "AB" }</c>.</summary>
public class TwAvatar : Border
{
    public static readonly BindableProperty TextProperty = BindableProperty.Create(
        nameof(Text), typeof(string), typeof(TwAvatar), "",
        propertyChanged: (b, _, v) => ((TwAvatar)b)._label.Text = (string?)v);

    public string Text { get => (string)GetValue(TextProperty); set => SetValue(TextProperty, value); }

    private readonly Label _label;

    public TwAvatar()
    {
        _label = new Label().Tw("text-sm font-semibold text-indigo-700 dark:text-indigo-300 m-auto");
        Content = _label;
        this.Tw("size-10 rounded-full bg-indigo-100 dark:bg-indigo-950 border-0");
    }
}

/// <summary>
/// Entry wrapped in a styled Border with a focus ring — the classic Tailwind input.
/// Focus feedback is a class swap; the engine's plan cache makes that a lookup.
/// </summary>
public class TwInput : Border
{
    public static readonly BindableProperty TextProperty = BindableProperty.Create(
        nameof(Text), typeof(string), typeof(TwInput), "", BindingMode.TwoWay,
        propertyChanged: (b, _, v) =>
        {
            var input = (TwInput)b;
            if (input._entry.Text != (string?)v)
                input._entry.Text = (string?)v;
        });

    public static readonly BindableProperty PlaceholderProperty = BindableProperty.Create(
        nameof(Placeholder), typeof(string), typeof(TwInput), "",
        propertyChanged: (b, _, v) => ((TwInput)b)._entry.Placeholder = (string?)v);

    public string Text { get => (string)GetValue(TextProperty); set => SetValue(TextProperty, value); }
    public string Placeholder { get => (string)GetValue(PlaceholderProperty); set => SetValue(PlaceholderProperty, value); }

    private const string RestingClasses = "bg-white dark:bg-slate-900 border border-slate-300 dark:border-slate-600 rounded-lg px-3 py-0.5";
    private const string FocusedClasses = "bg-white dark:bg-slate-900 border-2 border-indigo-500 rounded-lg px-3 py-0.5";

    private readonly Entry _entry;

    public TwInput()
    {
        _entry = new Entry().Tw("text-sm text-slate-900 dark:text-white bg-transparent");
        _entry.TextChanged += (_, e) => Text = e.NewTextValue;
        _entry.Focused += (_, _) => this.Tw(FocusedClasses);
        _entry.Unfocused += (_, _) => this.Tw(RestingClasses);
        Content = _entry;
        this.Tw(RestingClasses);
    }
}
