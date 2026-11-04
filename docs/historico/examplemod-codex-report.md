# ExampleMod Codex port report

## Scope and validation

Worktree: `C:/Scripts/Bunny Loader-codex`. Branch: `codex/examplemod-tml`. Scope: the Codex C1-C5 entries in `examplemod-tml-split.md`. Source: local tModLoader ExampleMod and API, PC decompilation, then the mobile dump and generated proxies. No JavaScript sources, Gradle, adb, emulator, or game testing were used.

The unchanged baseline compiled with 0 errors (100 existing warnings). C1 compiled with 0 errors and 107 warnings using the required Release command. Compilation verifies type and member availability, not runtime IL2CPP dispatch, rendering, sound assets, touch interaction, or multiplayer behavior.

## C1 — weapons and projectiles

### API added

- `ModItem.CanCatchNPC` and `GlobalItem.CanCatchNPC` in their existing API files. `Core/ItemExampleHooks.cs` requests `PlayerWorldHooks.WantCatching`; dispatch joins the existing `Player.ItemCheck_CatchCritters` and `NPC.CatchNPC` hooks. A false decision vetoes capture; true permits capture outside the vanilla tool restriction. Mobile method names and signatures are confirmed in generated proxies. This reuses the existing ModPlayer catch path; failed-capture reporting remains its existing behavior.
- `ModItem.GetGolfClubProperties` dispatches at native `GolfHelper.GetClubProperties(short)` in `Core/ItemExampleHooks.cs`. Added full proxies for `GolfHelper` and its nested `ClubProperties`; the signature is present in the mobile dump.
- `ItemLoader.RegisterUseStyle` / `UseStyleCount` in `ItemLoader.UseStyles.cs`: allocates IDs after the mobile `ItemUseStyleID.Count`, rejects duplicate mod/name pairs, uses the existing item UseStyle hook. There is no new native item-use algorithm.
- `ModProjectile.PrepareBombToBlow` and `GlobalProjectile.PrepareBombToBlow` dispatch after `Projectile.PrepareBombToBlow` in `Core/ProjectileExampleHooks.cs`. The native method is present in the mobile proxies.
- `ModProjectile.DisplayDollSettings` and `GlobalProjectile.DisplayDollSettings` dispatch at `Projectile.AI_DisplayDoll`. Reset uses native `AI_DisplayDoll_Reset`; return false skips vanilla pose logic; otherwise type/style are borrowed and restored with `finally`. Both native methods are present in the mobile proxies. Exact placement inside the method differs from the tML IL patch and requires visual testing.
- Added proxy coverage for `TEDisplayDoll`, `TagDamageChanges`, and `TagEffectStack`. The advanced whip uses the existing native `On_WhipTagEffect` callbacks with per-effect pointer filtering because a managed class cannot inherit a proxy struct. Native `TagDamageChanges.TotalDamageMultiplier` replaces tML `NPC.HitModifiers.ScalingBonusDamage`; verify the resulting damage against the original.

- `ModItem.OnSpawn(WorldItem, IEntitySource)` / `GlobalItem.OnSpawn(WorldItem, IEntitySource)` in the API files, with `Core/ItemSpawnHooks.cs`: wraps native `Item.NewItem(..., NewItemModifier, ...)` so hooks run after the original modifier and before ownership/broadcast. A handwritten unmanaged trampoline calls `GameDelegates.Create`; no generated delegate number is fixed. `Item.NewItemModifier.Invoke` is implemented in `Item.NewItemModifier.TML.cs` using the native delegate Invoke signature. The method, callback and argument layout are confirmed in the mobile dump/proxies; runtime delegate dispatch still needs testing.
- `Item.RequestNewItem` convenience overload in `Item.RequestNewItem.TML.cs` supplies the mobile velocity/modifier arguments; forwarded native signature confirmed.
- `SoundEngine.TryGetActiveSound`, `SoundUpdateCallback` and a callback PlaySound overload use native tracked sounds in `Core/ActiveSoundHooks.cs`; callbacks run at `SoundEngine.Update` and stop an instance when returning false. Native loop conditions use a handwritten trampoline. `ActiveSound` and `ProjectileAudioTracker` receive full proxy coverage. The sample uses the native DD2 sound IDs corresponding to its two original Terraria sound paths. Android sound-pool custom sounds do not support tracked instances and callback playback rejects them explicitly.

- `UtilsExtensions.ExampleMod.cs` forwards MoveTowards, DirectionTo, DirectionFrom, ClosestPointInRect, ToPoint, AngleTowards and ToInt to existing native Utils members. `Utils.Geometry.TML.cs` implements CornerRectangle with the same endpoint bounds. `SetFactory.TML.cs` marshals managed params arrays into native CreateIntSet; full SetFactory proxy coverage is enabled.
- `ItemID.Sets.CatchingTool` and `IsAGolfClub` in `ItemID.ExampleMod.Sets.cs`. Catching tools dispatch through `Player.ItemCheck_GetMeleeHitbox` into the native catch method; golf classification extends native `Item.IsAGolfingItem`. Native catcher checks are hard-coded to vanilla tool IDs, so the melee-hitbox adapter is required for the new tool.
- `ProjectileID.Sets.Explosive` in `ProjectileID.Sets.TML.cs` includes source vanilla defaults available in the mobile IDs. `ProjectileLoader.SetDefaults` represents a mod's explicit explosive flag with native aiStyle 16 when its original style is 0; ProjectileHooks suppresses that marker's vanilla AI while retaining the custom AI. Non-AI explosive branches continue to see style 16. This adaptation follows the PC branch logic; exact mobile branch bodies are not available in the dump and require runtime testing.
- `Player.ItemUsesThisAnimation`, `ItemAnimationEndingOrEnded`, HasAmmo and PickAmmo convenience overloads in `Player.ItemUse.TML.cs`. `Core/PlayerItemUseState.cs` counts outermost ApplyItemTime calls and resets at ApplyItemAnimation. The native signatures are confirmed; verify three-shot timing with speed modifiers.
- `Item.StopAnimationOnHurt` in `Item.State.TML.cs` is stored with weak per-item state, copied at native Item.Clone, and applied in the existing PlayerHurtHooks after positive damage. It cancels channel/item animation as in the tML hurt patch.
- `Item.NetStateChanged` explicitly syncs a matching local inventory slot or a world item through existing item network envelopes. Detached items and equipment/bank slots are not currently covered by this convenience method; the C1 showcase is an inventory item.
- Projectile GetSource_DropAsItem/GetSource_Death compatibility names forward to the mobile source methods in `Projectile.Sources.TML.cs`. The managed four-argument KillOldestJavelin retains the PC scan, cap and oldest-timeLeft selection instead of guessing the semantics of the mobile method's extra List argument.
- `Player.GetSource_ItemUse_WithPotentialAmmo` forwards to the existing native source method. Full PlayerInput and UniqueTagEffect proxies supply controller state and inherited whip proc callbacks.

- `DictionaryIntArray.TML.cs` exposes the native indexed get_Item/set_Item methods, preserving lazy native dictionary allocation for local NPC immunity. Both signatures are confirmed in the dump.

### To test in game

| Examples | Checks |
| --- | --- |
| ExampleBugNet / ExampleCatchItemModification | Normal, lava and Warmth-assisted catches; duplicate count and caught-source identity; host/client authority. |
| ExampleGolfClub | Driver trajectory and rough resistance; research grouping and weapon rack. |
| ExampleInteractableProjectileItem / ExampleInteractableProjectile | Five-frame animation, owner-only duplicate removal, selection outline, touch/smart interaction, duck/item sounds, pause and reverse gravity. |
| ExampleMagicMirror | Mid-animation recall, hook removal, recall dust, cycling name colors. |
| ExampleAdvancedFlail / ExampleAdvancedFlailProjectile | All seven original AI states, held attack/release, collision recoil, retract limits, chain textures and trail, jellyfish and display doll. |
| ExampleCloneWeapon / ExampleCloneProjectile | Meowmere defaults, extra penetration and four death projectiles; source propagation. |
| ExampleCustomAmmoGun / ExampleHomingProjectile | Custom ammo selection after C2, 10-tick homing delay, target death/loss, wall occlusion, turn rate and network state. |
| ExampleCustomSwingSword / ExampleCustomSwingProjectile | Alternating swing/spin, combo timeout, attack speed, arm pose, line collision/tile cutting, reverse gravity and display doll. |
| ExampleCustomUseStyleWeapon / ExampleCustomUseStyleGlobalItem | Custom style on the weapon and Cutlass; arm, hitbox and held frame; pose registration; remote swing angle pending the networking half. |
| ExampleHeldProjectileWeapon / ExampleHeldProjectileWeaponProjectile | Startup ammo exception, accelerated cadence, paired arrows and every-fourth bee arrow, stop on no ammo/release, world glow and display doll. |
| ExampleJavelin / ExampleJavelinProjectile | Gravity delay, sticking, six-javelin cap, 15-second expiry, NPC draw layers, occasional recovery and C2/C4 damage over time. |
| ExampleJoustingLance / ExampleJoustingLanceProjectile | Channeled extension/retraction, movement-scaled damage, dismounting, dust and doll pose. |
| ExampleShootingSword | Three falling stars, cursor/ceiling targeting and touch aiming. |
| ExampleSpecificAmmoGun | Three-shot cadence, cursed-bullet veto, per-shot conservation and consumed-ammo damage boost. |
| ExampleStaff | Staff rotation, magic stats and SparklingBall. |
| ExampleWhipAdvanced / ExampleWhipProjectileAdvanced | Charge-dependent length, collision points and line rendering, minion targeting, native tag/proc damage, particles, once-per-whip proc and C2 player buff. |
| HitModifiersShowcase | Eight modes, right-click/touch alternate use, PvP modifiers and serialized mode; dodge mode depends on C2/C3. |
| ActiveSoundShowcaseProjectile | Six modes, sound follows projectile, loop lifetime, wet/penetration pitch, bounce sounds and pause. Launcher item arrives in C5. |
| ExampleArrowProjectile | Delayed gravity, terminal velocity, rotation, impact dust/sound. |
| ExampleExplosive | Fuse, bounce, 250-pixel explosion, five children, wall/tile destruction, source/ownership, custom BananaImpact sound. Launcher item belongs to the other session. |
| ExampleGravityDebuffBullet | Trail, penetration, AI clone, impact sound, C2 gravity debuff. |
| ExamplePaperAirplaneProjectile | Wind, turns, wet death, sprite orientation, granite-golem multiplier and C5 recovery item. |
| ExamplePiercingProjectile / ExamplePiercingProjectileItem | No-ammo pistol, infinite penetration, melee damage class, 60-tick life. |
| ExampleGrenadeProjectile | Bounce and fuse, enlarged blast, dust/gore and native specialist damage behavior. |
| ExampleProximityMineProjectile | Settling, transparency, blast and native stationary triple damage/proximity triggering. |
| ExampleRocketProjectile | Acceleration cap, impact arming and blast radius/direct damage. |
| ExampleSnowmanRocketProjectile | Delayed homing, visibility ramp, target selection/obstruction, blast and player damage exclusion. |

### Left out

- ExampleBugNet omits the tML-only DuplicationMenuToolsFilter metadata; the current loader uses the native research groups, without that PC filter extension.
- Projectile death sources retain the native parent identity; a tML-only death-source runtime type is not fabricated.
- ActiveSoundShowcaseProjectile's tML `PauseBehavior` settings: the current SoundStyle/ActiveSound implementation has no such option. Existing sound engine pause behavior is retained; the missing per-style pause control must be observed during testing.
- Mobile capture sources are `EntitySource_Parent`, not the tML-only `EntitySource_Caught`. The duplicate-critter example uses a scoped `NPC.CatchNPC` hook and checks the parent player identity during OnSpawn. The caught NPC object is not exposed as a fabricated source type.
- Custom-use-style explicit message sending and server relay depend on `ExampleMod.Networking.cs`, owned by the other session. No changes were made to `ExampleMod.cs` and no unhandled packet is emitted.
- `ReinitializeDuringResizeArrays` on the sample's custom hand-offset set is replaced by initialization in GlobalItem.SetStaticDefaults, after item IDs and arrays exist. The loader does not currently implement that tML attribute.

### Unresolved references

- C2 ammo/buffs and C5 paper-airplane drop are looked up by full mod/name until those blocks arrive. If absent, only the dependency is skipped. Restore direct generic references as the owning Codex blocks are completed.
- HitModifiersShowcase mode 7's dodge application awaits the C3 ExampleDamageModificationPlayer and C2 ExampleDodgeBuff.
- The other session's networking message enum and dispatch for custom use style must be merged before restoring that explicit direction synchronization.


## C2 — ammo and buffs

Validation: required Release build passed with 0 errors and 108 warnings.

### API added

- `NPC.GravityMultiplier` in `NPC.Gravity.TML.cs`, backed by weak per-NPC state in `Core/NPCGravityHooks.cs`. State resets at `NPC.UpdateNPC_BuffFlagsReset`, before buff updates; `NPC.UpdateNPC_UpdateGravity(out float)` applies the multiplier after the mobile game resets its static NPC.gravity. These native points are confirmed in the proxies and dump. The PC/tML source confirms why buff-time gravity must survive the later native reset. This adapter currently covers modifications made before UpdateNPC_UpdateGravity; later AI modifications require a separate application point.
- `Player.MeleeEnchantActive` in `Player.MeleeEnchant.TML.cs` maps onto the existing native byte meleeEnchant (255 for a custom enchantment, 0 to clear). The field is present in the mobile proxy.
- `Player.SetCCed` in `Player.CrowdControl.TML.cs` adds a per-player crowd-control flag. `Core/PlayerCrowdControlHooks.cs` resets it at native ResetEffects and extends native get_CCed without replacing vanilla control conditions. Both native hooks are confirmed in the proxy.
- `Condition.NpcIsPresent` in `Condition.cs` follows the upstream localization/name/predicate implementation. The sample provides the condition text in new global Conditions localization files. Native NPC.AnyNPCs and Lang.GetNPCName are existing proxies.
- C1's temporary ammo, gravity-bullet, javelin-buff and whip-buff lookups are replaced with direct generic references now that those types exist.

### To test in game

| Examples | Checks |
| --- | --- |
| ExampleArrow | Research count, arrow ammo selection, projectile speed/damage and workbench recipe. |
| ExampleCustomAmmo | Custom ammo category equals its own item type; custom gun consumes only this ammo and fires homing projectiles. |
| ExampleGravityDebuffBullet | Bullet selection, penetration/trail and gravity effect on hit NPCs. |
| ExampleRocket | Five launcher mappings, specialist damage, conservation, recipes and Cyborg condition. |
| AbsorbTeamDamageBuff | Hidden duration/save flags; C3 player-state linkage and teammate damage redirection. |
| AnimatedBuff | Four-frame icon, one-second animation step and 10% generic damage increase. |
| ExampleCrowdControlledDebuff | Suppression of movement/items, pulley and mount cancellation, forced gravity, tint and touch controls. |
| ExampleDodgeBuff | C3 dodge flag, consumption, cooldown, held-weapon restriction and visuals. |
| ExampleGravityDebuff | Gravity varies by cosine; native gravity reset does not erase it or leak into another NPC. The source Counter is shared by the singleton buff, as upstream. |
| ExampleJavelinDebuff | Bone-javelin immunity inheritance; C4 sticking-javelin damage-over-time linkage. |
| ExampleWeaponImbue | Flask exclusion/persistence flags, native melee enchantment marker and C3 hit/enchantment effects. |
| ExampleWhipAdvancedPlayerBuff | 12% summon-melee attack-speed bonus and native whip tag application. |

### Left out

No C2 game behavior has been substituted with an invented native member. Player/global-NPC flags owned by later Codex blocks are temporarily omitted below and must be restored with those blocks.

### Unresolved references

- AbsorbTeamDamageBuff's description currently uses the upstream constant 30 and its Update awaits ExampleDamageModificationPlayer in C3.
- ExampleDodgeBuff.Update awaits ExampleDamageModificationPlayer in C3.
- ExampleWeaponImbue's custom state awaits ExampleWeaponEnchantmentPlayer in C3; the native enchantment marker is already active.
- ExampleJavelinDebuff.Update awaits DamageOverTimeGlobalNPC in C4.
