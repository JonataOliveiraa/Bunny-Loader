# ModItem combat

This is the first step of the [expansion plan](../../tools/tests/moditemhooks/PLANO.md). Bunny Loader's API receives `item` explicitly as the first argument. The mutable references are `Ref` objects, changed through `.value`.

| Method | Contract | Native point |
| --- | --- | --- |
| `ModifyWeaponDamage(item, player, damage)` | `damage` is a `StatModifier`. A numeric return is also accepted. | `Player.GetWeaponDamage` |
| `ModifyWeaponCrit(item, player, crit)` | `crit` is a `Ref<number>`; the result becomes an integer. | `Player.GetWeaponCrit` |
| `ModifyWeaponKnockback(item, player, knockback)` | `knockback` is a `StatModifier`; a negative or non-finite result becomes zero. | `Player.GetWeaponKnockback` |
| `ModifyItemScale(item, player, scale)` | `scale` is a `Ref<number>` of the multiplier. It includes the native glove. Do not change `Item.scale` in this callback. | `Player.GetAdjustedItemScale` and `ItemCheck_GetMeleeHitbox` |
| `CanHitNPC(item, player, target)` | `false` vetoes, `true` allows eligibility, `null`/`undefined` keeps the game's. Collision is still checked. | `Player.ProcessHitAgainstNPC` and `CanNPCBeHitByPlayerOrPlayerProjectile` |
| `CanMeleeAttackCollideWithNPC(item, player, hitbox, target)` | `false` vetoes, `true` allows the hitbox to collide with the target, `null`/`undefined` keeps the game's. | `Player.ProcessHitAgainstNPC` and `Rectangle.Intersects` |
| `ModifyHitNPC(item, player, target, modifiers)` | Modifies a melee attack before `StrikeNPC`. | `NPC.StrikeNPC`, in the item's context |
| `CanHitPvp(item, player, target)` | On the attacker; `false` vetoes before applying status and sending damage. | `Player.CanHit`, during `ItemCheck_MeleeHitPVP` |
| `ModifyHitPvp(item, player, target, modifiers)` | On the victim; an object with `damage`, `hitDirection`, `quiet`, `crit`, `dodgeable`. | `Player.Hurt`, including when received over the network |
| `OnHitPvp(item, player, target, hurtInfo)` | Only after positive damage. Receives the data actually applied. | `Player.Hurt`'s return |
| `MeleeEffects(item, player, hitbox)` | Effects after the native use visuals. | `Player.ItemCheck_EmitUseVisuals` |
| `UseItemHitbox(item, player, hitbox, noHitbox)` | `hitbox` is a `Ref<Rectangle>` and `noHitbox` a `Ref<boolean>`. Runs after the native hitbox. | `Player.ItemCheck_GetMeleeHitbox` |

`ModifyHitNPC` receives `damage`, `knockBack`, `hitDirection`, `crit`, `SourceDamage` and `Knockback`. The last two are `StatModifier`s. It can also call `SetCrit()` and `DisableCrit()`. The loader applies the modifiers and clamps damage and knockback to finite non-negative values. Hits received with `fromNet` do not reapply these modifiers.

`hurtInfo` has `DamageSource`, `Damage`, `HitDirection`, `PvP`, `Quiet`, `Crit`, `CooldownCounter` and `Dodgeable`. `Damage` is the native `Hurt`'s positive return. Dodges and vetoes produce no `OnHitPvp`. `ModifyHitPvp` and `OnHitPvp` run on the victim. The complementary packet keeps the weapon's NetSend/NetReceive, even when the attacker switches items before reception. Without the complement, the source is recovered from the PlayerDeathReason's type and prefix; custom data requires the complement sent by Bunny. The [two-process validation](../../tools/tests/pendingnetwork/README.md) passed both directions of the attack.

The modifier objects are Bunny Loader JavaScript contracts. They do not expose every field of tModLoader's `NPC.HitModifiers` or `Player.HurtModifiers`. The current signature is `OnHitNPC(item, player, npc, hit, damageDone)`, with `HitInfo` and the actual damage; `ModifyHitNPC` uses `HitModifiers` and `ModifyHitPvp` uses `HurtModifiers`. See the fields in [Hits](../mods/16-golpes.md).

## Order and vetoes

Weapon damage goes through ModItem, GlobalItem and ModPlayer, using a single shared hook. GlobalItem/ModPlayer's numeric returns and the legacy `ModPlayer.WeaponDamage` assignment remain valid. Crit, knockback, scale and hit modifiers go through ModItem before ModPlayer. Any ModPlayer or ModNPC veto beats a permission from the item.

## Example

```js
ModifyWeaponDamage(item, player, damage) {
    damage.Additive += 0.15;
}

ModifyWeaponCrit(item, player, crit) {
    crit.value += 5;
}

ModifyItemScale(item, player, scale) {
    scale.value *= 1.2;
}

ModifyHitNPC(item, player, target, modifiers) {
    modifiers.SourceDamage.Multiplicative *= 1.1;
}

UseItemHitbox(item, player, hitbox, noHitbox) {
    const box = hitbox.value;
    hitbox.value = Rectangle.new(box.X - 8, box.Y - 8, box.Width + 16, box.Height + 16);
}
```

## Performance and validation

Hooks with an item argument use per-type marks evaluated before entering JavaScript. Vanilla items and mod items without the matching override go straight to the original, as long as there is no global observer of that flow. ModPlayer, GlobalItem and DamageClass widen only the marks needed on registration. Mod types registered later receive those marks.

Inherited overrides and error labels are prepared per class. `Rectangle.Intersects` uses `whileIn` and a flag: it only enters JavaScript when the collision was forced during the attack. The contexts and the temporary scale changes are restored in `finally`.

See the [tests and limits of this step](../../tools/tests/moditemhooks/README.md). The contracts were checked against [tModLoader's ModItem](https://github.com/tModLoader/tModLoader/blob/stable/patches/tModLoader/Terraria/ModLoader/ModItem.cs), the mobile dump and the disassembly of the methods used.
