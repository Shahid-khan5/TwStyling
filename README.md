# TwStyling — Tailwind utility styling for native .NET UI

[![NuGet: TwStyling.Maui](https://img.shields.io/nuget/vpre/TwStyling.Maui?label=TwStyling.Maui)](https://www.nuget.org/packages/TwStyling.Maui)
[![NuGet: TwStyling](https://img.shields.io/nuget/vpre/TwStyling?label=TwStyling)](https://www.nuget.org/packages/TwStyling)

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
tw.css ──(Tailwind CLI)──▶ CSS ──(TwStyling.Css)──▶ StylePlan ──(codegen)──▶ static data
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

## Install

Two packages are published on [nuget.org](https://www.nuget.org/packages/TwStyling.Maui):

| Package | Install | Use when |
|---|---|---|
| [`TwStyling.Maui`](https://www.nuget.org/packages/TwStyling.Maui) | `dotnet add package TwStyling.Maui --prerelease` | You're building a MAUI app — this is the one you want. |
| [`TwStyling`](https://www.nuget.org/packages/TwStyling) | `dotnet add package TwStyling --prerelease` | You only want the framework-neutral engine (CSS lowering, value evaluator, `StylePlan` cache) with no MAUI dependency. |

(`--prerelease` is needed while the version is a release candidate — see [Status](#status).)

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

A `tw.css` is optional: without one the build generates a default entry stylesheet (Tailwind plus the
native variants, scanning your sources) so the pipeline still runs. You add a `tw.css` when you want
to customise — `@theme` tokens, `@utility`, plugins, safelists.

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

**Measured** (`tests/TwStyling.Benchmarks`, plus a 1000-cell `CollectionView` and a leak probe in the
Gallery): plan cache hit **~53 ns / 0 B**. A full restyle of a live element is ~22 µs and **184 B** —
the time is MAUI's `SetValue` floor, not ours. Leak probe: 0 survivors across 4 scenarios.

---

## Project layout

| Project | What it is |
|---|---|
| `src/TwStyling` | The engine: CSS → `StylePlan` lowering, a CSS value evaluator (`var()`, `calc()`, `oklch`, `color-mix`), plan cache. Framework-neutral, `net10.0` + `netstandard2.0`. |
| `src/TwStyling.Maui` | MAUI adapter: `Tw.Class`, per-control property mapping, state reconciler, theme swap. |
| `src/TwStyling.Analyzers` | Roslyn analyzer (TW0001) — validates class strings in C#. |
| `src/TwStyling.Generators` | Source generator — lowers the Tailwind CLI's CSS into preloaded plans at build time. |
| `src/TwStyling.Css` | The CSS parser/evaluator sources, compiled into the engine. Not a package: it is an internal AST, not API. |
| `samples/Gallery` | Demo, acceptance test, and the stress / leak / perf harness. |

Two packages: **`TwStyling.Maui`** (adapter) and **`TwStyling`** (engine, no MAUI dependency).
Assembly names, namespaces, and package IDs all match.

---

## One vocabulary

Tailwind is the *only* thing that decides what a class means. The engine has no class-name parser and
no copy of Tailwind's theme — those existed once, and drifted: the same palette bug had to be fixed in
them twice. They are deleted.

The consequence is that every path agrees by construction:

| | resolved by |
|---|---|
| a literal in XAML or C# | the stylesheet, at build time |
| an interpolated string (`$"bg-{shade}"`) | the same stylesheet, embedded in the app |
| `idiom:` variants (one iOS build serves iPhone and iPad) | the same stylesheet, on the device |
| the `TW0001` analyzer in your IDE | the same stylesheet |

Tailwind only emits rules for classes it can *see*, so a class built at runtime must be safelisted:

```css
@source inline("bg-{red,blue,green}-{100,500,900}");
```

Anything the engine cannot render is a diagnostic, never a silent no-op — `float-left` tells you to
use a layout container, `space-x-2` tells you to use `gap-*`.

## Status

**1.0.0-rc.1.** The architecture is settled, the API is what 1.0 will ship, and 256 tests plus a live
run on WinUI cover it — including an in-app check that build-time and runtime resolution of the same
class string produce the same values.

It is `rc` and not final for one reason: **iOS and Android have not been run.** All four heads build
in CI and their generated plans are verified correct per platform (`windows:p-5 android:p-3 ios:p-4`
yields 20px, 12px and 16px respectively), but "it compiles" is not "it renders". That needs a device.

Also worth knowing:

- **Colors are Tailwind v4's.** `bg-blue-500` is `#2B7FFF` — the oklch value Tailwind specifies — not
  v3's `#3B82F6`. If you pinned colors against an older preview, they will shift.
- Grid `col-end-*` / `row-end-*` are not lowered yet (`col-span-*` is).

## Roadmap

- **1.0**: run it on iOS and Android.
- **Next**: a WPF adapter — which doubles as the audit of whether the adapter abstraction is real.
- **Later**: a component library on the engine.

MIT.
