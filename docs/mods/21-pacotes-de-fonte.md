# Font packs

A font pack replaces the game's fonts: item tooltips, menus, item counts in
the inventory, damage numbers (normal and critical) and the death message. Like
a texture pack, it has no `main.js` and no JavaScript code.

```text
MyFont/
  manifest.json
  icon.png
  content/
    Fonts/
      Mouse_Text.json      Mouse_Text.0.png
      Item_Stack.json      Item_Stack.0.png
      Death_Text.json      Death_Text.0.png
      Combat_Text.json     Combat_Text.0.png
      Combat_Crit.json     Combat_Crit.0.png
```

```json
{
  "uid": "19e3900c-83a2-4e9f-9724-1a38f82dcae9",
  "id": "fontecomicneue",
  "name": "Fonte Comic Neue",
  "version": "1.0.0",
  "author": "Your name",
  "type": "font",
  "category": "Fontes",
  "blVersion": 2
}
```

Use your own UUID. `type: "font"` puts the package on the launcher's Fonts
list. A texture pack (`type: "texture"`) can also carry a `content/Fonts`
folder: the fonts are applied the same way.

The finished example is [`samples/FonteComicNeue`](../../samples/FonteComicNeue):
Comic Neue Bold (SIL Open Font License) for all five fonts.

| File in `content/Fonts/` | Game font | Where it shows |
|---|---|---|
| `Mouse_Text.json` | `FontAssets.MouseText` | Tooltips, menus, almost the whole UI |
| `Item_Stack.json` | `FontAssets.ItemStack` | Item counts in the inventory |
| `Death_Text.json` | `FontAssets.DeathText` | Death message and big titles |
| `Combat_Text.json` | `FontAssets.CombatText[0]` | Damage numbers |
| `Combat_Crit.json` | `FontAssets.CombatText[1]` | Critical damage numbers |

The name is matched without case, spaces or underscores, so the TL Pro names
(`MouseText.json`, `CombatCrit.json`...) work too. A missing file keeps that
font as it is.

## Making the files

Do not draw the atlas by hand. `tools/mods/fonts.py` builds it from a font
file:

```powershell
python tools/mods/fonts.py ttf "MyFont.ttf" --out "MyFont/content/Fonts"
```

It needs Python with Pillow, NumPy and fontTools
(`python -m pip install Pillow numpy fonttools`). The command fits the font to
the measurements of the mobile fonts (capital height, baseline and line
spacing of each one), so the UI does not move, and draws every glyph with
**3 times more pixels** than the game uses (2 times for `Death_Text`).

| Option | What it does |
|---|---|
| `--preset latin` | Characters: `latin` (default, full Latin with accents), `europe` (+ Greek, Cyrillic, arrows) or `all` (everything in the file). |
| `--ranges 0020-007E,0400-04FF` | Exact hexadecimal ranges, instead of the preset. |
| `--scale 4` | Atlas pixels per game pixel, for every font. More is sharper and uses more memory. |
| `--size-factor 1.1` | Makes the letters bigger (or smaller) than the fitted size. |
| `--stroke 1` | Thickens thin strokes by N atlas pixels. |
| `--only Mouse_Text Item_Stack` | Generates only these fonts. |

Characters the font does not have keep the game's original glyph, so nothing
is drawn blank.

## PC resource packs

The launcher imports a PC resource pack as it is: a zip with `pack.json` and
`Content/Fonts/*.xnb` (ReLogic `DynamicSpriteFont`, the files the Workshop
font packs ship). The importer converts each `.xnb` to `<Font>.json` plus its
pages, in the PC resolution (scale 1), and drops the `.xnb`. The glyphs are
repacked into pages of up to 2048 pixels: a pack with Chinese, Japanese and
Korean has about a hundred small pages in the XNB, and a texture per page made
the game stall for seconds when applying it. A pack with only
fonts becomes a font pack (`type: "font"`); one that also has
`Content/Images` stays a texture pack and keeps the fonts. Texture formats:
`Color`, `Dxt1`, `Dxt3` and `Dxt5`. Compressed XNB files (LZX/LZ4) are not
read: a fonts-only pack made of them is refused with that reason, and in a
texture pack they are skipped (the log says `resource pack font: ...`).

The letters are the pack's own. Many Workshop packs only redraw the basic
Latin letters and keep small accented ones from another font (Custom Fonts
(Pescadero) does this in `Death_Text`: "Créditos" shows a small "é"); the
same happens on PC.

To convert by hand (same result), use `xnb`:

```powershell
python tools/mods/fonts.py xnb "pack/Content/Fonts" --out "MyFont/content/Fonts"
```

Both keep the PC resolution (scale 1): it looks the same as on PC, not
sharper. When the original `.ttf` is available, `ttf` gives a much better
result. Compressed XNB files have to be decompressed first.

A TL Pro font mod already has the files this loader reads: copy its
`Assets/*.json` and `Assets/*.png` into `content/Fonts/`.

## Fonts for a mod's own UI

A mod can carry its own font and draw with it, without touching the game's
fonts: the `ModContent.Request<DynamicSpriteFont>` of tModLoader. Put the
font in the mod (any folder; `Assets/Fonts` is the convention) and request it
without the extension:

```javascript
const DrawBorderStringFourWay = Terraria.Utils['void DrawBorderStringFourWay(SpriteBatch sb, SpriteFont font, string text, float x, float y, Color textColor, Color borderColor, Vector2 origin, float scale)'];

const font = ModContent.Request('Assets/Fonts/ExampleFont').Value;
DrawBorderStringFourWay(spriteBatch, font, 'CPM', x, y, Color.Gold, Color.Black, Vector2.new(0, 0), 0.9);
```

`Value` is a game `SpriteFont`, so it works wherever the game takes one:
`SpriteBatch.DrawString`, `Utils.DrawBorderString*`, `ChatManager`,
`MeasureString`. Another mod's font is `'othermod/Assets/Fonts/Name'`, and
`ModContent.HasAsset` finds fonts too.

The file is the same `blfont/1` JSON with its pages. Generate one with `ttf`,
copying the measurements of one game font and giving it another name:

```powershell
python tools/mods/fonts.py ttf "MyFont.ttf" --only Mouse_Text --name MyFont --out "MyMod/content/Assets/Fonts"
```

How it behaves:

- The font starts as a copy of the game's `MouseText`: characters the file
  does not have are drawn with the game's letters (never a blank or a crash),
  and the default character stays the game's one.
- `lineSpacing` and `spacing` of the file are used (unlike font packs).
- Load it on the game thread with the content ready: in `PostSetupContent`, in
  a UI constructor built there, or in a hook. Earlier, `Request` throws.
- The same file returns the same font; it lives until the game closes. A file
  that fails (bad JSON, missing page) throws, and the log says why
  (`Mod font: ...`).
- Font packs do not change it: a pack that replaces `MouseText` changes the
  game's font, not the copies mods made.

The Example Mod draws the "CPM" label of its coins panel (`/coins`) with its
own font, New Rocker:
[`UIMoneyDisplay.js`](../../samples/ExampleMod/content/Common/UI/ExampleCoinsUI/UIMoneyDisplay.js)
and [`Assets/Fonts`](../../samples/ExampleMod/content/Assets/Fonts).

## Why the letters stay sharp

The phone draws the text much bigger than the font's pixels (tooltips use a
0.75 scale, then the whole screen is enlarged). A font atlas in the game's own
size, like the PC one, gets stretched, and the filtering smears the edges.

The game's sprite batcher takes the texture coordinates of a glyph from its
rectangle times `TexelWidth`/`TexelHeight` (1 / the logical width), and the
size on screen from the rectangle itself. A page with `"scale": 3` is 3 times
bigger than its logical size; Bunny Loader sets `TexelWidth = 3 / width`, so
each glyph is drawn at the game's size with 3 times the detail, and the page
uses trilinear filtering with mipmaps, so it also shrinks cleanly.

## The JSON

```json
{
  "format": "blfont/1",
  "scale": 3,
  "lineSpacing": 24,
  "spacing": 0,
  "pages": ["Mouse_Text.0.png"],
  "fields": "code,page,bx,by,bw,bh,cx,cy,cw,ch,leftBearing,width,rightBearing",
  "glyphs": [[65, 0, 120, 3, 12, 14, 0, 2, 12, 24, 0, 12, -0.667]]
}
```

| Field | Meaning |
|---|---|
| `format` | `blfont/1`. `tlfont/1` (TL Pro) is read the same way, with scale 1. |
| `scale` | Page pixels per logical pixel, 1 to 8. Default 1. |
| `pages` | PNG files next to the JSON, in order. White glyphs with transparency (not premultiplied). |
| `glyphs` | One array per character, in **logical** pixels (page pixels divided by `scale`). |

Each glyph: `code` (UTF-16 code unit), `page` (index in `pages`),
`bx,by,bw,bh` (rectangle in the page), `cx,cy,cw,ch` (offset from the pen and
from the top of the line, as in XNA's `SpriteFont.Glyph.Cropping`) and
`leftBearing, width, rightBearing` (the advance is their sum). Glyph positions
times `scale` must be whole page pixels.

`lineSpacing` and `spacing` are informative: the game's values are kept, so
the layout of every screen stays the same.

## How it is applied

When the game opens, after the content is ready, Bunny Loader edits the
**same** `SpriteFont` objects the game uses: characters the game already has
are replaced in place and new ones are appended. The mobile UI keeps
references to the fonts (`GetFont()` of the templates), so swapping only
`FontAssets.X.Value` would leave part of the screens with the old font.

The topmost package on the Packages tab wins per font. A file that fails to
load (invalid JSON, missing page) lets the next package try, and the log says
why: `Font pack <uid>: ...`. To turn a font pack off, disable it and open the
game again.

When joining a server, the player's font packs stay on: they only change the
letters on their own screen.

Not done: kerning pairs (the mobile `SpriteFont` has none) and compressed
XNB files in the importer.

## Porting fonts to the catalog

`tools/mods/port_fonts.py` builds catalog-ready font packs, the same way
`port_textures.py` does for texture packs. The list of ports lives in `PORTS`
at the top of the script: name, source (`.ttf` or the `.xnb` folder of a PC
pack), extra `fonts.py` options, the font's designer, link and license, the
summary and the page colors.

```powershell
python tools/mods/port_fonts.py                 # every port
python tools/mods/port_fonts.py "Ale and Wenches"
```

Each port is written to the clone of the mods repository:

```text
build/mods-repo/ports/fonts/<Name>/
  README.md                where the font came from and its license
  bl/                      the package (type "font", category "Fontes")
    manifest.json  icon.png  banner.png  description.md  license.md  changelog.md
    authors/potato.png
    content/Fonts/         the five fonts
build/mods-repo/ports/fonts/in bl/<Name>/<id>-<version>.bl
```

The source files go in `build/font-sources/<Name>/` (the `.ttf`, the author's
license text and an optional `icon.png`). That folder is outside every Git
repository on purpose: several free fonts allow using the font but not
redistributing the font file, and a package only carries the rendered glyphs.
Without an `icon.png`, the icon is drawn with the font itself in the frame of
the other font icons. The banner is always drawn with the generated fonts.

Two `fonts.py ttf` options exist for fonts that are not made for UI text:

| Option | What it does |
|---|---|
| `--compose-accents [FONT]` | Builds the accented letters the font lacks (á, ç, õ...): the font's own base letter plus the accent from FONT (default: Comic Neue Bold), sized to the same capital height. The dot of `i`/`j` is removed under the accent. |
| `--exclude 00A9,00DF` | Leaves these characters with the game's font, for fonts that reuse codes for ornaments (Ringbearer draws a decorative S on `ß` and `©`). |

`pack.py` and `publish.py` accept `type: "font"` packages: they need at least
one `.json` in `content/Fonts/` and no `main.js`.
