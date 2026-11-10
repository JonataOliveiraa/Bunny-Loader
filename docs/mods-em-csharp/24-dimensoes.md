# Dimensions

A dimension (subworld) is a separate world the player travels to and comes
back from: an arena, a dungeon, another planet. It has its own size, its own
generation and its own save file, next to the main world. `ModSubworld` is
Bunny Loader's own class.

```csharp
public class Moon : ModSubworld
{
    public override int Width => 1200;

    public override int Height => 600;

    public override bool Persistent => true;

    public override List<GenPass> GenerationTasks => new() {
        new PassLegacy("Surface", (progress, configuration) => {
            progress.Message = "Shaping the moon";
            int ground = (int)Main.worldSurface + 1;
            for (int x = 0; x < Main.maxTilesX; x++)
                for (int y = ground; y < Main.maxTilesY - 1; y++)
                    WorldGen.PlaceTile(x, y, TileID.Stone, true, true);
        }),
    };
}
```

`Register` is automatic, like any `ModType`. To go there and back, from an
item for example:

```csharp
public override bool? UseItem(Player player) {
    if (player.whoAmI == Main.myPlayer) {
        if (SubworldLoader.IsActive<Moon>()) SubworldLoader.Exit();
        else SubworldLoader.Enter<Moon>();
    }
    return true;
}
```

While a dimension is active, the pause menu's **Save & Exit** becomes **Exit
Dimension**, with a planet icon, and takes the player back to the main world.

The Example Mod has two: `Content/Subworlds/ExampleSubworld.cs`, a flat plain
with every default, and `Content/Subworlds/ExampleAdvancedSubworld.cs`, with
hills, caves, ore, an endless night, carried data and a transition with no
loading screen. The keys in `Content/Items/Tools/ExampleSubworldKey.cs` and
`ExampleAdvancedSubworldKey.cs` take the player there and back.

## `SubworldLoader`

| Member | |
|---|---|
| `Enter<T>()`, `Enter(ModSubworld)` | Starts the trip. `false` when it cannot start: already traveling, already there, a client in multiplayer. |
| `Exit()` | Back to the main world. |
| `Current` | The active dimension, `null` in the main world. |
| `InSubworld`, `IsActive<T>()` | Shortcuts for `Current`. |
| `Traveling` | `true` from `Enter` until the player is in the new world and its transition is over. |
| `Subworlds` | Every registered dimension. |

## What a dimension decides

| Member | Default | |
|---|---|---|
| `Width`, `Height` | (required) | Size in tiles, up to 8400 × 2400. |
| `GenerationTasks` | (required) | The passes that build it, `GenPass` or `PassLegacy`, as in [world generation](12-worldgen.md). |
| `Persistent` | `false` | `true` keeps the world between trips; `false` generates it again on every trip and deletes the file on the way out. |
| `SavePlayerChanges` | `true` | `false` gives the player back as they entered: inventory, health, everything in the `.plr`. |
| `RandomTileUpdates` | `false` | Grass spreading, plants and trees growing, corruption spreading. Liquids, wiring and tile entities update either way. |
| `HideUnderworld` | `true` | The bottom 200 rows are not the underworld: no underworld background, light, music or enemies, water does not evaporate there and the player's zone is not the underworld. `Main.UnderworldLayer` is `Main.maxTilesY` while it is on. |
| `LoadingText` | "Loading Dimension" | The text of the loading screen. |
| `ShowLoadingScreen` | `true` | `false` keeps the screen black while the world changes, with `DrawTransition` on top. |
| `TransitionOutSeconds`, `TransitionInSeconds` | `0` | How long the transition takes in the world being left and in the world being entered. See [Transitions](#transitions). |
| `DisplayName` | the class name | The world's name, from the `Subworlds` category of the translations. |

Before the passes run, the world is cleared, `Main.worldSurface` is 30% of the
height, `Main.rockLayer` 50%, the spawn point is the middle of the surface,
and the backgrounds, trees and moon get a random style, as in a new world.
Move `Main.spawnTileX`/`Main.spawnTileY` in a pass to put the player
elsewhere.

## Hooks and their order

On every trip, in this order:

1. `SaveCarriedData(TagCompound)` on every dimension: the world being left
   writes what should travel along.
2. `OnExit()` of the dimension being left.
3. `OnEnter()` of the dimension being entered.
4. The world loads, or generates and saves.
5. `LoadCarriedData(TagCompound)` on every dimension, with its own tag back.
6. `PostWorldReady()`, once the player is in the world.

`Update()` runs once per world update while the dimension is active.
`ModSystem` hooks (`OnWorldLoad`, `SaveWorldData`, `PostUpdateWorld`...) run
in dimensions too, and each dimension saves its own `ModSystem` data.

Carrying data, for example a boss defeated in the dimension:

```csharp
public override void SaveCarriedData(TagCompound tag) => tag["boss"] = DownedSystem.downedMoonBoss;

public override void LoadCarriedData(TagCompound tag) => DownedSystem.downedMoonBoss = tag.GetBool("boss");
```

## Transitions

By default a trip shows the loading screen. A dimension can play its own
transition instead: the world being left keeps running while the screen goes
dark, the screen stays dark while the world changes, and the new world shows up
as it clears.

```csharp
public override bool ShowLoadingScreen => false;

public override float TransitionOutSeconds => 1.5f;

public override float TransitionInSeconds => 1.2f;
```

`DrawTransition(SpriteBatch spriteBatch, float progress)` draws it over the
whole screen, the interface and the touch controls included. While it draws,
the screen is `Main.screenWidth` × `Main.screenHeight`.
`progress` goes from 0 to 1 while leaving, stays at 1 while the world changes,
and goes back to 0 while arriving. The default draws black with `progress` as
its opacity; override it for anything else, and call `base.DrawTransition` to
keep the fade:

```csharp
public override void DrawTransition(SpriteBatch spriteBatch, float progress) {
    float eased = progress * progress * (3f - 2f * progress);
    base.DrawTransition(spriteBatch, eased);
    int lid = (int)(Main.screenHeight / 2f * eased * eased);
    Texture2D pixel = TextureAssets.MagicPixel.Value;
    spriteBatch.Draw(pixel, new Rectangle(0, 0, Main.screenWidth, lid), Color.Black);
    spriteBatch.Draw(pixel, new Rectangle(0, Main.screenHeight - lid, Main.screenWidth, lid + 1), Color.Black);
}
```

The transition belongs to the dimension being entered, or to the one being left
when the trip goes back to the main world. With `ShowLoadingScreen` on,
`TransitionOutSeconds` and `TransitionInSeconds` still fade in and out around
the loading screen.

## Music and backgrounds

A `ModSceneEffect` that is active in the dimension, as in [biomes and
scenes](13-biomas.md):

```csharp
public class MoonScene : ModSceneEffect
{
    public override int Music => MusicID.SpaceDay;

    public override SceneEffectPriority Priority => SceneEffectPriority.Environment;

    public override bool IsSceneEffectActive(Player player) => SubworldLoader.IsActive<Moon>();
}
```

## Files

A dimension is saved in `Worlds/<main world id>/<Mod>_<Name>.subworld`, with
Bunny Loader's own files beside it. Each main world has its own dimensions,
they do not show up in the world list, and deleting the main world deletes
them too.

## Multiplayer

Everyone travels together:

- Only the host starts a trip. `Enter` on a client shows "Only the host can
  open dimensions" and returns `false`.
- Every client gets a request with the dimension's name, the portrait of every
  player (their own one framed in yellow, with "You"), **Accept**, **Decline**
  and 30 seconds. Everyone sees each answer: green for accept, red for decline.
- Any decline, or no answer in time, cancels the trip for everyone. When nobody
  accepted, only the host gets a message, and can ask again right away.
- When everyone accepts, the clients disconnect, the host changes world, and the
  clients join again by themselves (the host refuses joins until it is ready).
- **Exit Dimension** is the host's alone, with no vote: everyone goes back.
- A transition plays for everyone: the clients fade out and leave, the host
  fades out and changes world, and each one fades in once it is back in.

`SubworldLoader.Current` is the same on the host and on the clients.

## Limits

- A trip in multiplayer reconnects the clients: what only lives on a client
  (open chests, the camera) starts over.
