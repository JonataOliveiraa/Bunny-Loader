# Invasions

`ModInvasion` is a wave-based event that uses the game's own progress bar. It is
ours: tModLoader has no such class. The design comes from Animatak_'s Example
Invasion, who also made the Paladin Crusade and Grave Legion that the ExampleMod
carries, with their icons and texts.

```csharp
public class GraveLegion : ModInvasion
{
    public override Color MessageColor => new Color(130, 230, 160);

    public override IList<InvasionWave> Waves { get; } = new[] {
        new InvasionWave(15,
            new InvasionEnemy(NPCID.Zombie, 3),
            new InvasionEnemy(NPCID.Skeleton, 2)),
        new InvasionWave(25,
            new InvasionEnemy(NPCID.AngryBones, 2),
            new InvasionEnemy(NPCID.DoctorBones, 0.3f, 5)),
    };

    public override int SpawnRate => 40;

    public override int MaxSpawns => 10;
}
```

`Register` is automatic, like any `ModType`. To start it, from an item for example:

```csharp
public override bool CanUseItem(Player player) => InvasionLoader.CanStart(ModContent.GetInstance<GraveLegion>());

public override bool? UseItem(Player player) {
    if (player.whoAmI == Main.myPlayer)
        InvasionLoader.Start(ModContent.GetInstance<GraveLegion>());
    return true;
}
```

See `Content/Invasions` and `Content/Items/Consumables/GraveBell.cs` in the ExampleMod.

## Waves

| Type | What it means |
|---|---|
| `InvasionWave(int Points, params InvasionEnemy[] Enemies)` | The wave ends when the kills add up to `Points`, scaled by `ScalePoints`. An empty enemy list keeps the world's normal spawns |
| `InvasionEnemy(int Type, float Weight = 1, int Points = 1)` | An NPC of the wave, its weight in the spawn pool and what a kill is worth |

Kills only count on the server. Statues and friendly NPCs give no points.

## Members

| Member | Default | Use |
|---|---|---|
| `Waves` | required | The waves, in order |
| `DisplayName` | `Mods.<Mod>.Invasions.<Name>.DisplayName` | The bar's title and the messages' `{0}` |
| `IconTexture` | `<class path>_Icon` | The bar's icon |
| `MessageColor` | purple | The chat messages' color |
| `Music` | `-1`, none | Music while it is active, at event priority |
| `SpawnRate`, `MaxSpawns` | `60`, `8` | Replace the game's spawn rate |
| `CanStart()` | `true` | Refuse to start, for example by day |
| `CanSpawn(player)` | on the surface | Where the wave spawns around a player |
| `ShouldFail()` | `false` | Checked every tick on the server. `true` ends it in failure |
| `PointsFor(npc, wave)` | the enemy's `Points` | What a kill is worth |
| `ScalePoints(points, players)` | +50% per extra player | Points needed for a wave |
| `OnStart`, `OnWaveStart`, `OnWaveComplete`, `OnComplete`, `OnFail`, `Update` | empty | Run on the server |

## `InvasionLoader`

| Member | Use |
|---|---|
| `Active`, `Wave`, `Progress`, `ProgressMax` | The current state, on every side |
| `CanStart(invasion)` | `false` during another invasion, a moon event or the Old One's Army, or when `CanStart` refuses |
| `Start(invasion)` | On a client it sends a request and returns `true`, and the server decides |
| `Stop(success)`, `AddPoints(points)` | Server only |
| `IsDowned(invasion)` | Whether it was ever completed in this world |

While a mod invasion is active the game's invasions, moon events and the Old One's
Army do not start, because two events would fight over the bar.

## Texts

The messages use your keys when they exist, and otherwise a translated default:

```hjson
Invasions: {
    PaladinCrusade: {
        DisplayName: Paladin Crusade
        Start: The Paladins' crusade has begun!
        Wave: "{0}: wave {1} of {2}!"
        Fail: The Paladins retreat at dawn.
        Complete: The Paladins' crusade has been repelled!
    }
}
```

## Saving and multiplayer

The active invasion and the downed list are saved with the world. A player joining
late gets the state at once, every change goes out in a packet, and messages are
translated on each client.
