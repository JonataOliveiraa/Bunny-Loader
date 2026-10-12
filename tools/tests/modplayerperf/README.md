# ModPlayer cost review

The [October 6 measurement on the current APK](../hookperf/RESULTADOS.md) adds the comparison of 0, 1 and 8 classes, ModItem filters, AddDrawData and the cost of hiding/reordering layers. The report below describes the earlier review.

The [comparison after the drawing and hair optimizations](../hookperf/RESULTADOS-OTIMIZACOES.md) records the native results and the cache preservation tests.

## Analysis

The API registers hooks on demand: a class that does not override a method does not install its group of hooks. The cost depends on the methods actually overridden, the players updated and the native calls observed. The list of classes interested in each method was already cached.

The review found avoidable work in per-callback dispatch, in jump states, in drawing and in global hooks that observed calls outside their context. It also found a flaw in the bridge: `whileIn` only checked the first hook installed for a method. When that hook had a filter that rejected the call, another active hook on the same method did not open the expected context.

The constraints are preserving the order and frequency of the callbacks, player isolation, vetoes and nullable decisions, recovery after exceptions and later class registration. The cases exercised include no interested classes, calls outside the context, native failures, slot replacement and reconnecting.

## Design

| Approach | Time | Space | Maintenance |
| --- | --- | --- | --- |
| Reduce allocations and filter calls before JavaScript | Dispatch stays O(M); O(1) filters; network tracking O(C) per local tick. | Fewer temporary objects; cache and states limited to the participants. | Keeps the game's existing points and the hooks' frequency. |
| Run callbacks less often | Reduces calls according to the chosen interval. | Requires scheduling state. | Changes the semantics of update and effect methods. |
| Gather the points into new native hooks | Can reduce bridge crossings. | Requires new structures and native code. | Requires finding equivalent points and keeping their order in each game version. |

The first approach was applied. M is the number of classes interested in a callback; C is the number of players tracked as connected. No callback was limited to running every few ticks.

## Implementation

- `PlayerLoader`: direct dispatch, without a closure and a per-instance label. Exceptions stay isolated and reported through `Safe.Report`. Vetoes still ask every class; `First` keeps stopping at the first `true`. Calls with no interested classes create no instances. Registering a class invalidates the cache.
- `PlayerJumpHooks`: field names prepared once and states in bit masks for the nine existing jumps. A veto without a jump entry avoids reading every state. `OnExtraJumpCleared` alone installs neither `JumpMovement` nor the sound hooks. Sounds only enter JavaScript inside the jump movement.
- `PlayerDrawHooks`: `DrawEffects`' five references are only created if someone is interested. Segment data is only read when there is reordering. The layer calls are filtered natively when no layer needs a change. The states and filters are restored on exit, including after exceptions.
- `PlayerCombatHooks` and `PlayerItemHooks`: collisions, contextual damage, projectile creation and ammo cleanup use native filters. Global collision is not installed for a class that only modifies item damage.
- `PlayerNetworkHooks`: keeps the address on connect, reads the players array once per scan and removes disconnected players from the collection walked each tick. Deduplication lives in a separate collection, without keeping references to the old player after notifying it.
- Native bridge: `whileIn` uses a counter shared by the hooks of the same method, per thread, in O(1). Reentry control stays separate per hook. The extra table uses 5,888 bytes per thread in this slot configuration.

The references handed to the mod are not reused between callbacks: the mod may keep those objects. Copying the client state and detecting disconnection stay at the original frequency. Groups that observe different update points can still install several hooks on `Player.Update`; this review does not change that chain's order.

## Tests and measurement

```bash
node tools/tests/modplayerhooks/check.mjs
node tools/tests/modplayerperf/bench.mjs
node tools/tests/modplayerperf/bench.mjs 44bd638
```

The Node benchmark is for comparing dispatch in isolation. Its runtime is V8; its times do not represent Android's QuickJS.

For the native measurement, package `manifest.json` and `content/`, install this package on a device with the desired APK and enter a test world. The package registers eight ModPlayers with simple callbacks, warms up for 120 ticks, measures seven samples of 20,000 calls and collects statistics over 300 ticks. The output starts with `modplayerperf`.

`CapAttackSpeeds_8` includes the native call, the bridge crossing and the eight instances' `PostUpdateMiscEffects` callback. `ItemCheck_veto_8` includes the veto by the eight instances and `PostItemCheck`; the native body is vetoed. These numbers are not the total time of a frame.

`whileIn_multiplos_hooks=ok` requires a second hook to open the context when the first was rejected by a filter. It also checks that the call outside that context does not go through JavaScript.

Compare APKs on the same device, character, world and set of mods. The number of drawn frames can vary during the 300 ticks; normalize drawing statistics by their own calls. `bl.hookStats`' times exclude `original()` and do not measure the cost of the native path that rejects a callback. Emulator load variation prevents reading a single comparison as a guaranteed FPS gain.

The [multiplayer](../mpmodplayer/README.md) suite checks behavior on a real host and client after the optimizations. The logic regressions include filtering, no interested classes, later registration, exceptions and state restoration.
