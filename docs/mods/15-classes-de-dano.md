# 15. Damage classes (`DamageClass`)

The damage class says three things about an attack:

1. **which bonuses it benefits from**: the "+10% melee damage" accessory
   applies to the sword, not to the staff;
2. **which effects it triggers**: the Magma Stone sets melee hits on fire, the
   Spectre armor shoots bolts on magic attacks;
3. **which prefixes it accepts**: "Legendary" is for melee weapons, "Mythical"
   for magic weapons.

In the game these are four fixed flags on the item (`melee`, `ranged`,
`magic`, `summon`) and one field per class on the player (`meleeDamage`,
`meleeCrit`...). There is no room for a fifth. tModLoader's `DamageClass`
replaces that with objects: the vanilla classes become ready-made objects, and
a mod creates new classes (a "rogue", a "bard") with the same power as the
game's, which other mods can see too.

## Using a vanilla class

```js
SetDefaults() {
    this.Item.DamageType = DamageClass.Magic;
}
```

| Class | What it is |
|---|---|
| `DamageClass.Default` | No class: gets no bonus at all (trap damage, thrown water). |
| `DamageClass.Generic` | The base of all of them. A "+10% to all damage" goes here, and every class that inherits from it gets it. |
| `DamageClass.Melee` | Melee. |
| `DamageClass.MeleeNoSpeed` | Melee that does not get faster with attack speed (spears). |
| `DamageClass.Ranged` | Ranged. |
| `DamageClass.Magic` | Magic. |
| `DamageClass.Summon` | Summon (minions, sentries): no crit. |
| `DamageClass.SummonMeleeSpeed` | Whips: summon that gets faster with melee speed. |
| `DamageClass.MagicSummonHybrid` | Magic and summon at the same time. |
| `DamageClass.Throwing` | Throwing: no vanilla item uses it; it exists for mods to share. |

`item.melee = true`, the old way, still works: it is the same as
`DamageClass.Melee`. Projectiles have `proj.DamageType` too.

## Giving per-class bonuses

On an accessory, armor, buff or `ModPlayer`, in the equipment hooks
(`UpdateAccessory`, `UpdateEquip`, `PostUpdateEquips`, the buff's `Update`):

```js
UpdateAccessory(item, player, vanity, hideVisual) {
    player.GetDamage(DamageClass.Generic).Additive += 0.10;  // +10% to everything
    player.GetCritChance(DamageClass.Melee).value += 5;      // +5% melee crit
    player.GetAttackSpeed(DamageClass.Ranged).value += 0.15; // shoots 15% faster
    player.GetArmorPenetration(DamageClass.Magic).value += 5;
    player.GetKnockback(DamageClass.Summon).Additive += 0.5;
}
```

The bonuses apply on the frame they are set, like the game's: on the next
frame they start from zero and the accessory adds them again.

### Damage is a `StatModifier`

```
final damage = (weapon damage + Base) × Additive × Multiplicative + Flat
```

| Field | What for | Example |
|---|---|---|
| `Additive` | The "+X%" ones: they add up (two +10% give +20%, not +21%). | `.Additive += 0.25` |
| `Multiplicative` | The "×X" ones: they multiply on top of everything. | `.Multiplicative *= 1.12` |
| `Base` | Added to the weapon's damage, before the percentages. | `.Base += 4` |
| `Flat` | Added at the end, after everything. | `.Flat += 5` |

Knockback (`GetKnockback`) is a `StatModifier` too. Crit, speed and armor
penetration are numbers: `.value`.

In tModLoader's C# it is `player.GetDamage(x) += 0.1f`. JavaScript has no such
operator for objects, so here you add to the field: `.Additive += 0.1`.

### The totals

To know how much the player has in a class, inheritance included:

| Method | Returns |
|---|---|
| `player.GetTotalDamage(class)` | `StatModifier` (use `.ApplyTo(damage)`) |
| `player.GetTotalCritChance(class)` | number |
| `player.GetTotalAttackSpeed(class)` | number (1 = normal) |
| `player.GetTotalArmorPenetration(class)` | number |
| `player.GetTotalKnockback(class)` | `StatModifier` |
| `player.GetWeaponAttackSpeed(item)`, `player.GetWeaponArmorPenetration(item)` | the weapon's |

`player.GetArmorPenetration(true)`, with a boolean, is still the game's.

## Creating a new class

```js
export class RogueDamageClass extends DamageClass {
    // Whose bonuses it inherits: all of Generic and half of Throwing.
    GetModifierInheritance(damageClass) {
        if (damageClass === DamageClass.Generic) return StatInheritanceData.Full;
        if (damageClass === DamageClass.Throwing) return new StatInheritanceData({ damageInheritance: 0.5 });
        return StatInheritanceData.None;
    }

    // What it "counts as" for the game's effects: the Magma Stone works.
    GetEffectInheritance(damageClass) {
        return damageClass === DamageClass.Melee;
    }

    // Bonuses it always has, every frame.
    SetDefaultStats(player) {
        player.GetCritChance(this).value += 4;
    }
}
```

It registers on its own, like any exported class. The weapon:

```js
import { RogueDamageClass } from '../DamageClasses/RogueDamageClass.js';

SetDefaults() {
    this.Item.DamageType = ModContent.GetInstance(RogueDamageClass);
}
```

And an accessory (from this mod or another) gives it a bonus:
`player.GetDamage(RogueDamageClass).Additive += 0.2`.

### What the class can decide

| Method | What it decides | Default |
|---|---|---|
| `GetModifierInheritance(other)` | How much it inherits from the other's bonuses: a `StatInheritanceData` with damage, crit, speed, penetration and knockback (1 = all, 0 = none). | All of Generic, none of the others. |
| `GetEffectInheritance(other)` | `true`: counts as the other for the game's effects. | None. |
| `GetPrefixInheritance(other)` | `true`: accepts the other's prefixes. | The same as the effects. |
| `SetDefaultStats(player)` | The bonuses it always has. | None. |
| `get UseStandardCritCalcs()` | `false`: no crit (and no crit line in the tooltip). | `true` |
| `ShowStatTooltipLine(player, line)` | `false` hides the `'Damage'`, `'CritChance'`, `'Speed'` or `'Knockback'` line. | All show. |

`StatInheritanceData.Full` inherits everything and `StatInheritanceData.None`
nothing. To inherit only part of it, use the constructor with the names:
`new StatInheritanceData({ damageInheritance: 1, attackSpeedInheritance: 0.5 })`.

`class.CountsAsClass(DamageClass.Melee)` and `item.CountsAsClass(DamageClass.Melee)`
ask whether it counts as melee.

### The name in the tooltip

![The example weapon's tooltip: "70 example damage", the class's crit and, hidden by the class, no speed line](../imagens/classe-de-dano-tooltip.jpg)

The tooltip shows "70 rogue damage". The text comes from the mod's
`Localization/<culture>.json` and includes the word "damage":

```json
{
  "DamageClasses": {
    "RogueDamageClass": { "DisplayName": "rogue damage" }
  }
}
```

Without it, the class name comes out split into words ("Rogue Damage Class").
A `DisplayName = 'rogue damage'` written on the class also works.

## Changing a vanilla item's class

In a `GlobalItem`:

```js
SetDefaults(item) {
    if (item.type === ItemID.WoodenSword) item.DamageType = ModContent.GetInstance(RogueDamageClass);
}
```

The class written in `SetDefaults` applies to the whole type: the item keeps
it when it is copied, dropped on the ground or sent over the network.
Changing an item's `DamageType` outside `SetDefaults`, with the game running,
only applies to that item, and is lost when the game copies it.

## `ModifyWeaponDamage`

In `ModPlayer` and `GlobalItem`, damage arrives in a way that serves both
styles:

```js
// The old way: return the new damage.
ModifyWeaponDamage(player, item, damage) { return damage * 2; }

// tModLoader's way: change the StatModifier.
ModifyWeaponDamage(player, item, damage) { damage.Additive += 1; }
```

## How it works inside

The mobile game is compiled: its fields cannot be swapped the way tModLoader
does it. So:

- **the vanilla classes** remain the item's flags and the player's fields.
  What a mod adds to `GetDamage(DamageClass.Melee)` goes into `meleeDamage`
  every frame, before the game uses the value, and all of the game's code
  already sees the bonus;
- **mod classes** (and `Generic`, `Throwing` and `MagicSummonHybrid` on a
  weapon) are unknown to the game: their damage, crit (rolled again),
  knockback, speed, penetration and minion and sentry damage go through Bunny
  Loader hooks;
- a weapon of a mod class turns on the flags of the classes it "counts as" or
  whose prefixes it accepts: that way the game's effects and prefixes work on
  their own.

Only those who use it pay: a mod that only writes
`DamageType = DamageClass.Melee` installs no hook at all.

## Limits

- Game code that reads `meleeDamage` directly (some armors' special effects)
  sees the vanilla classes' bonuses, but not a mod class's.
- The "Generic" that comes from the game's own equipment is the part common to
  the four damage fields: the game does not keep a separate "all damage".
- A class's armor penetration goes into the projectile when it spawns.

## Complete example

The Example Mod has `ExampleDamageClass` (`Content/DamageClasses`),
`ExampleCustomDamageWeapon` (a sword with it) and `ExampleStatBonusAccessory`
(an accessory that changes several classes).
