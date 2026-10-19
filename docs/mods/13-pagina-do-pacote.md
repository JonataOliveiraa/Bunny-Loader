# 13. The package page: description, changelog, license and authors

A mod's page in the launcher (what opens when someone taps it) is built **by
the package**. The text comes from Markdown files, the authors' photos from a
folder, and the colors from the manifest itself. The launcher only draws, in
its own style: an outlined pixel font, beveled panels and unblurred images.

```
MyMod/
  authors/            the authors' photos (optional)
    potato.png
  content/
    main.js           the mod itself (previous guides)
  manifest.json       who the mod is
  description.md      Description tab
  changelog.md        Changelog tab (optional)
  license.md          License tab (optional)
  icon.png            square icon, in the list
  icon.gif            the animated icon, in the launcher (optional)
  banner.png          the page cover, ~3.4:1 (e.g. 384x112); banner.gif animates
  thumbnails/         extra images, at the bottom of Description (optional)
  docs/               anything else you want to show (optional)
    background.png    the page background, if the theme asks for it (optional)
```

**Image formats.** Across the whole page (cover, showcase, author photos,
background and Markdown images) PNG, GIF, WebP, JPG and BMP work. **GIF
animates** on the cover, the background, the showcase and in Markdown (author
photos show only the first frame). Animated WebP shows up still. The cover can
be `banner.gif`, `banner.png`, `banner.webp` or `banner.jpg`; with more than
one, that order applies.

The page looks like this, from top to bottom:

1. the **cover** (`banner.png`), with back and favorite;
2. the **header**: icon, name, the authors' photos and names, the category,
   the info sheet (version, date, size, license), the buttons (download,
   enable, remove, export) and the manifest's **links**;
3. the **tabs**: Description, Changelog, License, Credits and the ones you
   create. Description always shows; the others depend on the package's files
   and fields.

### Animated icon

An `icon.gif` next to `icon.png` makes the icon animated in the launcher
(list, featured and page). The GIF takes precedence; `icon.png` remains the
mod's icon **inside the game** (the mods menu and Mod Configuration do not
animate), so ship both. No filtering, like the PNG: the pixels stay square.

- square, up to 512x512 (beyond that, the launcher uses `icon.png`);
- each frame's time comes from the GIF; a frame with no time, or 10 ms, gets
  100 ms;
- transparency and partial frames work;
- a very long GIF is cut to its first frames (about 4 million pixels in
  total: 60 frames of 256x256, or 975 of 64x64).

Save the documents in **UTF-8**, with lowercase names as in the example. When
exporting the `.bl`, these documents and the `authors/` folder go along with
the mod.

## `manifest.json`

The new fields are marked. The rest is in the [README](README.md#manifestjson).

```json
{
  "uid": "7c2e9d41-5b3a-4f8e-9a61-2d0c4e8b7f15",
  "id": "mymod",
  "name": "My Mod",
  "version": "1.2.0",
  "authors": [
    { "name": "Potato", "role": "Code", "avatar": "potato.png",
      "color": "#E8C170", "link": "https://github.com/JonataOliveiraa" },
    "Someone Else"
  ],
  "category": "Armas",
  "summary": "One line, for the list card.",
  "license": "MIT",
  "updated": "2026-10-01",
  "blVersion": 2,
  "links": [
    { "title": "Source code", "url": "https://github.com/you/mymod" },
    { "title": "Discord", "url": "https://discord.gg/..." }
  ],
  "pages": [
    { "title": "Recipes", "file": "docs/receitas.md" }
  ],
  "theme": {
    "accent": "#7FE07A",
    "panel": "#24413A",
    "text": "#D8F0DD",
    "heading": "#FFFFFF",
    "button": "#2F6B4F",
    "buttonText": "#FFFFFF",
    "background": "docs/background.png",
    "backgroundMode": "tile",
    "backgroundScale": 2,
    "backgroundDim": 0.3
  }
}
```

| Field | |
|---|---|
| `authors` | **New.** Who made the mod. Each one is a plain name (`"Someone Else"`) or an object (below). A single string also works: `"authors": "Someone"`. |
| `author` | The old form, a single name. It still works; with `authors`, it is ignored. |
| `summary` | Still here: it is the list card's line and the final description fallback. |
| `license` | **New.** The license's short name (`MIT`, `CC BY-NC 4.0`, `All rights reserved`). It shows in the info sheet and at the top of the License tab. The full text goes in `license.md`. |
| `links` | **New.** Buttons in the header. Only `https://` and `http://` show up. |
| `pages` | **New.** Extra tabs, each one a `.md` from the package. A page whose file does not exist does not show up. |
| `theme` | **New.** This mod's page theme: text, panel and button colors, and the screen background (below). |
| `description` | The manifest fallback when neither description Markdown file exists. |

### Localized name, description and changelog

Keep `name` as the default and add `localizedNames` keyed by game culture:

```json
"localizedNames": {
  "zh-Hans": "示例模组"
}
```

The launcher language `zh` maps to `zh-Hans`. A missing translation uses
`name`. For translated Markdown pages, add `description_zh-Hans.md` or
`changelog_zh-Hans.md` beside the corresponding default file. Each localized
file falls back to its default Markdown file; the description then falls back
to manifest `description` and `summary`.

See `samples/ExampleMod/manifest.json`,
`samples/ExampleMod/description_zh-Hans.md` and
`samples/ExampleMod/changelog_zh-Hans.md`.

### The authors

```json
{
  "name": "Animatak_",
  "role": "TL Pro's ExMod",
  "avatar": "animatak.png",
  "color": "#841FFF",
  "link": "https://discord.com/invite/..."
}
```

| Field | |
|---|---|
| `name` | **Required.** The name that shows up. |
| `role` | What the person did: "Code", "Art", "Music", "Translation"... It shows below the name in the Credits tab. |
| `avatar` | The photo file **inside `authors/`**. |
| `color` | The name's color (`#RRGGBB`). Without a photo, it is also the color of the square with the initial. |
| `link` | Tapping the author, in the Credits tab, opens the link. |

**The photo.** With `avatar`, `authors/<avatar>` is used. Without it, the app
looks for `authors/<name>.png` and then the lowercase, hyphenated name
(`"Bunny Loader"` → `authors/bunny-loader.png`); it also accepts `.jpg`,
`.jpeg`, `.webp` and `.gif` (still). With no photo at all, the name's initial
shows up.

Use a **square** image (the excess is cropped). Small pixel art (32x32,
64x64) is enlarged without blurring; a large photo is shrunk with smoothing.
Do not go over 256x256: the photo shows at most 56 dp and the rest is dead
weight in the package.

The Credits tab lists every author, with the large photo. The header shows up
to four photos, overlapping, and "by A, B and C".

### The colors (`theme`)

| Color | Where |
|---|---|
| `accent` | The selected tab, links, the line under headings, list bullets, task boxes and the current version in the changelog. Default: the touch yellow. |
| `panel` | The background of the page's panels (header, content, tabs). Default: Terraria's panel blue. |
| `text` | Body text. |
| `heading` | Headings (`#`, `##`...) and the mod's name. |
| `button` | The buttons' background: download, update, remove, export and the links. The pressed button and the on/off box use the same color, darker. Default: the launcher's buttons (and the links in the selection blue). |
| `buttonText` | The buttons' text, links included. Default: white on buttons and `accent` on links. |

All of them are `#RRGGBB`, `#AARRGGBB` or `#RGB`. A missing (or unreadable)
color stays the launcher's, so you can change just `accent`. The theme applies
**only inside your mod's page**; the list and the other screens keep the app's
palette.

> [!TIP]
> Pick a dark `panel`: the text has a black and white outline on top, and on
> a light panel it looks heavy.

### The background (`background`)

With the page open, the launcher's scenery (the forest, the snow...) gives way
to your mod's background:

| Field | |
|---|---|
| `background` | A color (`"#1B2430"`) or an image from the package, by path from the root (`"docs/background.png"`). GIF animates. |
| `backgroundMode` | `"cover"` (the default): the image fills the screen, cropping the excess. `"tile"`: repeats the image side by side, like wallpaper — good for a game block texture. |
| `backgroundScale` | Only for `tile`: how many screen points each image pixel takes (default `2`; a 16x16 block becomes a 32 square). |
| `backgroundDim` | From `0` to `1`: a black layer over the background, so the panels stand out. `0.3` already helps on a light image. |

The background stays still behind the page (it does not scroll along) and
covers the whole screen, behind the bottom bar too. Without `background`, the
launcher's scenery remains.

- For `cover`, use a portrait image, close to a phone's aspect ratio (for
  example 270x480 in pixel art, or 1080x1920 for a photo). Sides wider than
  the screen are cropped.
- For `tile`, use a small image that tiles at the edges (16x16, 32x32,
  48x48).
- An animated GIF can be up to 512x512 anywhere on the page; larger than that
  it shows still, on the first frame. A very long GIF is cut to its first
  frames (about 2 million pixels in total: 30 frames of 256x256).

## `description.md`

The Description tab. It is plain Markdown (the [full list](#the-markdown) is
further down). The images in `thumbnails/` show up at its end.

If the package has no `description.md`, the tab shows the manifest's
`description`, and if that does not exist either, the `summary`.

A template to copy, with a bit of each feature:
[`samples/modelo-description.md`](../../samples/modelo-description.md).

## `changelog.md`

The Changelog tab. Each `##` heading that **starts with a version number**
becomes a card that opens and closes:

````markdown
# Changelog

## Unreleased

- What is already done but has not shipped.

## 1.2.0 - 2026-10-01

- New sword.
- **Fix:** the bow did not shoot in multiplayer.

## [1.1.0] - 2026-09-20

- First public version.
````

- The number accepts `1.2.0`, `v1.2.0` and `[1.2.0]` (the *Keep a Changelog*
  format). The date, optional, is `YYYY-MM-DD`, after a `-` or in
  parentheses, and is shown spelled out ("Oct 1, 2026").
- Text after the date becomes the version's title:
  `## 1.2.0 - 2026-10-01 - The ice update`.
- The first card starts open. The **installed** version's (the manifest's
  `version`) also starts open and gets the *Current* tag.
- Whatever comes before the first version (like "Unreleased" above) shows up
  on top, as plain Markdown. The top `# Changelog` disappears: it is the tab's
  name.
- A changelog with no version headings comes out as plain Markdown.

## `license.md`

The License tab: the manifest's `license` in a tag, and the `license.md` text
below. With only one of the two, the tab shows up the same way.

If your mod is a port or uses someone else's art, this is where the copyright
notice the original's license requires goes (`ExampleMod` is a port of
tModLoader's and carries its MIT notice).

## Extra tabs (`pages`)

```json
"pages": [
  { "title": "Recipes", "file": "docs/receitas.md" },
  { "title": "FAQ", "file": "docs/faq.md" }
]
```

Each one becomes a tab after Credits, in list order. The path is relative to
the package root. The tab bar scrolls sideways, so as many as you want fit,
but short names look better.

## The Markdown

Everything below works in any `.md` on the page.

### Text

| You write | You get |
|---|---|
| `**bold**` or `__bold__` | **bold**, in the heading color |
| `*italic*` or `_italic_` | *italic* |
| `~~strikethrough~~` | ~~strikethrough~~, dimmed |
| `==highlighted==` | a background in the accent color |
| `` `code` `` | code font, on a dark background |
| `[text](https://...)` | a link (tapping opens the browser) |
| `https://...` or `<https://...>` | a bare link |
| `\*` | the `*` character itself (works for `` ` _ [ ] # | `` etc.) |
| two spaces or `\` at the end of the line | line break |

Links only open `https://`, `http://` and `mailto:`; the others show up
colored, but do nothing.

### From Terraria

| You write | You get |
|---|---|
| `[c/FF8800:text]` | the text in that color (the game chat's color tag) |
| `[i:757]` | the icon of item 757 (the Terra Blade), at the font's size |
| `[b:2]` | the icon of buff 2 (Regeneration) |

The IDs are the game's (`ItemID`, `BuffID`). Mod items have no icon here: for
them, use an image from your package (below).

### Images

```markdown
![Caption](content/Content/Items/Sword.png)

Grab the ![](content/Content/Items/Coin.png) Coin and trade it at the shop.
```

- The path is **relative to the package root**: `content/...`,
  `thumbnails/...`, `docs/...`. A path with `..` or that leaves the package
  does not load.
- **Alone on its line**, the image becomes a block: enlarged by a whole number
  (2x, 3x, up to 4x) so the pixels stay square, with a frame, and with the
  text in brackets as its caption. One wider than the screen shrinks to fit.
- **In the middle of text**, it becomes an icon at the font's size. It is the
  way to show one of your mod's items in a sentence.
- PNG, GIF (animated), WebP, JPG and BMP. A missing image shows the
  alternative text.

#### Size and alignment

A `{...}` **right after** the image sets its size and where it sits:

```markdown
![Boss](docs/boss.gif){width=200 align=center}

![](docs/map.png){width=50% align=right}

![](content/Content/Items/Sword.png){float=left width=96}
This paragraph sits **next to** the sword, not below it.

Grab the ![](content/Content/Items/Coin.png){h=24} Coin, larger than the text, and [i:757]{32} too.
```

| Inside the `{}` | |
|---|---|
| `width=128` (or `w=128`) | The width, in screen points (the launcher's "pixel": the page is about 330 wide on a phone held upright). `px` and `dp` at the end are accepted and mean the same. |
| `width=50%` | The width as a percentage of the text. |
| `height=64` (or `h=64`) | The height. Only the width **or** only the height keeps the image's aspect ratio; both together stretch it. |
| `128x64`, `128x`, `x64` | Width and height at once. A number on its own (`{128}` or `{50%}`) is the width. |
| `align=left` / `center` / `right` | Where an image alone on its line sits. Without it, on the left (or centered, inside `::: center`). The word alone also works: `{right}`. |
| `float=left` / `right` | Puts the image **beside the next block** (a paragraph, a list, a table). The width is `width`'s (up to 60% of the text); without it, 40%. |

- The Portuguese names also work: `largura`, `altura`, `alinhar`, `flutuar`,
  `esquerda`, `centro`, `direita`.
- The image never goes beyond the text width: larger than that, it shrinks.
- Enlarged (bigger than the file), the image has no filtering, with square
  pixels; shrunk, it is smoothed. For crisp pixel art, use multiples of the
  file size (a 32 art with `width=64` or `width=96`).
- In the middle of text, only `width`/`height` in points work (`%` is
  ignored); the line grows to fit the image.
- `float` takes **one** block to the side. To put several (two paragraphs and
  a list), group them in a `::: group` right after the image:

```markdown
![](docs/guide.png){float=right width=40%}
::: group
First paragraph beside it.

- and a list
- also beside it
:::
```

### Blocks

````markdown
# Big heading          (with an accent line below)
## Medium heading
### Small heading

- bulleted list
  - sublist (2-space indent)
1. numbered list
- [x] done task
- [ ] pending task

> Quote, with a bar in the accent color.

---                    (separator line)

```js
// code block, scrolls sideways
```

| Column | Another |
|:---|---:|
| on the left | on the right |
````

In tables, `:---` aligns left, `---:` right and `:---:` center.

### Callouts

```markdown
> [!NOTE]
> Neutral information.

> [!TIP]
> A tip.

> [!WARNING] Watch out for the boss
> Text after the [!...] becomes the title.
```

| Type | Color |
|---|---|
| `[!NOTE]` (or `[!NOTA]`) | blue |
| `[!TIP]` (`[!DICA]`) | green |
| `[!IMPORTANT]` (`[!IMPORTANTE]`) | purple |
| `[!WARNING]` (`[!AVISO]`) | yellow |
| `[!CAUTION]` (`[!CUIDADO]`) | red |

### Containers

A line with `:::` and the type opens a special block; another with just `:::`
closes it:

```markdown
::: center
Everything here is **centered**, images included.
:::

::: spoiler How to beat the boss
Hidden until you tap the title.
:::

::: panel Requirements
A sunken panel, with an optional title.
:::

::: group
Only groups the blocks, without drawing anything: useful for putting several
next to an image with `float`.
:::
```

You can nest one inside another, and any block (list, table, image) inside
them.

### What is not supported

HTML (`<b>`, `<img>`, `<details>`) is not interpreted: it shows up as text.
`<!-- ... -->` comments disappear. For what HTML would do, use the containers
above.

## Real examples

Every mod in [`samples/`](../../samples) has a description, changelog,
license and authors. [`ExampleMod`](../../samples/ExampleMod) uses almost
everything in this guide: four authors (three with photos), links, an extra
tab (`docs/receitas.md`) with tables and a spoiler, callouts, colors and item
icons in the middle of the text.
