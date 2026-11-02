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
loads it. The full name table and the import of PC resource packs are in the
[old guide](../historico/mods-js/14-pacotes-de-textura.md); since format 3 the
folder is `Images/` at the root, not `content/Images/`. A texture pack may
carry a `Fonts/` folder too. Test: `tools/tests/cstexpack` (the Copper
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
sample is [`samples/FonteComicNeue`](../../samples/FonteComicNeue); the atlas
tool, the JSON and how the letters stay sharp are in the
[old guide](../historico/mods-js/21-pacotes-de-fonte.md).

Packs are applied by the native core, with no C# involved: the topmost package
on the Packages tab wins per file. A server that does not allow texture packs
refuses players that use one (`Allow textures` in the host settings).

Packages with `blVersion` below 3 (texture and font packs included) do not
load: move `content/Images` and `content/Fonts` to the root and set
`"blVersion": 3`.
