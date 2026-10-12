# ModNPC AIType

## Analysis

Input: a `ModNPC` instance, `AIType`, `npc.aiStyle`, local and global hooks.
Output: running the vanilla AI with a temporary type, keeping the NPC's
identity for the later callbacks.

Sources checked:

- [tModLoader stable's ModNPC.cs](https://github.com/tModLoader/tModLoader/blob/stable/patches/tModLoader/Terraria/ModLoader/ModNPC.cs):
  `AIType` is a per-instance integer, defaulting to zero; `AnimationType` is
  another property.
- [tModLoader stable's NPCLoader.cs](https://github.com/tModLoader/tModLoader/blob/stable/patches/tModLoader/Terraria/ModLoader/NPCLoader.cs):
  `NPCAI` asks `PreAI`, swaps `npc.type` when `AIType > 0`, runs `VanillaAI`,
  restores the type, runs `AI` and then `PostAI`. Every global `PreAI` is
  asked before the local one; a global veto prevents the local `PreAI`. `AI`
  and `PostAI` run the local one before the globals.
- [tModLoader's NPC.cs.patch](https://github.com/tModLoader/tModLoader/blob/stable/patches/tModLoader/Terraria/NPC.cs.patch):
  separates `AI` and `VanillaAI`; family selection stays in `aiStyle`.
- Local references in `exmod_tl/ExMod_v1.7.1 (3)/Modified/1.mod/TL/` and
  `exmod_tl/ExMod_v1.4.2 (2)/Modified/1.mod/TL/`: `ModNPC.js`,
  `Hooks/NPC.js` and `Loaders/NPCLoader.js`. Both declare `AIType = 0` and do
  the swap around `NPC.AI`'s original body, restoring before the callbacks.
  ExMod reads the value from the type's template, keeps a temporary
  `realTypes` map and does not protect the restoration with `finally` in that
  section. Its local `PreAI` comes before the globals, whose lookup can stop
  at the first veto; that ordering differs from tModLoader.
- Bunny Loader's `ModProjectile.js` and `Loaders/ProjectileLoader.js`: an
  existing per-instance property and restoration with `try/finally`.
- The mobile dump `refs/dump.cs` and the disassembly of `Terraria.NPC$$AI`:
  family 2 calls `AI_002_FloatingEye`; Android does not have the
  `NPC.VanillaAI` added by tModLoader. Here `NPC.AI`'s original body is the
  vanilla step.

Problems identified before the change:

1. The `ModNPC` base did not declare `AIType`.
2. The AI hook was only installed and marked for classes that overrode
   `PreAI`, `AI` or `PostAI`. Just setting the property in `SetDefaults`
   would neither install nor enable the hook.
3. Local and global hooks were independent wrappers. With the type swap, an
   inner global wrapper could receive the borrowed type, re-evaluate
   `AppliesToEntity` and lose the original global instance. The order of
   vetoes and callbacks also depended on the chaining.

Usage assumption: `AIType` receives a positive vanilla NPC ID compatible with
`aiStyle`. The loader neither picks the family nor clones stats. As in
`ModProjectile`, the read uses a 32-bit JS integer; non-positive values do not
activate the replacement. That neither validates arbitrary positive IDs nor
makes every vanilla AI suitable for any NPC.

Edge cases: the property without hooks, inheritance, later registration, two
instances, swapping in `PreAI`, zero, negatives, vetoes, exceptions, a type
change inside the AI, conditional and per-entity globals, different
installation orders and repetition over hundreds of ticks.

## Design

| Approach | Time per call | Space | Maintenance |
|---|---|---|---|
| Swap inside the existing local wrapper | O(1) local, O(G) in the globals | O(1) temporary | Simple, but the inner globals may see the borrowed type and the order depends on installation |
| A shared AI dispatch, with a native filter when there are no globals | O(1) local, O(G) in the globals | A per-class plan and O(1) temporary | A single order and restoration contract, independent of installation |

The second approach was adopted. `NPCLoader.InstallAI` installs the filtered
path for any ModNPC, allowing the property to be set without overriding AI.
When there is a global with AI methods, it installs the shared global dispatch
and turns the local path off through a native flag. A later local registration
does not turn the disabled path back on.

With no AI global, vanilla NPCs pass the `FIRST_NPC` filter without entering
JS. With an AI global, there is one JS dispatch per call. The local plans and
labels are reused; the global callbacks use the existing registry. A
`GlobalNPC` that only implements other events does not enable the global
dispatch.

## Implementation

The contract follows tModLoader for order and vetoes, and the protection
already used in Bunny Loader's ModProjectile to restore the type in `finally`.
The value is read from the instance after `PreAI`. Only `npc.type` is
replaced: `netID`, `aiStyle`, `AnimationType`, AI state and the instance's
identity are not swapped. Changes the AI makes to velocity, state and activity
are kept.

As in tModLoader's `NPCAI` section, if the borrowed AI changes `type`, the
previous type is still restored. Without `AIType`, native type changes are not
undone. The mod's intentional transformations can be done after the borrowed
step, in its `AI`.

An exception in the mod's callbacks is reported through the `Safe` mechanism
and the other stages go on according to the existing contract. An exception
propagated by the original body restores the type before leaving; running
`PostAI` on that exceptional path is not promised, as in tModLoader.

## Tests without a device

```powershell
node tools/tests/npcaitype/check.mjs
node tools/tests/modnpchooks/check.mjs
node tools/tests/globalprojectilehooks/check.mjs
node tools/tests/projectilekill/check.mjs
node tools/tests/modplayerhooks/check.mjs
```

The suite loads the production classes, `ModNPC.register`, `Entities`,
`GlobalRegistry`, `GlobalNPC.register` and the real loaders. Only the bridge
to Terraria is simulated. It checks filters, native flags and both wrapper
chaining directions. The `netMode` 0, 1 and 2 cases check that the dispatch is
independent of the mode; they do not replace a multiplayer test with two
devices. The original call's exception is injected into the simulation and
checks the restoration and the recovery on the next call.

## Android integration

Requires a full debug APK, ADB with root and local saves. End the current
session before preparing. The runner keeps the preferences, enables only this
fixture and uses `BL_NPCAIType_Test.plr/.wld`, copies of the existing saves.
It restores the preferences and removes only the fixture when done.

```powershell
python tools/tests/npcaitype/device.py prepare --order mod-first
python tools/tests/npcaitype/device.py launch --order mod-first
# Wait for completion in the game log.
python tools/tests/npcaitype/device.py collect --order mod-first
python tools/tests/npcaitype/device.py restore

python tools/tests/npcaitype/device.py prepare --order global-first
python tools/tests/npcaitype/device.py launch --order global-first
python tools/tests/npcaitype/device.py collect --order global-first
python tools/tests/npcaitype/device.py restore

python tools/tests/npcaitype/device.py prepare --order local-only
python tools/tests/npcaitype/device.py launch --order local-only
python tools/tests/npcaitype/device.py collect --order local-only
python tools/tests/npcaitype/device.py restore
```

The fixture observes `AI_002_FloatingEye` and `AI_003_Fighters` inside the
native body, compares velocity and direction with a vanilla Wandering Eye
under controlled conditions, checks vetoes, inheritance and independent
instances and runs 300 real calls to check repeated restoration. It also keeps
an NPC with only `AIType` for 30 ticks of the normal world loop, without the
fixture calling its AI during that interval. The NPCs are temporary and
deactivated in `finally`; the temporary change to `Main.dayTime` is restored.
It grants no items and does not change blocks. The logs live in
`build/npcaitype/<device>/`.

The run results are in [RESULTADOS.md](RESULTADOS.md).
