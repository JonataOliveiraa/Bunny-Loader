# ModPlayer, ModItem and DrawDataCache in multiplayer

The [October 6, 2026 result](RESULTADOS-DRAW-MODITEM.md) records 274 in-game checks, with 8 PvP failures. The drawing and reconnect scenarios passed. The [earlier result](RESULTADOS.md) keeps the October 5 ModPlayer run.

The suite uses two real game processes: a mobile host and a remote client. Install the same APK on both instances and use existing test characters and a test world. The script expects 1600 × 900 screens and the first character and world in the list.

The client needs the server `10.0.2.2:7777` saved and the remote session permission granted. Each emulator has its own NAT; the script forwards the host's port to the computer with `adb forward`.

```bash
export BL_HOST=127.0.0.1:16384
export BL_CLIENT=127.0.0.1:16416
export BL_RUN_ID=20261006-my-run
bash tools/tests/mpmodplayer/run.sh install
bash tools/tests/mpmodplayer/run.sh start
bash tools/tests/mpmodplayer/run.sh collect
```

Wait for `FIM falhas=0` on both sides and `READY_DISCONNECT` on the host. Then run:

```bash
bash tools/tests/mpmodplayer/run.sh disconnect
bash tools/tests/mpmodplayer/run.sh rejoin
bash tools/tests/mpmodplayer/run.sh collect
bash tools/tests/mpmodplayer/run.sh verify
bash tools/tests/mpmodplayer/run.sh cleanup
```

`disconnect` saves the logs of both processes' first session before closing the client. That copy avoids losing the initial result when the logcat buffer rotates during the reconnect. Also wait for `REJOIN_DRAW FIM falhas=0` on both sides before verifying. `verify` requires success in the initial session and in the reconnect. The logs live in `build/mpmodplayer/$BL_RUN_ID/`.

To investigate failures already recorded without giving up on the reconnect test:

```bash
BL_ALLOW_KNOWN_FAILURES=1 bash tools/tests/mpmodplayer/run.sh disconnect
bash tools/tests/mpmodplayer/run.sh rejoin
bash tools/tests/mpmodplayer/run.sh collect
bash tools/tests/mpmodplayer/run.sh verify-lifecycle
bash tools/tests/mpmodplayer/run.sh verify
bash tools/tests/mpmodplayer/run.sh cleanup
```

That option allows disconnecting a client that finished with failures. `verify-lifecycle` checks only the reconnect and its draws; `verify` still fails the complete round. Fixture load errors and fatal signals are not accepted.

`install` saves the launcher settings, selects quick start up to the title screen and temporarily disables the other packages to keep both processes with the same fixture. `cleanup` restores only the changed keys, keeps the other current preferences, removes only this suite's package and stops the port forwarding. The backups remain available on disk. Use the same `BL_RUN_ID` in the attempts and in the cleanup; pick another identifier for a new session after the cleanup. Use `cleanup` after an interrupted run too.

On Windows, run the script with Git's Bash, with `adb`, `python` and `rg` on the PATH. WSL's `bash.exe` does not automatically get those Windows tools.

## Coverage

| Group | Checks |
| --- | --- |
| Isolation | Separate instances, mutable objects, modifiers and counters for each player; a deep copy of the client state. |
| Network | `SyncPlayer` with the host's initial state; automatic `CopyClientState`; `SendClientChanges` with two changes and no extra sends; origin and confirmation of the real packets. |
| Connection | `PlayerConnect`, `PlayerDisconnect` after force-closing the client, a new connection and a new sync. |
| Items | Crit, knockback, scale, speed, time, animation, mana, healing, potion veto and `PreItemCheck`/`PostItemCheck`. |
| ModItem | The 27 methods from steps 1 to 3, with per-item state, changes to native values, collision, hitbox, frames, ammo selection and consumption, the last unit, normal/quick healing and composition with ModPlayer. |
| ModItem PvP | Attacks and vetoes in both directions; observes the remote copy, `SendPlayerHurt` and `OnHurt` in the victim's process; checks callbacks on the victim and the damage actually received. |
| Shooting | Shot veto, `ModifyShootStats` references, values received by `Shoot`, ammo veto and consumption. |
| Combat | An NPC created by the host and replicated; item and projectile modifiers and callbacks on the client; no duplicated callbacks on the host; damage-taken veto and local consumable dodge. |
| Movement | Local and remote updates, natural regeneration, info accessories, extra jump start/veto/visuals/refresh/end and armor bonus. |
| Drawing | Effects, info, layers, data transformation and rendering; local camera and zoom; writing without the counter, the manual counter and `ModPlayer.AddDrawData`, for local/remote in both processes and after reconnecting. |
| World | Fishing level by reference and teleport veto. |

The suite registers every hook to test its installation, but does not claim functional coverage of all of them. Buying, selling, the nurse, rewards, catching, the starting inventory, persistence and a dedicated server need additional scenarios. A callback passing without changing its result does not check all of its edge cases.

ModItem methods not implemented yet stay out of this round. Healing, ammo and weapon queries are exercised on each process's local player; the replication of each feature is not inferred from those results. PvP and drawing have explicit checks between the processes.

`draw.js` creates magenta markers for player 0 and cyan ones for player 1 with `MagicPixel`, without calling `DrawData.Draw`. A diagnostic hook checks the cache after the renderer, including layer reordering. The markers stay until the game closes for visual checking. Those hooks and counters belong only to the fixture.

## Particularities verified

The mobile host runs with `Main.netMode = 3`; the remote client uses `1`. The suite recognizes the host's server bit.

In this native version, `CheckMana` returns `true` even without enough mana and signals the condition in `slowMagicUse`. That is why the tests check the balance and the consumption callbacks, including partial consumption, without using the return value as proof of payment. `blockQuickMana` prevents `OnMissingMana`, as in [tModLoader's flow](https://github.com/tModLoader/tModLoader/blob/stable/patches/tModLoader/Terraria/Player.TML.cs).

The combat NPC gets test life on both instances. Changing `lifeMax` on the host does not make the client receive that custom maximum through the vanilla NPC message. Without preparing the target on the client, the first hit killed it before the projectile test.

The mobile host can deactivate a player without calling the disconnect helpers. `PlayerNetworkHooks` also checks the connected players after the local player's update, keeping a single notification per disconnect. That check needs to run with `LocalUserGameState` loaded; outside that context, the local player may look inactive. `PlayerDisconnect`'s contract is in the [ModPlayer reference](https://docs.tmodloader.net/docs/stable/class_mod_player.html).

The connection keeps the player's original object. If the game replaces the slot with an empty `Player`, the notification uses the previous instance and its ModPlayer state. The reconnect test requires a single notification for the remote and none for the local host.

The complementary logic tests run with:

```bash
node tools/tests/modplayerhooks/check.mjs
node tools/tests/moditemhooks/check.mjs
```
