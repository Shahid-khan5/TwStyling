# Tw — Tailwind utility styling for native .NET UI

Style native controls with the Tailwind vocabulary that AI models — and web developers — already know:

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

---

## The three things this library is for

### 1. Real Tailwind, not a lookalike

Tw does not reimplement Tailwind's grammar. At build time it runs **the actual Tailwind CSS
compiler** over your project, scanning your `.xaml` and `.cs` for class strings exactly as it would
scan `.jsx`. The CSS it emits is lowered into native style plans.

So the vocabulary is Tailwind's, not an approximation of it:

```xml
<Label tw:Tw.Class="w-[137px] text-[13px] bg-brand-600/50" />
```

Arbitrary values, opacity modifiers, `@theme` design tokens, `@utility`, `@custom-variant` — they
work because Tailwind itself resolves them. When Tailwind ships a new utility, you get it by bumping
the CLI version, not by waiting for us to reimplement it.

### 2. Native controls, and nothing at runtime

No WebView. No CSS engine on the device. No runtime parsing. A `Label` styled with `text-xl
font-bold` is an ordinary `Label` with `FontSize` and `FontAttributes` set — real platform
accessibility, real controls, native performance.

Everything cold happens during `dotnet build`:

```
tw.css ──(Tailwind CLI)──▶ CSS ──(Tw.Css)──▶ StylePlan ──(codegen)──▶ static data
                                                                          │
                                       runtime: dictionary hit ◀──────────┘
                                                    │
                                          SetValue on the control
```

Applying a class is a dictionary lookup and a loop of `SetValue`. Nothing is parsed, nothing is
reflected — trimming- and AOT-safe.

### 3. AI-legible native UI

This is the point of the whole exercise. Models write beautiful Tailwind and mediocre XAML, because
the training data is overwhelmingly web. Tw lets a model emit what it is good at and renders it
natively. The acceptance test for this repo is literally: *paste an AI-generated component in
unedited, and it looks right.*

Unknown or web-only utilities are never silent no-ops — they fail at build time, with a hint:

```
error TWG001: 'float-left': 'float' has no native analog — use layout containers
```

---

## Setup

```
dotnet add package TwStyling.Maui
```

Add a `tw.css` next to your `.csproj`:

```css
@import "tailwindcss";

/* Native variants. Tailwind passes an unknown media type through verbatim,
   and Tw resolves it at build time for the platform head being compiled. */
@custom-variant windows (@media windows);
@custom-variant android (@media android);
@custom-variant ios (@media ios);
@custom-variant phone (@media phone);
@custom-variant tablet (@media tablet);
@custom-variant desktop (@media desktop);
@custom-variant pressed (&:active);

@source "./**/*.xaml";
@source "./**/*.cs";
```

That is the whole install. On first build the standalone Tailwind CLI (a Node-free binary) is fetched
once into your NuGet cache; set `<TailwindCliPath>` to your own copy for offline or CI builds.

Without a `tw.css`, Tw falls back to its built-in class-name parser and everything still works — you
just lose arbitrary values, custom tokens, and plugins.

XAML namespace: `xmlns:tw="https://tw"`.

```csharp
// MauiProgram.cs — optional; picks how problems surface
builder.UseTw(o => o.DiagnosticMode = TwDiagnosticMode.Throw);  // Throw in CI, DebugOutput in prod
```

---

## What you get

**Utilities.** Color (full palette, `bg-black/50`, `bg-[#hex]`), gradients, spacing, typography,
shape, shadows, sizing, flexbox, grid, transforms, transitions, animations, visibility — about 95% of
what Tailwind expresses that a native toolkit *can* express. [COVERAGE.md](COVERAGE.md) has the
honest accounting, including what is web-only and why.

**Variants**, stackable (`md:dark:bg-indigo-400`):

| Class | Prefixes | Cost |
|---|---|---|
| Platform | `android:` `ios:` `mac:` `windows:` | **zero** — compiled out per build head |
| Idiom | `phone:` `tablet:` `desktop:` | runtime (one iOS head serves iPhone *and* iPad) |
| Theme | `dark:` `light:` | `AppThemeBinding`, on differing properties only |
| Interactive | `pressed:` `hover:` `focus:` `disabled:` | native events → state vector → reconcile |
| Responsive | `sm:` `md:` `lg:` `xl:` `2xl:` | overlay entries + window-width tracking |

`pressed:` is first-class (touch-first) and `hover:` maps to PointerOver — deliberate deviations from
web Tailwind.

**Binding-driven styling**, with animation:

```xml
<Label tw:Tw.Class="transition-all duration-300 text-sm text-slate-500"
       tw:Tw.ActiveClass="text-2xl text-white bg-indigo-600 font-bold"
       tw:Tw.IsActive="{Binding IsImportant}" />
```

`ActiveClass` is appended last-wins while `IsActive` holds. Both compositions are cached plans, so
toggling is two dictionary lookups — and `transition-*` in the base classes makes the switch animate
instead of snap.

**Measured** (`tests/Tw.Benchmarks`, plus a 1000-cell `CollectionView` and a leak probe in the
Gallery): plan cache hit **~53 ns / 0 B**. A full restyle of a live element is ~22 µs and **184 B** —
the time is MAUI's `SetValue` floor, not ours. Leak probe: 0 survivors across 4 scenarios.

---

## Project layout

| Project | What it is |
|---|---|
| `src/Tw.Css` | CSS component-value parser and evaluator: `var()`, `calc()` with unit algebra, CSS Color 4 (`oklch`, `color-mix`). No UI dependency. |
| `src/Tw.Core` | Framework-neutral engine: CSS → `StylePlan` lowering, token tables, plan cache. `net10.0` + `netstandard2.0`. |
| `src/Tw.Maui` | MAUI adapter: `Tw.Class`, per-control property mapping, state reconciler, theme swap. |
| `src/Tw.Analyzers` | Roslyn analyzer (TW0001) — validates class strings in C#. |
| `src/Tw.Generators` | Source generator — lowers the Tailwind CLI's CSS into preloaded plans at build time. |
| `samples/Gallery` | Demo, acceptance test, and the stress / leak / perf harness. |

Packages: **`TwStyling.Maui`** (adapter) and **`TwStyling`** (engine, no MAUI dependency). The
assemblies and namespaces are `Tw.Maui` / `Tw.Core`; the package IDs differ only because nuget.org
reserves the short `Tw.` prefix.

---

## Status

**Preview — not production ready.** The architecture is settled and the engine is well covered (269
tests, live-verified on WinUI), but the CSS pipeline is new. Before shipping, know:

- **Two palettes.** Literal class strings compile through Tailwind v4; interpolated ones
  (`$"bg-{family}-{shade}"`) still fall through to the legacy parser and its **v3** color tables. An
  app can render both. Fix in progress.
- **Colors shift.** Adopting real v4 changes rendered colors — `bg-blue-500` moves `#3B82F6` →
  `#2B7FFF`. Correct, but visible.
- **Heads exercised:** Windows and Android. iOS/macOS build but are unverified end to end.
- Grid `col-end-*` / `row-end-*` are not lowered yet.

## Roadmap

- **v1**: one palette everywhere (retire the legacy parser), then a WPF adapter — which doubles as
  the audit of whether the adapter abstraction is real.
- **Later**: a component library on the engine; docs and the Claude skill generated from the same
  tables.

MIT.
