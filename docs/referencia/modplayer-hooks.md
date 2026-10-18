# ModPlayer hooks

The signatures below belong to Bunny Loader. `player` is the first argument
and is the same as `this.Player`. The other parameters follow the roles of
[tModLoader's ModPlayer](https://docs.tmodloader.net/docs/stable/class_mod_player.html),
adapted to the IL2CPP Terraria integrated in the project.

The [ModPlayer catalog](metodos.md#modplayer) gathers all of the class's
members, including the update hooks and the static methods. The
[Excel spreadsheet](metodos.xlsx) and the [CSV](metodos.csv) let you filter by
`ModPlayer` and look up one row per signature.

Only overridden methods install hooks. Instances and data are separated by
player and class. A mod's exceptions are logged and do not interrupt the other
mods' callbacks.

## Returns and references

In boolean methods that allow an action, any `false` vetoes. In methods with
an optional decision, `null` keeps the game's rule, `true` allows and `false`
takes priority. Dodges and slot handlers stop at the first `true`. Positive,
finite multipliers are multiplied across mods.

Parameters marked `Ref<T>` are changed through `.value`. Other mutable
objects, like modifiers, items, fishing attempts and draw info, are changed
directly.

## Hits and damage taken

| Signature | Contract |
|---|---|
| `CanHitNPC(player, target)` | `true` by default; a general veto. |
| `CanHitNPCWithItem(player, item, target)` | `null` by default; an optional decision for the item. |
| `CanHitNPCWithProj(player, projectile, target)` | An optional decision for the projectile. |
| `CanHitPvp(player, item, target)` | `false` prevents the melee hit on another player. |
| `CanMeleeAttackCollideWithNPC(player, item, hitbox, target)` | An optional decision for the swing's intersection. |
| `ModifyHitNPC(player, target, modifiers)` | Before the native damage, followed by the source-specific callback. |
| `ModifyHitNPCWithItem(player, item, target, modifiers)` | Modifies the item hit. |
| `ModifyHitNPCWithProj(player, projectile, target, modifiers)` | Modifies the projectile hit. |
| `OnHitNPC(player, target, hit, damageDone)` | After a hit with positive damage. |
| `OnHitNPCWithItem(player, item, target, hit, damageDone)` | Item-specific notification. |
| `OnHitNPCWithProj(player, projectile, target, hit, damageDone)` | Projectile-specific notification. |
| `OnHitAnything(player, x, y, victim)` | After `Player.OnHit`. |
| `MeleeEffects(player, item, hitbox)` | During the use visuals. |
| `EmitEnchantmentVisualsAt(player, projectile, position, width, height)` | After the projectile's enchantment visuals. |
| `CanBeHitByNPC(player, npc, cooldownSlot)` | Veto; `cooldownSlot` is a `Ref<int>`. |
| `CanBeHitByProjectile(player, projectile)` | Veto on damage taken from a projectile. |
| `ModifyHitByNPC(player, npc, modifiers)` | Before `ModifyHurt`. |
| `ModifyHitByProjectile(player, projectile, modifiers)` | Before `ModifyHurt`. |
| `OnHitByNPC(player, npc, info)` | After positive damage caused by the NPC. |
| `OnHitByProjectile(player, projectile, info)` | After positive damage caused by the projectile. |
| `ConsumableDodge(player, info)` | `true` avoids the damage after `ModifyHurt` and `FreeDodge`. Only for the local player's dodgeable hits. |

The attack modifiers have `damage`, `knockBack`, `hitDirection`, `crit`,
`SourceDamage` and `Knockback` (`StatModifier`), plus `SetCrit()` and
`DisableCrit()`. The defense math stays in the game. `hit` has `Damage`,
`SourceDamage`, `Knockback`, `HitDirection` and `Crit`.

The damage-taken modifiers keep `ModifyHurt`'s existing contract: `damage`,
`hitDirection`, `quiet`, `crit` and `dodgeable`. `info` has `DamageSource`,
`Damage`, `HitDirection`, `PvP`, `Quiet`, `Crit`, `CooldownCounter` and
`Dodgeable`.

## Items, ammo, mana and healing

| Signature | Contract |
|---|---|
| `CanShoot(player, item)` | Vetoes the shot, keeping the use time. |
| `ModifyShootStats(player, item, position, velocity, type, damage, knockBack)` | The last five parameters are `Ref`s. |
| `Shoot(player, item, source, position, velocity, type, damage, knockBack)` | `false` prevents creating the projectile. |
| `CanConsumeAmmo(player, weapon, ammo)` | `false` keeps the ammo; not asked on `dontConsume` calls. |
| `OnConsumeAmmo(player, weapon, ammo)` | When the stack goes down; keeps the last ammo's type before `TurnToAir`. |
| `CanAutoReuseItem(player, item)` | An optional decision, keeping the item's permanent fields. |
| `ModifyWeaponCrit(player, item, crit)` | `crit` is a `Ref<int>`. |
| `ModifyWeaponKnockback(player, item, knockback)` | `knockback` is a `StatModifier`. |
| `ModifyItemScale(player, item, scale)` | `scale` is a `Ref<float>` of the multiplier. Acts on `GetAdjustedItemScale` and the native hitbox, together with ModItem. |
| `UseSpeedMultiplier(player, item)` | Divisor of the use and animation times; default `1`. |
| `UseTimeMultiplier(player, item)` | Multiplies the use time; default `1`. |
| `UseAnimationMultiplier(player, item)` | Multiplies the animation time; default `1`. |
| `ModifyManaCost(player, item, reduce, mult)` | `Ref<float>`: current cost × `(1 - reduce.value)` × `mult.value`. |
| `OnConsumeMana(player, item, manaConsumed)` | Notifies the mana consumed. |
| `OnMissingMana(player, item, neededMana)` | Before the native attempt to recover mana. |
| `PreItemCheck(player)` | `false` skips the item check. |
| `PostItemCheck(player)` | After the check, even when vetoed. |
| `GetHealLife(player, item, quickHeal, healValue)` | `healValue` is a `Ref<int>`; it also influences the quick heal choice. |
| `GetHealMana(player, item, quickHeal, healValue)` | `healValue` is a `Ref<int>`. |
| `ApplyPotionDelay(player, item, potionDelay)` | Receives the effective time, including rolls; `false` prevents the delay and the buff. |

Crit, knockback and times combine with `DamageClass` before the `ModPlayer`
modifiers. The resulting times are at least one frame.

`ModifyShootStats` and `Shoot` run for each native projectile creation within
a shot. A weapon that creates several projectiles may call them several
times. Projectiles created by these callbacks themselves go straight to the
game, avoiding recursion in the same shot.

## Updates, controls and jumps

| Signature | Execution point |
|---|---|
| `PreUpdateMovement(player)` | Before the frame's first collision and movement step. |
| `PostUpdateMiscEffects(player)` | After `CapAttackSpeeds`. |
| `PostUpdateRunSpeeds(player)` | Before `HorizontalMovement`. |
| `NaturalLifeRegen(player, regen)` | `Ref<float>` in the native calculation of natural regeneration. |
| `UpdateAutopause(player)` | After the local player's paused update. |
| `ProcessTriggers(player, triggersSet)` | After copying the controls to the local player. |
| `ResetInfoAccessories(player)` | After resetting effects and after updating the info accessories. |
| `ArmorSetBonusActivated(player)` | Double tap in the configured activation direction. |
| `ArmorSetBonusHeld(player, holdTime)` | Activation direction held down. |
| `OnEquipmentLoadoutSwitched(player, oldLoadoutIndex, loadoutIndex)` | After an actual switch. |
| `CanStartExtraJump(player, jump)` | Veto on the available extra jump. |
| `CanShowExtraJumpVisuals(player, jump)` | Veto on the active jump's native visuals. |
| `ExtraJumpVisuals(player, jump)` | Additional visuals for the allowed jump. |
| `ModifyExtraJumpDurationMultiplier(player, jump, duration)` | `duration` is a `Ref<float>`, initially `1`. |
| `OnExtraJumpStarted(player, jump, playSound)` | `playSound` is a `Ref<bool>`; `false` suppresses the start sound. |
| `OnExtraJumpEnded(player, jump)` | When the active jump ends. |
| `OnExtraJumpRefreshed(player, jump)` | After renewing the jumps available from equipment. |
| `OnExtraJumpCleared(player, jump)` | When a jump's availability is removed without enabling equipment. |

`ExtraJump` exports descriptors for the nine native jumps: `CloudInABottle`,
`SandstormInABottle`, `BlizzardInABottle`, `FartInAJar`, `TsunamiInABottle`,
`UnicornMount`, `SantankMount`, `GoatMount` and `BasiliskMount`. Compare the
descriptor's identity or read `jump.Name`. Registering new jump types is not
part of this set of hooks.

## Drawing and camera

| Signature | Contract |
|---|---|
| `DrawEffects(player, drawInfo, r, g, b, a, fullBright)` | `Ref<float>` multipliers and `Ref<bool>`; after the native preparation. |
| `DrawPlayer(player, camera)` | After the complete native drawing. |
| `HideDrawLayers(player, drawInfo)` | Call `PlayerDrawLayers.Head.Hide()`, for example, for that draw. |
| `ModifyDrawInfo(player, drawInfo)` | Changes the `PlayerDrawSet` before the layers are built. |
| `ModifyDrawLayerOrdering(player, positions)` | A `Map` from descriptor to `{ Before: descriptor }` or `{ After: descriptor }`. |
| `TransformDrawData(player, drawInfo)` | After the native transformations and before rendering the cache. |
| `ModifyScreenPosition(player)` | After updating the camera; change `Main.screenPosition`. |
| `ModifyZoom(player, zoom)` | `Ref<float>`; keeps the axes' aspect ratio. |

`PlayerDrawLayer.BeforeParent(layer)` and `AfterParent(layer)` create
positions. The ordering rearranges the data produced by the native layers,
keeping the game's position and equipment rules. Cycles are ignored with a
warning. The cache is `drawInfo.DrawDataCache`, with
`drawInfo.DrawDataCacheCount` active entries. Visibilities are restored at the
end of each draw.

Exported classes that extend `PlayerDrawLayer` are also registered
automatically. Implement `GetDefaultPosition()` and `Draw(drawInfo)`;
`GetDefaultVisibility(drawInfo)` is optional. See the
[contract and glowmask example](playerdrawlayers.md). Children follow the
parent's visibility and offset. The visibility query happens on each draw, and
the default positions are cached. Unchanged native layers stay out of the
JavaScript dispatch.

In the mobile game, `DrawDataCache` is a fixed-capacity array, not a list as
in [tModLoader](https://github.com/tModLoader/tModLoader/blob/stable/patches/tModLoader/Terraria/DataStructures/PlayerDrawSet.cs.patch).
Writing an entry without incrementing `DrawDataCacheCount` does not include it
in the drawing and lets the next layer overwrite it. Use
`ModPlayer.AddDrawData(drawInfo, drawData)`: it writes into the next active
slot and only increments the counter after the write. It returns `false` if
the cache is full, the counter is invalid or the cache or the data is
missing. It does not allocate another array or install hooks. The drawing
still goes through the native transformations, shaders and rendering.

```js
ModifyDrawInfo(player, drawInfo) {
    const Main = Terraria.Main;
    const drawData = Terraria.DataStructures.DrawData.new();
    drawData['void .ctor(Texture2D texture, Vector2 position, Rectangle sourceRect, Color color, float rotation, Vector2 origin, float scale, SpriteEffects effect, int inactiveLayerDepth)'](
        Terraria.GameContent.TextureAssets.MagicPixel.Value,
        Vector2.new(drawInfo.Position.X - Main.screenPosition.X - 24, drawInfo.Position.Y - Main.screenPosition.Y),
        Rectangle.new(0, 0, 1, 1), Color.new(255, 0, 255, 255),
        0, Vector2.new(0, 0), 16, 0, 0
    );
    ModPlayer.AddDrawData(drawInfo, drawData);
}
```

In `ModifyDrawInfo`, the entry comes before the data produced by the native
layers and may end up behind the player. In `TransformDrawData`, it goes in
after that callback's transformations; provide the desired final position.
`DrawData.Draw(Main.spriteBatch)` draws immediately and does not add an entry
to the cache. Native test: [player draw cache](../../tools/tests/playerdrawcache/README.md).

## Fishing, services and inventory

| Signature | Contract |
|---|---|
| `GetFishingLevel(player, fishingRod, bait, fishingLevel)` | `Ref<float>`; the items can be `null` when not found. |
| `ModifyFishingAttempt(player, attempt)` | Before the item roll; changes the attempt directly. |
| `ModifyCaughtFish(player, fish)` | Changes the actual item before handing it over. |
| `CanConsumeBait(player, bait)` | An optional decision after the native consumption roll. |
| `AnglerQuestReward(player, rareMultiplier, rewardItems)` | A mutable JS list of items before delivery. |
| `GetDyeTraderReward(player, rewardPool)` | A mutable JS list of IDs before the roll. An empty list produces an empty item. |
| `CanBuyItem(player, vendor, shopInventory, item)` | Veto before paying for the purchase. |
| `PostBuyItem(player, vendor, shopInventory, item)` | After a paid purchase; item received through the cursor. |
| `CanSellItem(player, vendor, shopInventory, item)` | Veto before the sale. |
| `PostSellItem(player, vendor, shopInventory, item)` | After an accepted sale. |
| `ModifyNurseHeal(player, nurse, health, removeDebuffs, chatText)` | Three `Ref`s; `false` prevents the service and uses the given text. |
| `ModifyNursePrice(player, nurse, health, removeDebuffs, price)` | `price` is a `Ref<int>`; also asked when showing the price. |
| `PostNurseHeal(player, nurse, health, removeDebuffs, price)` | After a paid service, including a zero price. |
| `CanCatchNPC(player, target, item)` | An optional decision for the catch. |
| `OnCatchNPC(player, npc, item, failed)` | A completed catch or an attempt that causes damage when trying to catch a lava critter. |
| `AddStartingItems(player, mediumCoreDeath)` | Returns a list of items; empty by default. |
| `ModifyStartingInventory(player, itemsByMod, mediumCoreDeath)` | A `Map` from UUID to item lists; native items under `'Terraria'`. |
| `AddMaterialsForCrafting(player, itemConsumedCallback)` | Returns items; put a `(item, index)` function in `itemConsumedCallback.value`. |
| `OnPickup(player, item)` | `false` removes the item from the ground without putting it in the inventory. |
| `HoverSlot(player, inventory, context, slot)` | `true` handles the hover and skips the native one. |
| `ShiftClickSlot(player, inventory, context, slot)` | With Shift active, `true` handles the click. |
| `CanBeTeleportedTo(player, teleportPosition, context)` | Veto on the native teleport; context `'TeleportRod'`, `'TeleportationPotion'`, `'Teleport'` or `'Wormhole'`. |

Crafting items are references to the original items, not copies. The
callback's index matches the list returned by the mod. Repeated sources are
deduplicated by address and split into native inventories of up to 200 slots.
The game uses these sources both for recipe availability and for consumption.
Starting items are assembled after the native inventory, before the character
save; on a mediumcore death, after the replacement inventory.

## Saving and network

| Signature | Contract |
|---|---|
| `PreSavePlayer(player)` | Before the native save. |
| `PostSavePlayer(player)` | When the native save and the auxiliary data finish, even if the native save throws. |
| `PreSaveCustomData(player)` | Before building the `.plr.bl.json` file's data. |
| `CopyClientState(player, targetCopy)` | Copies into another instance of the same class, tied to the native clone. |
| `SendClientChanges(player, clientPlayer)` | For the local player, compares with the instance copied on the previous frame. Runs on a client and on a phone hosting and playing (`netMode` 3). |
| `SyncPlayer(player, toWho, fromWho, newPlayer)` | After syncing the native player. On a joining client, once with `newPlayer` true when it enters the world (tModLoader sends it earlier, but the server rejects mod packets before that point). |
| `PlayerConnect(player)` | After the player connects. |
| `PlayerDisconnect(player)` | Before removing the disconnected player. |

`this.Mod` is the mod that owns the class, for `this.Mod.GetPacket()`. The
Example Mod's `ExampleStatIncreasePlayer` and `ExampleResourcePlayer` show the
full pattern (its `HandlePacket` is in `main.js`).

Mod state messages are still sent explicitly through `ModPacket`. There is no
automatic serialization of JS fields. Copy arrays and mutable objects in
`CopyClientState` so as not to share state with the clone. `newPlayer` means a
connection has not been observed yet for that index.

## Validation and performance

The [multiplayer test](../../tools/tests/mpmodplayer/RESULTADOS.md) uses a
real host and client and records 102 checks with no failures, including
disconnecting and reconnecting. The callbacks observed do not represent full
coverage of every method. A dedicated server and other specific flows remain
outside that run; see the [script](../../tools/tests/mpmodplayer/README.md).

The [performance review](../../tools/tests/modplayerperf/RESULTADOS.md)
measures frequent hooks with eight ModPlayers and preserves the number of
callbacks. Hooks are installed per group on demand. Instances, override plans
and lists of interested classes are reused; drawing and jumps use context
filters where applicable. The measurements belong to the scenario described,
without representing an FPS gain in any mod. The save hooks preserve data from
missing mods and keep the existing rule of an auxiliary file only for local
characters.

tModLoader declares `PreSaveCustomData`, but does not call it in the stable
code consulted. Here the hook has the explicit point described above.

## Verification

`node tools/tests/modplayerhooks/check.mjs` checks signatures against the
local dump and runs tests for composition, vetoes, references, reentry, draw
cache, services, crafting, clones and saving. It requires `refs/dump.cs`,
which is generated locally and not versioned.

The `tools/tests/modplayerhooks` package registers every hook and exercises
the native paths on a test character and world. The bridge for natural
regeneration, bait, dye rewards and fishing conditions checks the binary's
instructions before installing; an incompatible binary produces an error,
without applying the patch.
