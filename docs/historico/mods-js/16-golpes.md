# Hits: landing, changing the damage and reacting

Every hit in the game goes through the same three moments, as in tModLoader:

1. **CanHit**: anyone can veto (`false`) or force (`true`) the hit;
2. **ModifyHit**: the hit becomes a `HitModifiers` (on an NPC) or a
   `HurtModifiers` (on a player), which each one adjusts;
3. **OnHit**: after the damage, with the result (`HitInfo` or `HurtInfo`).

The methods have tModLoader 1.4.4's names and order and apply the same way to
mod items, projectiles and NPCs (`ModItem`, `ModProjectile`, `ModNPC`), to
Globals (`GlobalItem`, `GlobalProjectile`, `GlobalNPC`, also on vanilla
content) and to `ModPlayer`. `CombatLoader` ties it all together.

## Who is called on each hit

**Melee item on an NPC** (the player's sword):

| | Item | NPC hit | Player |
|---|---|---|---|
| CanHit | `CanHitNPC(item, player, target)` | `CanBeHitByItem(npc, player, item)` | `CanHitNPC`, `CanHitNPCWithItem` |
| ModifyHit | `ModifyHitNPC(item, player, target, modifiers)` | `ModifyHitByItem(npc, player, item, modifiers)` | `ModifyHitNPCWithItem`, `ModifyHitNPC` |
| OnHit | `OnHitNPC(item, player, target, hit, damageDone)` | `OnHitByItem(npc, player, item, hit, damageDone)` | `OnHitNPCWithItem`, `OnHitNPC` |

**Projectile on an NPC**:

| | Projectile | NPC hit | Owner (player) |
|---|---|---|---|
| CanHit | `CanHitNPC(proj, target)` | `CanBeHitByProjectile(npc, proj)` | `CanHitNPCWithProj` |
| ModifyHit | `ModifyHitNPC(proj, target, modifiers)` | `ModifyHitByProjectile(npc, proj, modifiers)` | `ModifyHitNPCWithProj`, `ModifyHitNPC` |
| OnHit | `OnHitNPC(proj, target, hit, damageDone)` | `OnHitByProjectile(npc, proj, hit, damageDone)` | `OnHitNPCWithProj`, `OnHitNPC` |

**NPC on NPC** (the enemy that touches a town NPC):

| | Attacker | Target |
|---|---|---|
| CanHit | `CanHitNPC(npc, target)` | `CanBeHitByNPC(npc, attacker)` |
| ModifyHit | `ModifyHitNPC(npc, target, modifiers)` | |
| OnHit | `OnHitNPC(npc, target, hit)` | |

On every hit on an NPC, last: the hit NPC's `ModifyIncomingHit(npc, modifiers)`
(also on hits with no source: traps, explosions).

**On the player** (NPC contact, hostile projectile, PvP):

| | NPC | Hostile projectile | PvP with item | PvP with projectile | Player hit |
|---|---|---|---|---|---|
| CanHit | `CanHitPlayer(npc, target, cooldownSlot)` | `CanHitPlayer(proj, target)` | `CanHitPvp(item, player, target)` and `ModPlayer.CanHitPvp` | `CanHitPvp(proj, target)` and `ModPlayer.CanHitPvpWithProj` | `CanBeHitByNPC`, `CanBeHitByProjectile`, `ImmuneTo` |
| ModifyHit | `ModifyHitPlayer(npc, target, modifiers)` | `ModifyHitPlayer(proj, target, modifiers)` | `ModifyHitPvp(item, player, target, modifiers)` | `ModifyHitPlayer(proj, ...)` | `ModifyHitByNPC`, `ModifyHitByProjectile`, `ModifyHurt` |
| OnHit | `OnHitPlayer(npc, target, hurtInfo)` | `OnHitPlayer(proj, target, info)` | `OnHitPvp(item, player, target, hurtInfo)` | `OnHitPlayer(proj, ...)` | `OnHitByNPC`, `OnHitByProjectile`, `OnHurt`, `PostHurt` |

The `Can` methods that return `null` (tModLoader's "bool?" ones) let the game
decide: a `false` from anyone vetoes; otherwise a `true` forces it. The ones
that return `true` by default (`CanHitPlayer`, `CanHitPvp`, NPC-on-NPC
`CanHitNPC`) only veto.

## HitModifiers: the hit on an NPC

```js
ModifyHitNPC(item, player, target, modifiers) {
    modifiers.SourceDamage.Multiplicative *= 1.5;   // before defense
    modifiers.ArmorPenetration += 10;
    if (target.boss) modifiers.FinalDamage.Flat += 20;   // after defense and crit
    modifiers.SetCrit();
}
```

The math is tModLoader's: `SourceDamage` on the raw damage, plus
`FlatBonusDamage`, `ScalingBonusDamage` and `TargetDamageMultiplier`; defense
is subtracted (`Defense`, `ArmorPenetration`, `ScalingArmorPenetration`,
`DefenseEffectiveness`, 0.5 by default, as in the game); `CritDamage` (×2 by
default) or `NonCritDamage` is applied; and finally `FinalDamage`. Also:
`SetMaxDamage(n)`, `SetInstantKill()`, `DisableCrit()`, `SetCrit()`,
`DisableKnockback()`, `HideCombatText()`, `Knockback` (`StatModifier`) and
`HitDirectionOverride`.

C#'s `AddableFloat` and `MultipliableFloat` fields are numbers here:
`modifiers.ArmorPenetration += 5`, `modifiers.TargetDamageMultiplier *= 2`.
The older names (`modifiers.damage`, `knockBack`, `hitDirection`, `crit`)
still work.

The `hit` in `OnHit` is a `HitInfo`: `Damage` (what the NPC took),
`SourceDamage`, `Crit`, `Knockback`, `HitDirection`, `InstantKill`,
`HideCombatText`.

**Multiplayer:** the mobile game sends the server the hit's raw damage
(message 28), and the server redoes the defense math. The loader converts the
final damage into a raw value that, through the game's math, gives the same
number; with odd defense and a crit, 1 point may be left over.

## HurtModifiers: the hit on the player

```js
ModifyHurt(player, modifiers) {
    modifiers.SourceDamage.Multiplicative *= 0.8;
    if (modifiers.PvP) modifiers.SetMaxDamage(50);
}
```

`SourceDamage` and `IncomingDamageMultiplier` before defense;
`ArmorPenetration` and `ScalingArmorPenetration` subtract defense;
`FinalDamage` and `SetMaxDamage` after it. `Cancel()` undoes the hit;
`DamageSource`, `PvP`, `CooldownCounter`, `Dodgeable` and
`HitDirectionOverride` as in tModLoader. The mobile game has no variable
knockback on the player: `Knockback` is only there to be read. The older names
(`damage`, `hitDirection`, `quiet`, `crit`, `dodgeable`) still work.

The `HurtInfo` in the `OnHit`s: `Damage`, `SourceDamage`, `DamageSource`,
`PvP`, `CooldownCounter`, `Dodgeable`, `HitDirection`, `Crit`, `Quiet`.

## Limits

- The projectile's `CanHitNPC` and `CanBeHitByProjectile` apply to the
  player's projectiles (the game only asks about those); a town NPC's
  projectile hits without them, but goes through `ModifyHit` and `OnHit`.
- `true` in a projectile's `CanHitNPC` does not make it hit a friendly NPC
  (the game decides that before asking); in the item's, it does.
- Cost: each hit enters JS once per hook. The item and projectile ones are
  only installed when some class writes one of these methods; the player's
  (`Player.Hurt`) only with a method of the player being hit.
