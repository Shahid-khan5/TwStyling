# Tailwind Coverage

How much of Tailwind CSS the Tw engine implements for native MAUI, as of this pass.

Tailwind was designed for the browser. A large part of its catalog describes things
that **do not exist in a native UI toolkit** — floats, columns, CSS filters, backdrop
effects, scroll-snap, blend modes, pseudo-element `content`, table layout, SVG. Those
are not "missing"; they have no native analog. This engine's job is to cover the
utilities that *do* map to native concepts, and to emit a **loud, helpful diagnostic**
for the rest instead of silently doing nothing.

So there are two honest numbers:

| Denominator | Coverage |
|---|---|
| **Natively-mappable utility groups** (the ones that *can* work on native) | **~88%** |
| **Usage-weighted** (the classes real app UIs / AI actually emit: color, spacing, flex, text, rounded, shadow, sizing) | **~95%+** |
| Raw Tailwind catalog (incl. ~120 web-only groups) | ~27% |

The raw-catalog number is intentionally deprioritized — most of the remainder is
structurally inapplicable to native UI.

---

## Legend

- ✅ **Supported** — implemented, values resolve
- ◑ **Partial** — common values supported, some variants missing
- ✗ **Missing** — has a native mapping but not yet implemented
- ⛔ **N/A** — no native analog; emits a helpful diagnostic

---

## Layout

| Utility | Status | Notes |
|---|---|---|
| `display` (flex/grid/hidden) | ◑ | flex, grid, hidden ✅; block/inline/table ⛔ |
| `object-fit` | ✅ | `object-contain/cover/fill/none/scale-down` → `Image.Aspect` |
| `overflow` | ◑ | `overflow-hidden/visible` ✅; scroll/auto → use `ScrollView` |
| `visibility` | ✅ | `visible`, `invisible` |
| `z-index` | ✅ | `z-*` |
| `aspect-ratio` | ✗ | no direct MAUI property; diagnostic |
| `position` / `inset` / `top…` | ⛔ | diagnostic → use layout, margins, translate |
| container, columns, break-*, box-*, float, clear, isolation, object-position, overscroll | ⛔ | web-only |

## Flexbox & Grid

| Utility | Status | Notes |
|---|---|---|
| `flex-basis` `basis-*` | ✅ | incl. fractions `basis-1/2` |
| `flex-direction` | ✅ | row/col/reverse |
| `flex-wrap` | ✅ | wrap/nowrap/reverse |
| `flex` | ✅ | flex-1/auto/initial/none |
| `flex-grow` / `flex-shrink` | ✅ | grow(-0/-n), shrink(-0/-n) |
| `order` | ✅ | `order-*`, `order-first/last/none`, negative → `FlexLayout.Order` |
| `grid-template-columns/rows` | ✅ | `grid-cols-N`, `grid-rows-N` (star sizing) |
| `grid-column`/`grid-row` | ◑ | span + start ✅; `col-end`/`row-end` ✗ |
| `gap` | ✅ | `gap-*`, `gap-x/y-*` |
| `justify-content` | ✅ | start/end/center/between/around/evenly |
| `align-content` `content-*` | ✅ | → `FlexLayout.AlignContent` (evenly≈around) |
| `align-items` `items-*` | ✅ | |
| `align-self` `self-*` | ✅ | |
| `justify-items` / `justify-self` / `place-*` | ✗ | limited native equivalent |
| grid-auto-flow/cols/rows | ⛔ | |

## Spacing

| Utility | Status | Notes |
|---|---|---|
| `padding` p/px/py/pt/pr/pb/pl | ✅ | |
| `margin` m/mx/…, negative, `m-auto` | ✅ | auto margins → self-alignment |
| `space-x/y` | ⛔ | diagnostic → use `gap-*` |

## Sizing

| Utility | Status | Notes |
|---|---|---|
| `width` `w-*`, `w-full`, `w-auto` | ✅ | fractions/`w-fit/min/max` → diagnostic |
| `min-width` / `max-width` | ✅ | `max-w-*` named scale |
| `height` / `min-height` / `max-height` | ✅ | `h-full` |
| `size-*` | ✅ | |

## Typography

| Utility | Status | Notes |
|---|---|---|
| `font-size` `text-xs…9xl`, `text-[n]` | ✅ | sets size + line-height |
| `font-weight` | ✅ | thin…black → FontAttributes.Bold at ≥600 |
| `font-style` italic | ✅ | |
| `letter-spacing` `tracking-*` | ✅ | em → DIU via plan font size |
| `line-height` `leading-*` | ✅ | named **and numeric** (`leading-6`) |
| `line-clamp-*` / `truncate` | ✅ | |
| `text-align` | ✅ | left/center/right/justify/start/end |
| `text-color` | ✅ | full palette + `/opacity` + `text-[#hex]` |
| `text-decoration` | ✅ | underline / line-through / no-underline |
| `text-transform` | ✅ | uppercase/lowercase/normal-case (capitalize ⛔) |
| `whitespace` | ◑ | nowrap/normal → `LineBreakMode` |
| `word-break` | ✅ | normal/words/all → `LineBreakMode` |
| `font-family` | ◑ | diagnostic → set `FontFamily` directly |
| `text-indent`, `text-decoration-color/style/thickness`, underline-offset | ✗/⛔ | |
| list-style, font-variant, vertical-align, hyphens, content | ⛔ | |

## Backgrounds

| Utility | Status | Notes |
|---|---|---|
| `background-color` `bg-*`, `bg-*/opacity`, `bg-[#hex]` | ✅ | |
| `background-image` `bg-gradient-to-*` | ✅ | 8 directions → LinearGradientBrush |
| gradient stops `from/via/to-*` | ✅ | transparent-fade semantics |
| bg clip/origin/position/repeat/size/attachment/blend | ⛔ | web-only |

## Borders

| Utility | Status | Notes |
|---|---|---|
| `border-radius` `rounded-*` | ✅ | per-corner + per-side |
| `border-width` | ✅ | `border`, `border-0/2/4/8`, **per-side `border-t/r/b/l/x/y-*`** carried as Edges (WPF `BorderThickness`-ready); MAUI renders uniform (max side) since its `StrokeThickness` is a single `double` — see [dotnet/maui#7612](https://github.com/dotnet/maui/issues/7612) |
| `border-color` | ✅ | preflight gray-200 default |
| border-style, `divide-*`, `outline-*`, `ring-*` | ⛔ | diagnostics |

## Effects / Transitions / Transforms

| Utility | Status | Notes |
|---|---|---|
| `box-shadow` `shadow-*` | ✅ | named scale + **colored `shadow-{color}` / `shadow-{color}/opacity`** (folds into the shadow brush) |
| `opacity-*` | ✅ | |
| `transition` / `duration` / `ease` / `delay` | ✅ | event-driven tweens |
| `animate-spin/pulse/bounce/none` | ✅ | keyframe loops (ping ✗) |
| `scale` / `scale-x` / `scale-y` | ✅ | negative flips |
| `rotate` / `translate-x/y` | ✅ | |
| `transform-origin` `origin-*` | ✅ | → AnchorX/AnchorY |
| `skew-*` | ⛔ | no native skew |
| CSS `filter`/`backdrop-*` | ⛔ | diagnostics |

## Interactivity & Variants

| Utility | Status | Notes |
|---|---|---|
| `pointer-events-none/auto` | ✅ | → `InputTransparent` |
| **variants**: `dark:` | ✅ | two plans, pointer swap |
| **variants**: `hover:` `focus:` `pressed:`/`active:` `disabled:` | ✅ | VSM / event wiring |
| **variants**: `sm:`–`2xl:` | ✅ | responsive overlays |
| **variants**: platform (`ios:` `android:` `windows:`…) + idiom (`phone:` `desktop:`…) | ✅ | **native bonus** — compiled out at zero cost |
| pseudo variants `first:` `odd:` `group-*` `peer-*` `before:` `checked:` … | ✗ | not modeled |
| cursor, accent/caret-color, user-select, resize, scroll-*, touch, appearance | ⛔ | web-only |

---

## Remaining native-mappable gaps (candidate next work)

Ranked by value:

1. **Grid `col-end-*` / `row-end-*`** — complete the grid placement set (needs start+end folding).
2. **`justify-items` / `justify-self` / `place-*`** — Grid child alignment.
3. **`aspect-*`** — could drive Width/HeightRequest from a ratio.
4. **`animate-ping`** and arbitrary `animate-[…]`.

Done in later passes: per-side border width (carried as Edges; WPF-ready), colored shadows.

Everything else in Tailwind's remaining catalog is web-only and correctly surfaces a
diagnostic rather than a silent no-op.
