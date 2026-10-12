# ModNPC: contracts and validation

## Analysis

Input: a native NPC tied to a ModNPC instance, spawn sources, attackers,
mutable references and drawing or shop calls. Output: per-entity events,
death and damage decisions and parameters used by the game.

Facts checked: signatures and parameter names in `refs/dump.cs`; the
disassembly of the methods of mobile version 301720; tModLoader's ModNPC
contracts and NPCLoader ordering on the stable branch. Mobile does not have
tModLoader's NPC.HitModifiers, NPC.HitInfo and Player.HurtModifiers
structures. The combat callbacks use the JavaScript objects already adopted
in Bunny Loader's ModPlayer flow, with the fields described in the reference.

Assumption: mods respect those fields and the return types. No missing
parameter is filled with an estimated value. The methods below were dropped
as authorized in the request.

Edge cases: null and undefined, a ModPlayer veto after a ModNPC permission,
alpha zero, two entities of the same type, nested hits, a projectile with
owner 255, zero damage, a hit received from the network, an inactive NPC, a
type without overrides, later registration, exceptions and restoring the
drawing and combat context.

## Design

| Approach | Time per call | Space | Maintenance |
|---|---|---|---|
| Combat hooks separate from ModPlayer | Two JS entries in the methods already shared, duplicated lookups | Two attacker contexts | Order and notifications depend on installation |
| Shared dispatch and native filters for events and drawing | O(1) to find the instance and the per-class plan; only written callbacks | A per-class plan, a per-hit context; a map of NPCs with DrawBehind | One damage and restoration sequence per flow |

The second approach keeps one hook per combat point. The event, getter and
drawing filters reject types without an override before entering JavaScript.
The error labels are built once per class. ResetEffects creates no closures
per tick. DrawBehind only walks the tracked entities, removing inactive ones
and types that stopped implementing the method. TownNPCProfile registers the
profile once after content, with no per-frame hook.

The color change test checks PackedValue, avoiding reading four channels
separately across the bridge on each comparison. Hurt's damage is normalized
once and the same value is used in the dodge check and the native call.

ResetEffects uses the entry of UpdateNPC_BuffSetFlags: the disassembly
confirms that UpdateNPC resets the flags inline right before that call.
Hooking UpdateNPC_BuffFlagsReset does not intercept the real tick, because the
compiler inlined its body into the caller. OnSpawn uses NewNPC's exit and the
source kept in the creation argument; NPC.OnSpawn's native body does not use
source, and its caller does not keep that argument for an external hook.

The combat points without an NPC as an argument, especially Damage_PVE, need
a global scope to identify the projectile. They are only installed if there is
a callback that depends on that flow; they are shared with ModPlayer when both
are present. They have a JS entry cost on attacks, even when the target is not
a mod NPC.

## Implementation

23 methods were added:

- OnSpawn, ResetEffects, OnHitByItem, OnHitByProjectile.
- CanHitPlayer, ModifyHitPlayer, OnHitPlayer, CanBeHitByItem,
  ModifyIncomingHit, CheckDead and ApplyDifficultyAndPlayerScaling.
- TownNPCProfile, CanChat, ModifyActiveShop and ModifyNPCHappiness.
- PreDraw, PostDraw, DrawEffects, DrawBehind, GetAlpha,
  BossHeadSlot, BossHeadRotation and BossHeadSpriteEffects.

The full parameters are in
[classes.md](../../../docs/referencia/classes.md#modnpc).
DrawEffects uses the center's lighting as its base, like Bunny Loader's
drawing API; the replaced color reaches the native buff tint. If the color
does not change, the specific lighting computed by the mobile renderer is
kept. PreDraw false skips the native drawing body and keeps PostDraw.
ModifyActiveShop serves shops registered through NPCShop and passes the local
name; the mobile array has empty items instead of null entries.
TownNPCProfile requires a native profile, since the bridge does not implement
C# interfaces from JavaScript objects.

After the user's pointer, the local ExMod v1.5.0's ModNPC.js, Hooks/NPC.js,
Hooks/GameContent.js and Loaders/NPCLoader.js were checked. Two alternatives
could be used with real data:

- ApplyDifficultyAndPlayerScaling runs after ScaleStats_ByPlayerCount. The
  count is the one the NPC uses, including overrides; balance comes from
  GetStatScalingFactors. The adjustment follows the tModLoader/ExMod
  convention: 0.85 when the NPC's own difficulty is Master or higher, 1 below.
- ModifyNPCHappiness runs after AllPersonalitiesModifier.ModifyShopPrice, with
  the player, the NPC, the helper and the neighbors array from the native
  HelperInfo. The primary biome uses ExMod's IDs and priority. It is not an
  active tModLoader stable API; it is the requested mobile alternative,
  described in the reference.

ExMod's UpdateLifeRegen alternative calls the callback after
UpdateNPC_BuffApplyDOTs and passes a number, not a mutable reference. It does
not allow controlling the damage already applied in that step; that is why it
was not adopted as equivalent to the requested contract.

## Dropped methods

| Method | Verified reason |
|---|---|
| CanBeHitByProjectile | The reachable eligibility point is Player.CanNPCBeHitByPlayerOrPlayerProjectile. Damage_PVE skips that point in some flows, including projectiles with no player owner. A veto in StrikeNPC would be late, after contact effects; it would not have the complete contract. |
| UpdateLifeRegen | UpdateNPC_BuffApplyDOTs accumulates losses in a local DOTTally structure and applies damage and effects inside the method itself. It does not expose the contract's damage reference before applying it. |
| ModifyDeathMessage | The message and the color are internal variables of checkDead. DropTombstoneTownNPC only exposes the tombstone text, without controlling every announcement or its color. |
| TownNPCAttackCooldown, TownNPCAttackShoot, TownNPCAttackSwing, TownNPCAttackMagic | The parameters are locals in the town NPC AI. There are no interceptable helpers with the complete cooldown, shot, hitbox or aura references. The earlier projectile hooks remain a documented flow of their own. |
| DrawTownAttackGun, DrawTownAttackSwing | The attack's texture, frame, scale and offsets live inside the renderer. There is no equivalent method with all of the mutable arguments. |
| DrawHealthBar | Main.DrawHealthBar receives coordinates, life and scale, without the NPC. The bar loop also serves players and worm segments; inferring the NPC from coordinates or life could modify the wrong entity. |

## Running

```powershell
node tools/tests/modnpchooks/check.mjs
node tools/tests/modplayerhooks/check.mjs
node tools/tests/globalprojectilehooks/check.mjs
node tools/tests/projectilekill/check.mjs
```

The first script loads the production code and validates every intercepted
signature, including parameter names, against the mobile dump.

```powershell
$env:JAVA_HOME = 'C:\Program Files\Android\Android Studio\jbr'
$env:CMAKE_BUILD_PARALLEL_LEVEL = '1'
gradle -I tools/tests/modnpchooks/isolated.gradle :app:assembleDebug --no-daemon --max-workers=1 '-Dorg.gradle.jvmargs=-Xmx1536m' '-Pkotlin.compiler.execution.strategy=in-process'
```

The APK lands in `build/modnpchooks/builds/app/outputs/apk/debug/`. The
fixture uses UID b19fb975-3058-47d5-8e59-fb737ca9e3e2. Copy only
manifest.json and content into that UID's folder in bunny_packs and enter a
single player world. The log ends with `modnpchooks FIM`. Then remove only that
package.

The fixture creates temporary NPCs and projectiles and deactivates them at
the end; it does not place blocks or grant items. The Hurt proof uses low
damage and restores the player's life and immunity in finally. The shop cases
use temporary storage and restore the chat target. The benchmark measures
direct calls to the native reset, including dispatch and DrawBehind tracking;
it does not measure FPS nor the JavaScript callback's exclusive cost.

## Sources

- [ModNPC.cs](https://github.com/tModLoader/tModLoader/blob/stable/patches/tModLoader/Terraria/ModLoader/ModNPC.cs).
- [NPCLoader.cs](https://github.com/tModLoader/tModLoader/blob/stable/patches/tModLoader/Terraria/ModLoader/NPCLoader.cs).
- [NPC.cs.patch](https://github.com/tModLoader/tModLoader/blob/stable/patches/tModLoader/Terraria/NPC.cs.patch).
- [Main.cs.patch](https://github.com/tModLoader/tModLoader/blob/stable/patches/tModLoader/Terraria/Main.cs.patch).
- `refs/dump.cs` and the disassembly of OnSpawn, UpdateNPC_BuffFlagsReset,
  StrikeNPC_Inner, Damage_PVE, checkDead, ScaleStats_ByPlayerCount,
  UpdateNPC_BuffApplyDOTs, DrawNPCDirect and CacheNPCDraws.
- Local ExMod v1.5.0 in `C:\Scripts\Terraria\curso\ExMod_v1.5.0\Modified\1.mod\TL`:
  ModNPC.js, Hooks/NPC.js, Hooks/GameContent.js and Loaders/NPCLoader.js.

The single player results, the review and the measurement limits are in
[RESULTADOS.md](RESULTADOS.md).
