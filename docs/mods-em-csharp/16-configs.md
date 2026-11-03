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
It has one tab per mod (with the mod's
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

## Opening a config from code

`config.Open()` opens the screen on the mod's tab, as in tModLoader. In a
world it opens over the game and Back returns to it; `onClose` runs when the
screen closes. `scrollToOption` scrolls to a field (`nameof(MyConfig.Field)`),
or to the header above it with `"Header:Field"`; `centerScrolledOption: false`
puts it at the top instead of the middle.

```csharp
ModContent.GetInstance<ExampleModConfig>().Open(onClose: () => Main.NewText("Closed"), scrollToOption: nameof(ExampleModConfig.SpawnRate));
```

## Client-side and server-side configs

`Mode` says whose values count:

- `ConfigScope.ClientSide`: each player has their own values, saved on their
  device. They never go over the network.
- `ConfigScope.ServerSide`: in multiplayer the host's values count for
  everyone. A player who joins receives them (their own file is not touched)
  and gets their own values back on leaving. Only the host sees these configs
  on the screen, under "Server Side Config"; a client in multiplayer does not
  see them. They also show in single player and on the title screen.

When a mod's code on a client changes a `ServerSide` config, `SaveChanges` returns
`RequestSentToServer` and the change is not applied yet. The host checks it:
a `[ReloadRequired]` change is refused, and then `AcceptClientChanges` decides.
An accepted change is saved by the host and sent to every player, a refusal
only to the one who asked. Everyone then gets
`HandleAcceptClientChangesReply(success, player, message)`, and the chat shows
the result. The host's own changes are applied directly and sent to everyone.

```csharp
public override bool AcceptClientChanges(ModConfig pendingConfig, int whoAmI, ref NetworkText message)
{
    if (!NetMessage.DoesPlayerSlotCountAsAHost(whoAmI))
    {
        message = NetworkText.FromKey("tModLoader.ModConfigRejectChangesNotHost");
        return false;
    }
    return true;
}
```

If the host uses other values in `[ReloadRequired]` options, the joining
player gets them anyway, with a chat warning: content loaded at start only
follows them after restarting the game with those values (tModLoader reloads
the mods instead).

## Tooltips

An option's tooltip comes from `Mods.<Mod>.Configs.<Config>.<Option>.Tooltip`,
or from `[TooltipKey]` and `[TooltipArgs]`, as in tModLoader. The touch screen
has no hover, so an option with a tooltip shows a `?` button right after its
name. Touching it opens a panel over the list with the option's name and the
tooltip, wrapped to the panel's width (`
` starts a new line). Any touch
closes it, and touching the `?` does not change the option.

```hjson
Configs: {
	ExampleConfig: {
		ShowTrail: {
			Label: Show trail
			Tooltip: Draws a trail behind the sword.
		}
	}
}
```

## Not available yet

Custom elements (`[CustomModConfigItem]`), separate pages, and editing lists, dictionaries,
vectors and free text.

Example: `samples/ExampleModCSharp` (`ExampleModConfig`,
`ModConfigShowcaseRanges`, `ModConfigShowcaseAcceptClientChanges` and the
supported part of `ModConfigShowcaseMisc`). Test packs: `tools/tests/csconfig`,
`tools/tests/csconfigopen` (`Open`, checked by a screenshot), and
`tools/tests/csconfigmp` on two devices (`tools/tests/run-mp.sh csconfigmp`).
