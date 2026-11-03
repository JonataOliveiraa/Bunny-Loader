# Fonts and texture packs

Three things that change how the game looks without new content: a font of
the mod's own for its interface, and texture and font packs, which are
packages without code.

## A mod's own font

tModLoader requests a `DynamicSpriteFont`; on mobile the game's font class is
`SpriteFont`, and that is what a C# mod requests:

```csharp
using Microsoft.Xna.Framework.Graphics;

SpriteFont font = ModContent.Request<SpriteFont>("MyMod/Assets/Fonts/MyFont").Value;
Utils.DrawBorderStringFourWay(spriteBatch, font, "CPM", x, y, Color.Gold, Color.Black, Vector2.Zero, 0.9f);
```

- The file is a `blfont/1` JSON with its pages (`MyFont.json` and
  `MyFont.0.png`), anywhere in the mod; `Assets/Fonts` is the convention. Build
  it from a `.ttf` with the measurements of a game font:

  ```powershell
  python tools/mods/fonts.py ttf "MyFont.ttf" --only Mouse_Text --name MyFont --out "MyMod/Assets/Fonts"
  ```

- `Value` is a game `SpriteFont`: it works with `SpriteBatch.DrawString`,
  `Utils.DrawBorderString*`, `MeasureString` and the UI elements.
- It starts as a copy of the game's `MouseText`, so characters the file lacks
  use the game's letters, and it keeps the file's `lineSpacing` and `spacing`.
- It loads on the game thread once the content is ready: `PostSetupContent`, a
  UI built there, or a hook. Earlier, `Value` throws.
- The same file gives the same asset (`Mod.Assets.Request<SpriteFont>` too);
  `ModContent.HasAsset` finds fonts; a missing file throws
  `FileNotFoundException`.
- Font packs change the game's fonts, not the copies mods made.
- The game's fonts by name: `ModContent.Request<SpriteFont>("Terraria/Fonts/Mouse_Text")`
  (also `Item_Stack` and `Death_Text`) is the same asset as
  `FontAssets.MouseText`.

The Example Mod draws the "CPM" label of its coins panel (`/coins`) with New
Rocker: [`ExampleCoinsUI.cs`](../../samples/ExampleModCSharp/Common/UI/ExampleCoinsUI/ExampleCoinsUI.cs)
and [`Assets/Fonts`](../../samples/ExampleModCSharp/Assets/Fonts). Test:
`tools/tests/csfont`.

## Texture packs

PNGs that replace the game's sprites. The package has no `.cs` and no `.dll`:

```text
MyPack/
  manifest.json      "type": "texture", "blVersion": 3
  icon.png
  Images/
    Item_1.png
    NPC_4.png
    Tiles_0.png
```

`Images/` mirrors the game's `Images` folder: `Item_N.png` replaces
`TextureAssets.Item[N]`, `NPC_N`, `Projectile_N`, `Tiles_N`, `Wall_N`,
`Buff_N`, `Gore_N` and the armor sheets go to their tables, and any other path
(`UI/...`, `Backgrounds/...`) replaces the asset with that name when the game
loads it. A texture pack may carry a `Fonts/` folder too.

| File in `Images/` | Game table |
|---|---|
| `Item_N.png` | `TextureAssets.Item[N]` |
| `NPC_N.png` | `TextureAssets.Npc[N]` |
| `Projectile_N.png` | `TextureAssets.Projectile[N]` |
| `Tiles_N.png` | `TextureAssets.Tile[N]` |
| `Wall_N.png` | `TextureAssets.Wall[N]` |
| `Buff_N.png` | `TextureAssets.Buff[N]` |
| `Gore_N.png` | `TextureAssets.Gore[N]` |
| `Armor_Head_N.png`, `Armor_Body_N.png`, `Armor_Arm_N.png`, `Armor_Legs_N.png` | `TextureAssets.ArmorHead`/`ArmorBody`/`ArmorArm`/`ArmorLeg[N]` |
| `Female_Body_N.png` | `TextureAssets.FemaleBody[N]` |
| `Armor/Armor_N.png` | `TextureAssets.ArmorBodyComposite[N]` |

`N` is the vanilla number (`Item_1` is the Copper Pickaxe, `NPC_4` the Eye of
Cthulhu, `Tiles_0` dirt). Any other path must match the `Name` of the asset
the game requests (`Images/Inventory_Back.png`,
`Images/UI/Minimap/Default/MinimapFrame.png`, `Images/TownNPCs/Wizard_Default.png`);
the match ignores case and normalizes slashes. PC-only images and UI the phone
draws differently may have no visible effect.

To import a PC resource pack, pick its ZIP (`pack.json`, `icon.png` and
`Content/Images/`) in *Packages → Import package*: the launcher writes the
`manifest.json` with `type: "texture"`, and a `workshop.json` keeps the
identity across updates. `tools/mods/port_textures.py <workshop ids>` does the
same from the local Workshop folder, for the catalog. Test: `tools/tests/cstexpack` (the Copper
Shortsword icon turns magenta).

## Font packs

```text
MyFont/
  manifest.json      "type": "font", "blVersion": 3
  Fonts/
    Mouse_Text.json  Mouse_Text.0.png
    Item_Stack.json  Item_Stack.0.png
    Death_Text.json  Death_Text.0.png
    Combat_Text.json Combat_Text.0.png
    Combat_Crit.json Combat_Crit.0.png
```

Each file replaces that game font (a missing one keeps the original). The
sample is [`samples/FonteComicNeue`](../../samples/FonteComicNeue).

| File in `Fonts/` | Game font | Where it shows |
|---|---|---|
| `Mouse_Text.json` | `FontAssets.MouseText` | Tooltips, menus, almost the whole UI |
| `Item_Stack.json` | `FontAssets.ItemStack` | Item counts in the inventory |
| `Death_Text.json` | `FontAssets.DeathText` | Death message and big titles |
| `Combat_Text.json` | `FontAssets.CombatText[0]` | Damage numbers |
| `Combat_Crit.json` | `FontAssets.CombatText[1]` | Critical damage numbers |

The name is matched without case, spaces or underscores.

**Making the files.** `tools/mods/fonts.py` builds the atlas from a font file
(it needs Python with Pillow, NumPy and fontTools):

```powershell
python tools/mods/fonts.py ttf "MyFont.ttf" --out "MyFont/Fonts"
```

It fits the font to the measurements of the mobile fonts (capital height,
baseline, line spacing), so the UI does not move, and draws every glyph with
3 times the pixels the game uses (`--scale` changes it). `--preset`
(`latin`, `europe`, `all`) or `--ranges` choose the characters,
`--size-factor` and `--stroke` adjust the letters, `--only` picks fonts.
Characters the font lacks keep the game's glyph.

**PC resource packs.** The launcher imports a zip with `pack.json` and
`Content/Fonts/*.xnb`: each `.xnb` becomes `<Font>.json` plus its pages, at
the PC resolution. `fonts.py xnb "<pack>/Content/Fonts" --out "MyFont/Fonts"`
does the same by hand. Compressed XNB files (LZX/LZ4) are not read.

**Why the letters stay sharp.** A page with `"scale": 3` is three times its
logical size; Bunny Loader sets the font's `TexelWidth`/`TexelHeight` to
match, so each glyph is drawn at the game's size with three times the detail,
with trilinear filtering and mipmaps.

**The JSON** (`blfont/1`):

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

Each glyph has its UTF-16 `code`, its `page`, its rectangle in the page
(`bx,by,bw,bh`, in logical pixels), its offset from the pen and the top of the
line (`cx,cy,cw,ch`, as XNA's `SpriteFont.Glyph.Cropping`) and its advance
(`leftBearing + width + rightBearing`). `lineSpacing` and `spacing` are
informative: the game's values are kept. The game's `SpriteFont` objects are
edited in place (characters replaced, new ones appended), because the mobile
UI keeps references to them.

Packs are applied by the native core, with no C# involved: the topmost package
on the Packages tab wins per file. A server that does not allow texture packs
refuses players that use one (`Allow textures` in the host settings).

How a PNG gets in, and what it costs:

- The pack's PNG replaces the `Value` of the game's own asset (the same
  `Asset<Texture2D>` object the tables and the UI hold), never the table
  entry. The PNG goes in at the moment the game loads that texture, during
  the loading screen, and again if the game unloads and reloads it; the few
  the game loads before its texture tables exist are replaced right after,
  and with the fast intro on, the splash waits for them. Enabling or
  reordering packs in the menu applies them on the next frames.
- A pack texture is uploaded once to the GPU and keeps no copy in RAM and no
  mipmaps. A large pack (the Calamity Texture Pack: 3211 PNGs, ~225 MB of
  pixels) adds ~25 MB to the game's memory and a few seconds to the loading screen
  on the MuMu emulator, with no pause on the menu or when opening the world;
  it used to add ~290 MB and ~12 s to opening the world.
- Each replaced sprite is its own texture, outside the game's atlas pages, so
  many different replaced sprites on screen mean more draw calls than the
  vanilla atlas. Keep the sheet sizes of the originals: a different size logs
  `size mismatch` and sprite frames may break.
- Test: `tools/tests/cstexperf` (frame time and memory every 2 s through
  desert, snow and jungle, the low resolution texture setting and low life),
  `tools/tests/cstexhearts` (the Example Mod life and mana overlays over a
  pack).

Packages with `blVersion` below 3 (texture and font packs included) do not
load: move `content/Images` and `content/Fonts` to the root and set
`"blVersion": 3`.
