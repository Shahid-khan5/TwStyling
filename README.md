# Tw — Tailwind utility styling for native .NET UI

Style native controls with the Tailwind vocabulary AI models (and web developers) already know:

```xml
<ContentPage xmlns:tw="https://tw" ...>
    <Border tw:Tw.Class="bg-white dark:bg-slate-800 rounded-2xl shadow-md p-6">
        <VerticalStackLayout tw:Tw.Class="gap-3">
            <Label tw:Tw.Class="text-xl font-bold text-slate-900 dark:text-white" Text="Upgrade to Pro" />
            <Label tw:Tw.Class="text-sm text-slate-500 dark:text-slate-400" Text="Unlock unlimited projects." />
            <Button tw:Tw.Class="bg-indigo-600 pressed:bg-indigo-800 text-white font-semibold rounded-lg px-4 py-3"
                    Text="Get started" />
        </VerticalStackLayout>
    </Border>
</ContentPage>
```

Or in C#: `new Label().Tw("text-xl font-bold")`.

**Why:** native platforms give you real controls, accessibility, and performance a WebView can't; the Tailwind vocabulary gives you a design system plus everything AI models learned from millions of well-designed web components. This engine is the bridge — an AI-legibility layer for native UI.

## Projects

| Project | What it is |
|---|---|
| `src/Tw.Core` | Framework-neutral engine: span-based parser, Tailwind token tables, resolver, cached `StylePlan`s. `net10.0` + `netstandard2.0`, zero UI references. |
| `src/Tw.Maui` | MAUI adapter: `Tw.Class` attached property, per-control property mapping, VisualStateManager bridge, `dark:` via AppThemeBinding. |
| `src/Tw.Analyzers` | Roslyn analyzer (TW0001): validates class-string literals in C# at build time using the same parser the runtime uses. |
| `samples/Gallery` | Demo app (Windows TFM for fast iteration) — the acceptance test: AI-idiom components pasted in unedited. |
| `tests/Tw.Core.Tests` | Engine unit tests. |
| `tests/Tw.Benchmarks` | BenchmarkDotNet perf contract. |

## Setup

```csharp
// MauiProgram.cs — optional; the engine self-initializes, this picks diagnostics behavior
builder.UseTw(o => o.DiagnosticMode = TwDiagnosticMode.Throw); // Throw in Debug/CI, DebugOutput in prod
```

XAML namespace: `xmlns:tw="https://tw"` (or `clr-namespace:Tw.Maui;assembly=Tw.Maui`).

## Supported utilities (~85% of AI-emitted Tailwind, usage-weighted)

| Area | Utilities |
|---|---|
| Color | `bg-*` `text-*` `border-*` — full Tailwind palette, `white/black/transparent`, opacity `bg-black/50`, arbitrary `bg-[#rrggbb]` |
| Gradients | `bg-gradient-to-{t,tr,r,br,b,bl,l,tl}` + `from-*` `via-*` `to-*` |
| Spacing | `p/px/py/pt/pr/pb/pl-*`, `m*-*` (negatives ok), `m/mx/my/ml/mr/mt/mb-auto` (→ alignment), `gap-*` `gap-x/y-*`, values: scale, `px`, `2.5`, `[13px]` |
| Typography | `text-xs…9xl`, `font-thin…black`, `italic`, `tracking-*`, `leading-*`, `line-clamp-N`, `truncate`, `underline`/`line-through`, `uppercase`/`lowercase`, `text-left/center/right/justify` |
| Shape | `rounded(-none/sm/md/lg/xl/2xl/3xl/full)`, per-side `rounded-t-lg`, `border`, `border-0/2/4/8` |
| Effects | `shadow(-sm/md/lg/xl/2xl/none)`, `opacity-0…100` |
| Sizing | `w/h-*`, `size-*`, `w-full`, `min/max-w/h-*`, named `max-w-sm…7xl` |
| Flexbox | `flex`, `flex-row/col(-reverse)`, `flex-wrap/nowrap`, `flex-1/auto/initial/none`, `justify-*`, `items-*`, `self-*`, `grow(-N)`, `shrink(-N)`, `basis-{N,auto,full,1/2}` (on `FlexLayout` / children) |
| Grid | `grid-cols-N`, `grid-rows-N` (star tracks), `col/row-span-N`, `col/row-start-N` (on `Grid` / children) |
| Transforms | `rotate-*`, `scale-*`, `translate-x/y-*`, `z-*` |
| Transitions | `transition(-colors/opacity/transform/all/none)`, `duration-N`, `delay-N`, `ease-*` — animates class changes, theme flips, breakpoint crossings, AND interactive states (with transition-*, pressed/hover/focus tween instead of snapping); `transition-all` also tweens font size |
| Animations | `animate-spin`, `animate-pulse`, `animate-bounce`, `animate-none` — engine-managed loops |
| Visibility | `hidden`, `visible`, `invisible`, `overflow-hidden/visible` |

**Variants** (stackable, e.g. `md:dark:bg-indigo-400`):

| Class | Prefixes | Cost |
|---|---|---|
| Platform | `android:` `ios:` `mac:` `windows:` `tizen:` `mobile:` | zero — filtered at plan compile |
| Idiom | `phone:` `tablet:` `desktop:` `tv:` `watch:` | zero — filtered at plan compile |
| Theme | `dark:` `light:` | AppThemeBinding on differing properties only |
| Interactive | `pressed:`/`active:` `hover:` `focus:` `disabled:` | VisualStateManager, built only when present |
| Responsive | `sm:` `md:` `lg:` `xl:` `2xl:` (window width 640/768/1024/1280/1536) | overlay entries + window-size tier tracking |

Deliberate deviations from web Tailwind: `pressed:` is first-class (touch-first); `hover:` maps to PointerOver; auto margins become `LayoutOptions`. Not yet supported (loud diagnostic, never a silent no-op): `divide-*`/`space-*`/`ring-*`, `group-*`/`peer-*`, fractional widths, font families, breakpoints combined with interactive variants.

## Binding-driven styling

Different font size / colors / anything based on a view-model boolean — with animation:

```xml
<Label tw:Tw.Class="transition-all duration-300 text-sm text-slate-500"
       tw:Tw.ActiveClass="text-2xl text-white bg-indigo-600 font-bold"
       tw:Tw.IsActive="{Binding IsImportant}" />
```

`ActiveClass` utilities are appended (last-wins, like Tailwind) while `IsActive` is true. Both compositions are cached plans, so toggling is two dictionary lookups — and with `transition-*` in the base classes the switch animates.

## Tw.Components

A component library built entirely on the engine — every look is a utility-class preset, every unique composition compiles once:

```csharp
new TwButton { Text = "Get started", Variant = TwButtonVariant.Primary, Size = TwSize.Lg }
new TwCard { Variant = TwCardVariant.Outlined, Class = "rounded-3xl" }  // Class overrides win
new TwBadge { Text = "Beta", Variant = TwBadgeVariant.Warning }
new TwInput { Placeholder = "you@example.com" }  // focus ring via cached class swap
new TwAvatar { Text = "CF" }, new TwDivider()
```

`builder.UseTwComponents()` registers handler-mapper customizations (e.g. neutralizing WinUI's built-in button hover chrome so the `pressed:` classes own interaction feedback) — the MAUI-blessed way to adjust platform behavior.

## Performance model

Class strings are static in practice, so everything is *compute once per unique string, share across all elements*:

```
"bg-indigo-600 rounded-lg px-4"      (once per unique string)
  → span tokenizer → static token tables → StylePlan (immutable, platform-filtered)
  → lowered per control type to (BindableProperty, boxed value) pairs   (once per (string, type))
  → per element: a for-loop of SetValue                                  (the hot path)
```

Measured (see `tests/Tw.Benchmarks`): **cache hit ~53 ns, 0 B allocated**; cold compile of a 10-utility string **~4 µs**. No reflection anywhere — trimming/AOT safe.

## Build-time validation

Reference `Tw.Analyzers` as an analyzer and typos become build warnings with fix hints:

```
warning TW0001: 'bg-nope-500': unknown color 'nope-500'
warning TW0001: 'sr-only': 'sr-only' has no native equivalent — use SemanticProperties instead
```

Covers C# literals today; XAML validation via build task is planned. At runtime the same diagnostics flow to `TwRuntime.DiagnosticSink` / `DiagnosticMode`.

## Regenerating the palette

`src/Tw.Core/Generated/TwPalette.g.cs` is generated from the official Tailwind source:

```powershell
./tools/gen-palette.ps1 -InputFile tools/colors-v3.4.17.js -OutputFile src/Tw.Core/Generated/TwPalette.g.cs
```

## Roadmap

- **v1**: FlexLayout mapping (`flex`, `items-*`, `justify-*`), `transition-*`/`duration-*` animations, XAML build-time validation, responsive `md:`/`lg:`, theme customization (`tailwind.config`-style JSON)
- **v1.5**: WPF adapter (`Tw.Wpf`) — doubles as the audit of the adapter abstraction
- **Later**: docs/Claude-skill generation from the token tables (single source of truth), higher-level component library built on the engine
