# Mod configs

A `ModConfig` is written as in tModLoader: a class with public fields (or
properties), described by attributes. It is loaded with the mod, before
`Load`, so its values can already be read there.

```csharp
using System.ComponentModel;
using Terraria.ModLoader.Config;

public class ExampleModConfig : ModConfig
{
    public override ConfigScope Mode => ConfigScope.ClientSide;

    [Header("Items")]
    [DefaultValue(true)]
    [ReloadRequired]
    public bool ExampleWingsToggle;

    [Range(60, 250)]
    [Increment(5)]
    [DefaultValue(100)]
    public int SpawnRate;
}
```

Read it with `ModContent.GetInstance<ExampleModConfig>()`, or declare
`public static ExampleModConfig Instance;` in the class and it is filled in.

## The screen

The players change the options in **Mod Configuration**: a button in the pause
menu and a row at the top of the General tab of the title screen's settings.
It is the same screen as the JS configs, with one tab per mod (with the mod's
`icon.png`), **Discard/Back**, **Default** and **Apply**. Nothing reaches the
config before **Apply**; then `SaveChanges` runs for each config that changed.

| Field type | Shown as |
| --- | --- |
| `bool` | On/Off |
| `int`, `float`, `byte`... | slider, from `[Range]` (default 0–100, or 0–1 for `float`) in steps of `[Increment]` |
| `enum`, `string` with `[OptionStrings]` | buttons up to 3 choices, a list up to 12, otherwise `< >`; `[Dropdown]` and `[Cycle]` choose |
| `Color` | the color with hue, saturation and lightness (`[ColorHSLSlider(false)]` keeps only the hue, `[ColorNoAlpha]` keeps alpha at 255) |

Other types (lists, dictionaries, `Vector2`, classes) are saved and loaded but
not shown. `[Header("Id")]` puts a title over a field. With more than one
config, each one gets its `DisplayName` as a title.

## Texts

The texts come from the localization files, with tModLoader's keys:

- `Mods.<Mod>.Configs.<Config>.DisplayName`
- `Mods.<Mod>.Configs.<Config>.<Field>.Label` (or `[LabelKey("$key")]`)
- `Mods.<Mod>.Configs.<Config>.Headers.<Id>` (or `[Header("$key")]`)
- `Mods.<Mod>.Configs.<Enum>.<Value>.Label` for the choices of an `enum`

A missing text shows the name with spaces (`SpawnRate` → "Spawn Rate").

## Saving and changing by code

The values are saved in `ModConfigs/<Mod>_<Config>.json`, next to the
`Worlds` folder. `[DefaultValue]` gives the starting value, and the fresh
instance's field initializers apply where there is none.

```csharp
var pending = (ExampleModConfig)config.Clone();
pending.SpawnRate = 150;
ConfigSaveResult result = config.SaveChanges(pending);
```

`SaveChanges` writes the file, copies the values and calls `OnChanged`. In a
world, a change to a `[ReloadRequired]` field is refused (`NeedsReload`) and
its row is greyed out; in the title screen it is saved and takes effect the
next time the game starts.

## Not available yet

Server-side configs in multiplayer are not synced: a client cannot change a
`ServerSide` config (its rows are greyed out), and the server does not send its
values to the clients (`AcceptClientChanges` is never called). Also missing:
`ModConfig.Open`, tooltips on the screen, custom elements
(`[CustomModConfigItem]`), separate pages, and editing lists, dictionaries,
vectors and free text.

Example: `samples/ExampleModCSharp` (`ExampleModConfig`,
`ModConfigShowcaseRanges` and the supported part of `ModConfigShowcaseMisc`).
Test pack: `tools/tests/csconfig`.
