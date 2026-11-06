# Touch buttons

On the phone there is no keyboard: a `ModKeybind` becomes a **touch button**
that the player places on the screen with the game's own controls editor (the
same one used for Jump, Heal and Zoom). The mod reads it as in tModLoader:

```csharp
public class KeybindSystem : ModSystem
{
    public static ModKeybind RandomBuffKeybind { get; private set; }

    public override void Load()
    {
        RandomBuffKeybind = KeybindLoader.RegisterKeybind(Mod, "RandomBuff", "P");
    }
}

public class ExampleKeybindPlayer : ModPlayer
{
    public override void ProcessTriggers(TriggersSet triggersSet)
    {
        if (KeybindSystem.RandomBuffKeybind.JustPressed)
            Player.AddBuff(Main.rand.Next(BuffID.Count), 600);
    }
}
```

The default key (`"P"`) is kept for tModLoader code; the phone ignores it.
`Current`, `JustPressed`, `JustReleased`, `Old` and `RetroCurrent` work as in
tModLoader. `GetAssignedKeys()` gives the button's name while it is in the
layout in use, and nothing otherwise.

The name comes from `Mods.<Mod>.Keybinds.<Name>.DisplayName` (or
`TouchButtons.<Name>.DisplayName`); without it, the name split at capitals.

## `ModTouchButton`

`KeybindLoader.RegisterKeybind` returns a `ModTouchButton`, and
`TouchButtonLoader.RegisterButton(Mod, name)` gives one directly. It has what
only the phone needs:

| Property | What it does |
| --- | --- |
| `Icon` | the icon's path in the mod; by default `Assets/TouchButtons/<Name>.png`, then the mod's icon |
| `Label` | a `Func<string>` for a name that changes |
| `Visible` | a `Func<bool>`; `false` hides the button from the screen |
| `Enabled` | a `Func<bool>`; `false` greys it out and it does not press |
| `OnPress` | an `Action<Player>` run once per touch |
| `IsBound` | whether the button is in the layout in use |

```csharp
ModTouchButton dash = TouchButtonLoader.RegisterButton(Mod, "Dash");
dash.OnPress = player => player.velocity.X = 12 * player.direction;
dash.Enabled = () => !Main.LocalPlayer.mount.Active;
```

## `ModTouchJoystick`

`TouchButtonLoader.RegisterJoystick(Mod, name)` gives a button the player drags
to aim, like the game's own aim stick. It has everything `ModTouchButton` has,
plus:

| Member | What it does |
| --- | --- |
| `Direction` | where the stick points now, as a `Vector2`; `Vector2.Zero` at rest |
| `Aiming` | whether the player is holding it; the same as `Current` |
| `OnRelease` | an `Action<Player, Vector2>` run when it is let go, with the last direction |

```csharp
public class AimSystem : ModSystem
{
    public static ModTouchJoystick Aim { get; private set; }

    public override void Load() => Aim = TouchButtonLoader.RegisterJoystick(Mod, "Aim");
}

public class AimPlayer : ModPlayer
{
    public override void OnEnterWorld() => AimSystem.Aim.OnRelease = Shoot;

    static void Shoot(Player player, Vector2 direction)
    {
        Projectile.NewProjectile(player.GetSource_FromThis(), player.Center, direction.SafeNormalize(Vector2.UnitX) * 12f, ProjectileID.WoodenArrowFriendly, 10, 1f, player.whoAmI);
    }
}
```

Read `Direction` in `ProcessTriggers` to show where it aims while it is held.

Example: `samples/ExampleModCSharp/Common/Systems/TouchButtonSystem.cs` and
`Common/Players/ExampleTouchButtonPlayer.cs`. Test pack: `tools/tests/csextouch`.

## In the controls editor

The game's action list keeps its size: after the game's actions comes
**Mod buttons**, which opens the mod buttons' pages (with **Back** and, when
there are more, **More**). Opening the list of a slot that already has a mod
button shows its page.

The layout file of the game (`VirtualControls.json`) never keeps a mod button,
so it stays valid without the mod or without Bunny Loader. The mod buttons of
each layout go to `BunnyLoader.touchbuttons.json`, by name and slot; a button
of a mod that is turned off stays saved and comes back with the mod.

Example: `samples/ExampleModCSharp/Common/Systems/KeybindSystem.cs` and
`Common/Players/ExampleKeybindPlayer.cs`. Test pack: `tools/tests/cstouch`
(42 buttons: tap the mount slot while it runs).
