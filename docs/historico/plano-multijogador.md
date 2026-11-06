# Multiplayer test plan

Written 2026-11-05. Goal: test in multiplayer everything that has only been
tested alone, on the user's phone and on MuMu, without subagents. Runs after
[plano-final-examplemod.md](plano-final-examplemod.md), over the finished content.

## 1. Where we stand

**Covered by a two-device pack** (last run on two MuMus):

| Pack | Covers |
|---|---|
| `csnet` | `ModPacket`, `HandlePacket` round trip |
| `csnetitem` | `NetSend`/`NetReceive` of `ModItem` and `GlobalItem` in message 5 |
| `csnetplayer` | `SyncPlayer`, `CopyClientState`, `SendClientChanges`, connect and disconnect |
| `csextraai`, `csnpcdraw` | `SendExtraAI`/`ReceiveExtraAI` |
| `cspvp` | PvP hooks and attacker item data |
| `csbiomemp` | Biome flags, `ModSystem` world data |
| `csconfigmp` | `ServerSide` config |
| `cstileentity` | Server placing a tile entity for a client |

**Never in multiplayer:** almost all the content ported since, by the record of
each block ("multiplayer not tested"), including the whole ExampleMod.

**Never on a phone:** no record of a phone and an emulator playing together. Every
run so far shares the emulators' timing and the same PC.

### Suspected bug class: `netMode` 3

On the phone the host plays, so its `Main.netMode` is **3**, the raw game field,
and `NetmodeID.Server` is 2. Code written for tML that tests
`Main.netMode == NetmodeID.Server` is false on the host, so a server-only branch
never runs there. tML's ExampleMod has 36 such tests and our sample 14, for
example in `BasicTileEntity` and `ExampleRiftProjectile`. Some are harmless, such
as skipping visuals on a dedicated server. Others may silently break the feature.
Block A of the final plan adds a compile warning and reviews the sample's cases.
Group M12 measures what still breaks, first, because it decides how the rest of the
failures read.

## 2. Topologies

| Name | Host | Clients | Use |
|---|---|---|---|
| A | MuMu 0 | MuMu 1 | Fast regression on every change |
| B | Phone | MuMu 0 | Real ARM host, real network |
| C | MuMu 0 | Phone | Real ARM client |
| D | Phone | MuMu 0 and 1 | Three players: selected groups only |

MuMu has three instances (0, 1, 2) and all were stopped when this was written. Each
needs the beta APK, `com.and.games505.TerrariaPaid` and a character with its own
name.

Network paths:

- **Phone hosting:** MuMu connects out to the phone's ZeroTier address, port 7777.
- **MuMu hosting, phone on ZeroTier:** `adb forward` only listens on localhost, so
  a relay on the PC listens on `10.147.18.202` and forwards to the forwarded port.
- **Phone on USB:** `adb reverse`, which is much faster than ZeroTier.

ZeroTier measured 0.2 to 2 MB/s today, so a 235 MB APK takes 10 to 20 minutes and a
35 MB DLL under a minute at its best. The tools avoid the APK where they can.

## 3. Part T: tools

Built first and in this order. T1 to T4 make a run possible, T5 to T7 make it
cover everything, and the rest make failures quick to read.

### T1. `tools/tests/mp/env.sh`: find the devices

Finds the phone (USB or ZeroTier) and the MuMus (`MuMuManager info`, `adb connect`),
starts stopped instances, and checks the package, the game and the unlocked screen.
Names devices by role, so tests never hold a port. Today `run-mp.sh` hard-codes
`16384` and `16416`, and the phone's port changes on each reconnect.

### T2. Entering the game without touches

`lib.sh` taps fixed coordinates for 1600x900, which do not work on a 2340x1080
phone, and touches on the user's phone are off limits unless asked for.

`csauto` learns to drive itself from a role passed by `setprop`
(`debug.bl.mp.role=host`, or `client:<ip>:<port>`): the phone's game has
`GUIMultiplayerHost.HostServer`, `Netplay.SetRemoteIP` and
`Netplay.StartTcpClient`, and it also dismisses the controls warning.

This needs a spike. If the game's menus refuse it, fall back to a tap table per
resolution, asking before using it on the phone.

### T3. `tools/tests/mp/relay.py`: network paths and bad networks

A TCP relay on the PC for the paths above, with optional delay, jitter, loss and a
timed cut. It gives latency on demand at home, and reconnect tests for M11.

### T4. `tools/tests/mp/deploy.sh`: fast, safe deployment

- Sends only what changed, by hash: `BunnyLoader.dll`, packs, saves.
- Verifies the hash on the device before installing (see section 7).
- Copies the test character and flat world per device, as the phone has no `su`.
- Takes a lock and a PID file, so two runs can never overlap, and kills leftovers
  by command line.
- Backs up and restores the phone's mods, settings and saves. The `uid` is checked
  before any `rm -rf`.

### T5. `MpTestKit`: writing a test becomes short

A small library that packs compile against:

```csharp
public sealed class MyTest : MpTest
{
    protected override IEnumerable<Step> Steps()
    {
        yield return Host("spawn the NPC", () => npc = NPC.NewNPC(...));
        yield return Barrier("spawned");
        yield return Client("sees it", () => Expect(Main.npc[npc].type == type));
    }
}
```

- `Host`, `Client`, `Everyone` steps; `Barrier` waits for all sides, through
  `ModPacket`, with a timeout.
- Results travel to the host, which writes one summary line per test, so one log
  read is enough over a slow link.
- It builds its own arena near the spawn, in god mode, without saving.
- Ticks identify events, as clocks differ: the phone was 9 minutes off the PC today.

### T6. `csmpdiff`: desync detector

An always-on pack. Every N ticks, each side hashes its view and the host compares:

- active NPCs: type, position with tolerance, life, `ai`, mod data sizes
- projectiles: type, owner, position, `ai`
- world items: type, stack, prefix
- players: life, mana, inventory, armor, buffs, mod player data
- a tile window around each player, chests and tile entities

On a mismatch it logs both values and takes a screenshot. It runs beside every
test and during normal play, so playing with the pack on is also a test.

### T7. `csmpsweep`: every piece of content, automatically

Walks `ModContent` by reflection instead of a hand-written list, for any loaded
mod. From the host and then from a client, and through `csmpdiff`:

| Content | Round trip |
|---|---|
| `ModItem` | Drop, pick up, use, chest, prefix, data |
| `ModProjectile` | Spawn, check on the other side, kill |
| `ModNPC` | Spawn, hit from the other side, kill, loot |
| `ModTile`, `ModWall` | Place, frame, break, drops |
| `ModBuff`, `ModMount`, `ModPrefix` | Apply and see it on the other side |

This covers most of the ExampleMod, and whatever comes later. It only proves the
generic path, so features with their own logic also get a scenario below.

### T8. `tools/tests/mp/suite.sh`: the runner

Takes packs and topologies. It writes `tools/tests/out/mp/<run>/` with each device's
log, screenshots on failure, tombstones and a summary table. A crash stops that run
and is reported with the first tombstone. A retry is allowed once and is marked as
a flake in the summary, never counted as a pass.

### T9. `csnettrace`: seeing the traffic

Optional pack: messages sent and received per second, by id and size, and any
dropped message. For reading a desync, and for the bandwidth in M11. It goes on the
game's send path, so it stays off by default.

## 4. Part M: what gets tested

Each group lists what the sweep cannot prove. All run on topology A first, then B
and C. D runs for M1, M6, M9 and M11.

### M0. Baseline

The existing packs of section 1 on A, B and C, before the new tests. Anything
failing here gets fixed or listed first.

### M1. Joining and leaving

Mod sync accepted and refused, the download of a missing mod, texture rules, cheat
rule, vanilla server refused (`debug.bl.nowelcome`), password, joining late with a
boss alive, rejoining, a client dropping, the host leaving.

### M2. World and tiles

Sections with mod tiles and walls, paint and slopes. From a client: placing and
breaking mod tiles and multitiles, doors, chests and dressers with mod items, signs,
tile entities, music box and other wired tiles, pressure plate, statues, banners,
trees, rubble, conversion, pylons and teleporting.

### M3. Items

Mod prefix across the network, `NetSend` data in drops and chests, stacks, ammo,
consumables, healing, reforging, shop purchases from a client, coins. Check the
fixed limits on item types in network messages.

### M4. Player

Mod data on late join, buffs with mod ids, equipment visuals seen by the other
player (wings, armor, hair, dyes), mod mounts, dash, resources, death and respawn,
PvP damage, teleports.

### M5. Projectiles

Mod type spawned by a client, `ai` and extra data, ownership, hits on NPCs from a
client (the phone's `StrikeNPC` sends raw damage in message 28), minions and
`originalDamage`, sentries, whip tags, hostile projectiles, the projectile limit.

### M6. NPCs and bosses

Mod types in message 23, life sync, a boss summoned by a client (message 61 and
`MPAllowedEnemies`), multipart NPCs, boss bar, instanced loot and treasure bags,
banners, catching critters. Town NPCs: arrival, names, housing, dialogue, shops,
happiness, traveling merchant, attacks, and `DrawTownAttack*` seen by the other
player. Emotes.

### M7. Systems and commands

`ModSystem.NetSend`/`NetReceive`, `ModCommand` of each type (client, server, world),
chat broadcasts, downed flags and `ModSystem` data on join.

### M8. Visuals and sound

Dust, gore, combat text, sounds and mod music heard by each side, and screen effects
staying local. The check is that nothing crashes and nothing is sent twice. What it
looks like is checked by screenshot.

### M9. Events and `ModInvasion`

`csinvasionmp`: start from a client (request) and from the host, spawning around
each player, points from kills by either side, wave change, bar values on both,
late joiner mid-wave, failure, reward, save on the host, block against vanilla
events. Rain, time, biomes and scene effects on each side.

### M10. Saves after a session

World reopened by the host and character reopened by each client, with mod data
intact and nothing duplicated or lost.

### M11. Robustness

- 10-minute play with `csmpdiff` on, including a boss and an invasion.
- With the relay: 150 ms delay, jitter, a little loss, and a cut followed by a rejoin.
- The app sent to the background and the phone locked, as host and as client.
- Three players (D). Bandwidth, and frame time compared with single player.

### M12. `netMode` 3 (first)

The warning, the review of the sample and the guide were done in block A of the
final plan. Here:

1. List every `netMode` test in `BunnyLoader`, by what it means: "I am the server",
   "I am not a client", "I render".
2. Run each sample case on a phone host and record what happens.
3. Fix what still breaks, and revisit D6 if the warning is not enough.

## 5. Order

Starts when the final plan is finished.

1. T1, T2, T4: a run starts by itself on every topology.
2. M0, then M12.
3. T5, T6, T7, T8, T3.
4. Sweep on A, then B and C. Fix what it finds.
5. M1 to M11 by group. T9 when a failure needs it.
6. A full `suite.sh` run on A, B and C before closing, with a table of what was
   covered and what stays out.

## 6. Limits to state in the result

- Three devices is the maximum here, and MuMu's graphics differ from the phone's.
- Crossplay with the iOS or PC game is not covered.
- Screen touches on the phone need the user's go-ahead or the user's hands.
- The phone must be free and unlocked during its runs. Plan these with the user.

## 7. Lessons already paid for

From 2026-11-05, and built into T4:

- On Windows, stopping a background task did not kill the script or its `adb push`.
  Two runs overlapped, truncating the DLL (`BadImageFormatException` in the mod
  compiler) and corrupting an APK push.
- `run-phone.sh restore` with a path instead of the short pack name gets an empty
  `uid` and would run `rm -rf bunny_packs/`.
- `GameActivity` is not exported: return to the game with `monkey`, not `am start`.
- `IsMusicPlaying` reads stored state now: check real audio with `dumpsys audio`.

## 8. Decision

| # | Question | Answer |
|---|---|---|
| D6 | What should `Main.netMode == NetmodeID.Server` mean on a phone host? | Left to the implementer by the user (2026-11-05). Keep the raw field, as hiding it would break visuals on the host, and warn at compile time in `ModCompiler`, suggesting `(Main.netMode & NetmodeID.Server) != 0`. Revisit only if M12 shows this is not enough |
