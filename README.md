# TwStyling — Tailwind utility styling for native .NET UI

[![NuGet: TwStyling.Maui](https://img.shields.io/nuget/vpre/TwStyling.Maui?label=TwStyling.Maui)](https://www.nuget.org/packages/TwStyling.Maui)
[![NuGet: TwStyling](https://img.shields.io/nuget/vpre/TwStyling?label=TwStyling)](https://www.nuget.org/packages/TwStyling)

Style native .NET MAUI controls with Tailwind classes:

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

There is no WebView and no CSS engine on the device. That `Label` is an ordinary MAUI `Label` with
`FontSize` and `FontAttributes` set — real native controls, real platform accessibility.

**The vocabulary is Tailwind's own.** TwStyling does not reimplement Tailwind's grammar: at build
time it runs the actual Tailwind CSS compiler over your project, scanning your `.xaml` and `.cs` for
class strings exactly as it would scan `.jsx`, and lowers the CSS it emits into native styling. So
arbitrary values, opacity modifiers, `@theme` tokens, `@utility` and `@custom-variant` all work —
because Tailwind resolves them, not us:

```xml
<Label tw:Tw.Class="w-[137px] text-[13px] bg-brand-600/50" />
```

When Tailwind ships a new utility, you get it by bumping the CLI version.

The reason to want any of this: models write excellent Tailwind and mediocre XAML, because their
training data is overwhelmingly web. TwStyling lets a model emit what it is good at and renders it
natively.

---

## Install

```
dotnet add package TwStyling.Maui --prerelease
```

That is the whole install. On first build the standalone Tailwind CLI (a Node-free binary) is fetched
once into your NuGet cache; set `<TailwindCliPath>` to your own copy for offline or CI builds.

Then use the XAML namespace `xmlns:tw="https://tw"`, and optionally choose how problems surface:

```csharp
// MauiProgram.cs — optional
builder.UseTw(o => o.DiagnosticMode = TwDiagnosticMode.Throw);  // Throw in CI, DebugOutput in prod
```

To customise Tailwind — `@theme` tokens, `@utility`, plugins, safelists — add a `tw.css` next to your
`.csproj`. Without one, the build generates a default entry stylesheet for you, so the pipeline runs
either way.

```css
@import "tailwindcss";

/* Native variants. Tailwind passes an unknown media type through verbatim, and
   TwStyling resolves it at build time for the platform head being compiled. */
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

There is a second package, [`TwStyling`](https://www.nuget.org/packages/TwStyling) — the
framework-neutral engine, with no MAUI dependency. You only need it directly if you are building an
adapter for another toolkit.

---

## Using it

**Utilities.** Color (full palette, `bg-black/50`, `bg-[#hex]`), gradients, spacing, typography,
shape, shadows, sizing, flexbox, grid, transforms, transitions, animations, visibility — most of what
Tailwind expresses that a native toolkit *can* express. [COVERAGE.md](COVERAGE.md) is the honest
accounting, including what is web-only and why.

**Variants**, stackable (`md:dark:bg-indigo-400`):

| Kind | Prefixes |
|---|---|
| Platform | `android:` `ios:` `mac:` `windows:` |
| Idiom | `phone:` `tablet:` `desktop:` |
| Theme | `dark:` `light:` |
| Interactive | `pressed:` `hover:` `focus:` `disabled:` |
| Responsive | `sm:` `md:` `lg:` `xl:` `2xl:` |

`pressed:` is first-class (touch-first) and `hover:` maps to PointerOver — deliberate deviations from
web Tailwind.

**Binding-driven styling**, with animation:

```xml
<Label tw:Tw.Class="transition-all duration-300 text-sm text-slate-500"
       tw:Tw.ActiveClass="text-2xl text-white bg-indigo-600 font-bold"
       tw:Tw.IsActive="{Binding IsImportant}" />
```

`ActiveClass` is appended last-wins while `IsActive` holds, and `transition-*` in the base classes
makes the switch animate instead of snap.

**Nothing is ever a silent no-op.** A utility that cannot be rendered natively fails your build, and
tells you what to do instead:

```
error TWG001: 'float-left': 'float' has no native analog — use layout containers
```

---

## How it works

Everything expensive happens during `dotnet build`:

```
tw.css ──(Tailwind CLI)──▶ CSS ──(lowering)──▶ StylePlan ──(codegen)──▶ static data
                                                                     │
                                  runtime: dictionary hit ◀──────────┘
                                               │
                                     SetValue on the control
```

Tailwind compiles your stylesheet. TwStyling lowers each CSS declaration onto a framework-neutral
style plan — this is the trick that makes the whole thing tractable, because the Tailwind catalog
grows without bound but the set of CSS properties a native control can consume does not. `bg-red-500`,
`bg-[#abc]`, `bg-red-500/50` and `bg-(--brand)` are four utilities but one declaration:
`background-color`. A source generator then bakes the resulting plans into your assembly.

At runtime, applying a class is a dictionary lookup and a loop of `SetValue`. Nothing is parsed and
nothing is reflected, so it is trimming- and AOT-safe.

**One vocabulary.** Tailwind is the only thing that decides what a class means. The engine carries no
class-name parser and no copy of Tailwind's theme — those existed once, and drifted from Tailwind. So
every path agrees by construction: a literal in your XAML, an interpolated string like `$"bg-{shade}"`,
an `idiom:` variant that can only be resolved on the device, and the analyzer in your IDE all resolve
against the same compiled stylesheet.

The one thing to know about that: Tailwind only emits rules for classes it can *see*. A class your
code assembles at runtime is invisible to it, so safelist it:

```css
@source inline("bg-{red,blue,green}-{100,500,900}");
```

---

## Status

**1.0.0-rc.1.** The architecture is settled and the API is what 1.0 will ship.

It is a release candidate for one reason: **iOS and Android have not been run on a device.** All four
platform heads build and their generated styling is verified correct per platform, but "it compiles"
is not "it renders."

Two other things worth knowing:

- **Colors are Tailwind v4's.** `bg-blue-500` is `#2B7FFF` — the oklch value Tailwind v4 specifies —
  not v3's `#3B82F6`. If you pinned colors against an older preview of this library, they will shift.
- Grid `col-end-*` / `row-end-*` are not lowered yet (`col-span-*` is).

MIT.
