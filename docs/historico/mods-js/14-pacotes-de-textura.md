# Texture packs

A texture pack replaces vanilla sprites using PNGs only. It needs no
`main.js`, no `Mod` class and no JavaScript code.

```text
MyPack/
  manifest.json
  icon.png
  content/
    Images/
      Item_1.png
      NPC_4.png
      Projectile_1.png
      Tiles_0.png
      Wall_1.png
      Buff_1.png
      Armor_Head_1.png
```

```json
{
  "uid": "19a01ecc-063f-3e40-a704-ace60cf3b483",
  "id": "mypack",
  "name": "My Pack",
  "version": "1.0.0",
  "author": "Your name",
  "type": "texture",
  "category": "Texturas",
  "blVersion": 2
}
```

The example's UUID belongs to the Un-mechanical Bosses test pack: use your
own UUID for a local package and the identity issued by the site when
publishing. Description, icon, authors and links use the same fields as other
packages.

| File relative to `content/Images/` | Game table |
|---|---|
| `Item_N.png` | `TextureAssets.Item[N]` |
| `NPC_N.png` | `TextureAssets.Npc[N]` |
| `Projectile_N.png` | `TextureAssets.Projectile[N]` |
| `Tiles_N.png` | `TextureAssets.Tile[N]` |
| `Wall_N.png` | `TextureAssets.Wall[N]` |
| `Buff_N.png` | `TextureAssets.Buff[N]` |
| `Gore_N.png` | `TextureAssets.Gore[N]` |
| `Armor_Head_N.png` | `TextureAssets.ArmorHead[N]` |
| `Armor_Body_N.png` | `TextureAssets.ArmorBody[N]` |
| `Armor_Arm_N.png` | `TextureAssets.ArmorArm[N]` |
| `Armor_Legs_N.png` | `TextureAssets.ArmorLeg[N]` |
| `Female_Body_N.png` | `TextureAssets.FemaleBody[N]` |
| `Armor/Armor_N.png` | `TextureAssets.ArmorBodyComposite[N]` |

`N` is the vanilla number. Examples: `Item_1` is the Copper Pickaxe, `NPC_4`
is the Eye of Cthulhu, `Tiles_0` is dirt. Use the original resource pack's
names and capitalization. Files in `Misc/` and `UI/Bestiary/` do not replace
NPCs, even when they are named `NPC_N.png`.

The loader also looks up the **full path** of the other texture assets,
including individual fields, other tables and assets held by the repository.
That makes it possible to replace `Extra_N`, `Background_N`, `Wings_N`,
accessories, NPC heads, trees, inventory, boss parts and images in
subfolders:

```text
content/Images/Inventory_Back.png
content/Images/Arm_Bone_2.png
content/Images/Bone_Eyes.png
content/Images/Background_325.png
content/Images/UI/Minimap/Default/MinimapFrame.png
content/Images/UI/PlayerResourceSets/FancyClassic/Heart_Fill.png
content/Images/TownNPCs/Wizard_Default.png
```

The path must match the `Name` of the asset the game requests. The lookup by
name is case-insensitive and normalizes slashes; it does not reduce
subfolders to the file name. PC-only images, alternatives in `Misc/` and UI
elements mobile draws differently may have no effect. Applying a PC minimap
frame does not adapt the mobile UI's measurements or buttons; the PNG may need
art adjustments to line up.

On the Packages tab, the topmost package wins **per file**. A PNG that fails
to load lets the same file from a package below be used; with no valid
replacement, the original asset stays. Indexes outside the table leave a
warning.

The PNGs are read when the game loads, after the tables are ready, with pixel
filtering and no edge wrapping. Overridden PNGs from packages below are not
decoded. Memory grows with the replaced sprites: name-based textures that have
not been requested yet are decoded on the first call to
`Asset<Texture2D>.ActionUnityLoad` on the game thread. That load uses the PNG
directly. The sprites of the 13 tables above and the name-based assets that
already exist are still applied during loading; on-demand loading of every
sprite of a large pack is not implemented yet.

Keep the sheet's original dimensions. The loader compares against the vanilla
sprite's logical width and height (not the Unity page size) and logs
`size mismatch` when they differ. It is a warning: the PNG is still applied.
If the original asset has no `Value` yet, `Asset.ActionUnityLoad` prepares the
logical Texture2D through the ContentManager, which knows the atlas and the
SourceAssetEntry. Reading Width/Height does not request the Unity page used
for drawing. If those dimensions are not available, the log says it cannot
check. Future name-based requests that have no Value yet get the PNG without
loading the vanilla one first; in that case the warning says the original
dimensions were not verified.

Package choices are applied when the game opens/restarts. To turn one off, go
back to the launcher, disable the package and open the game again. The
original assets are kept alive with GC references while a replacement exists.
For name-based replacements, the Asset object stays the same, preserving
references held by the UI. The previous value, source and state are restored
before freeing the Unity texture the package created.

## Importing and packing

To import a PC pack, pick its ZIP with `pack.json`, `icon.png` and
`Content/Images/`. The launcher converts name, author, description and
version into `manifest.json`, sets `type: "texture"` and normalizes `Content/`
to `content/`. It also accepts these files inside a folder in the ZIP. If
there is a `workshop.json`, the Workshop ID keeps the identity across updates;
without it, the local identity derives from the name and author. An existing
`manifest.json` takes priority and is never recreated by the converter.

To pack the Bunny Loader folder:

```powershell
python tools/mods/pack.py "build/mods-repo/ports/textures/Un-mechanical Bosses/bl"
```

The `.bl` comes out in `build/mods-packs/`. The same content produces the
same file.

To port images from a resource pack installed from the local Workshop:

```powershell
python tools/mods/port_textures.py 2439853873 2796830227
```

The command copies the PNGs to `build/mods-repo/ports/textures/<name>/bl`
(the clone of the [mods repository](https://github.com/JonataOliveiraa/Bunny-Loader-Mods)
that `publish.py` maintains), creates the manifest, preserves authorship,
metadata and icon and builds the `.bl`. It does not touch the Steam folder.
The UID derives from the Workshop ID, as in the launcher's importer; an
existing port keeps its identity. Each port has a README in
`ports/textures/<name>/`, and the finished `.bl` files sit in
`ports/textures/in bl/`.

Localization, sounds and music are not applied by this loader. The port
command copies images only; the `.xnb` fonts of a PC pack are converted with
`tools/mods/fonts.py` (see [Font packs](21-pacotes-de-fonte.md)), and a
`content/Fonts` folder in a texture pack is applied too. Matching names and
dimensions does not guarantee every PC image has a visible equivalent element
in the mobile UI.
