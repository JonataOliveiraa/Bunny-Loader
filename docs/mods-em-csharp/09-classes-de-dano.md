# 9. Damage classes

> [Overview](README.md) · [1. First mod](01-primeiro-mod.md) · [2. Hooks](02-hooks.md) · [3. `ref`, `out`, `Nullable`](03-ref-out-e-nullable.md) · [4. Values and objects](04-valores-e-objetos.md) · [5. C# × JS × tModLoader](05-comparacao.md) · [6. Performance](06-desempenho.md) · [7. Moving to C#](07-migracao-para-csharp.md) · [8. Under the hood](08-por-baixo-dos-panos.md) · **9. Damage classes**

A damage class says what kind of damage a weapon does (melee, ranged, magic,
summon, or one of your own) and which bonuses apply to it. The API is
tModLoader's, in `Terraria.ModLoader`: a `DamageClass` from tModLoader's
ExampleMod compiles here unchanged.

## Giving an item a class

```csharp
using BunnyLoader;
using Terraria;
using Terraria.ModLoader;

public class MySword : ModItem
{
    public override void SetDefaults()
    {
        Item.DamageType = DamageClass.Melee;
        Item.damage = 30;
        // ...
    }
}
```

The ten classes of the game are static members of `DamageClass`, in
tModLoader's order: `Default`, `Generic`, `Melee`, `MeleeNoSpeed`, `Ranged`,
`Magic`, `Summon`, `SummonMeleeSpeed` (whips), `MagicSummonHybrid` and
`Throwing`.

A `DamageType` written in `SetDefaults` holds for the whole item type: it
survives `Clone`, `NewItem` and the network. Written anywhere else (a hook on a
vanilla item, for example), it holds for that one item while it keeps its
type. `Projectile.DamageType` works the same way.

`item.CountsAsClass(DamageClass.Magic)` asks whether the item's class counts as
magic for the game's effects.

## Bonuses on the player

The player's bonuses go back to normal every frame. Add them where tModLoader
adds them, every frame (an accessory, armor, a buff):

```csharp
player.GetDamage(DamageClass.Generic) += 0.25f;   // +25% damage (additive)
player.GetDamage(DamageClass.Generic) *= 1.12f;   // x1.12 (multiplicative)
player.GetDamage(DamageClass.Generic).Base += 4;  // +4 before the multipliers
player.GetDamage(DamageClass.Generic).Flat += 5;  // +5 after them
player.GetCritChance(DamageClass.Melee) += 10;    // +10% melee crit
player.GetAttackSpeed(DamageClass.Ranged) += 0.15f;
player.GetArmorPenetration(DamageClass.Magic) += 5;
player.GetKnockback<ExampleDamageClass>() += 1f;
```

`GetDamage`, `GetCritChance`, `GetAttackSpeed`, `GetArmorPenetration` and
`GetKnockback` return the value **by `ref`**, so `+=` writes into the player.
The `GetTotal*` versions add what the class inherits from others (see below)
and are read only. `GetWeaponAttackSpeed(item)` and
`GetWeaponArmorPenetration(item)` give the numbers for an item.

`StatModifier` is the same struct as tModLoader's:
`(value + Base) * Additive * Multiplicative + Flat`.

## Your own class

```csharp
using BunnyLoader;
using Terraria;
using Terraria.ModLoader;

public class ExampleDamageClass : DamageClass
{
    // Gets every "all damage" bonus, and nothing from the other classes.
    public override StatInheritanceData GetModifierInheritance(DamageClass damageClass) =>
        damageClass == DamageClass.Generic ? StatInheritanceData.Full : StatInheritanceData.None;

    // Counts as melee and magic for the game's effects (and their prefixes).
    public override bool GetEffectInheritance(DamageClass damageClass) =>
        damageClass == DamageClass.Melee || damageClass == DamageClass.Magic;

    // The class's own base bonuses, on every player, every frame.
    public override void SetDefaultStats(Player player)
    {
        player.GetCritChance<ExampleDamageClass>() += 4;
        player.GetArmorPenetration<ExampleDamageClass>() += 10;
    }

    // Hides the speed line from the tooltip of its weapons.
    public override bool ShowStatTooltipLine(Player player, string lineName) => lineName != "Speed";
}
```

The class is registered by itself, like a `ModItem`, and an item takes it with
`Item.DamageType = ModContent.GetInstance<ExampleDamageClass>();`.

| Member | What it decides |
|---|---|
| `GetModifierInheritance(other)` | How much of `other`'s bonuses this class gets (`StatInheritanceData`, 1 = all). Default: all of `Generic`, nothing else. |
| `GetEffectInheritance(other)` | Whether it counts as `other` for the game's effects. |
| `GetPrefixInheritance(other)` | Whether its items take `other`'s prefixes. Default: the classes it counts as. |
| `SetDefaultStats(player)` | Base bonuses, added every frame. |
| `UseStandardCritCalcs` | `false` = no crits (like summon). |
| `ShowStatTooltipLine(player, line)` | Hides `"Damage"`, `"CritChance"`, `"Speed"` or `"Knockback"`. |

The tooltip name comes from the mod's translations,
`Localization/<culture>.json`:

```json
{
  "DamageClasses": {
    "ExampleDamageClass": { "DisplayName": "example damage" }
  }
}
```

so the weapon reads "70 example damage". Without it, the class name is split
("Example Damage Class"). `DisplayName` can also be overridden with a fixed
string.

## What the phone does differently

The mobile game has no damage classes: it has the item's flags (`melee`,
`ranged`, `magic`, `summon`) and the player's fields (`meleeDamage`,
`meleeCrit`, `meleeSpeed`, `minionDamage`...).

- Bonuses on the game's own classes are folded into those fields before
  `CapAttackSpeeds`, so everything in the game that reads them sees the bonus.
- An item of a class of yours gets the flags of the classes it counts as
  (`GetEffectInheritance`) or takes prefixes from. The game's effects and
  prefixes follow those flags.
- Your classes, and `Generic`/`Throwing`/`MagicSummonHybrid` on an item, go
  through hooks: weapon damage, crit, knockback, use speed, armor penetration
  (on hit and on spawned projectiles), the projectile crit roll, minion damage
  and the tooltip.

The example is `samples/ExampleModCSharp` (`Content/DamageClasses/ExampleDamageClass.cs`
and `Content/Items/Weapons/ExampleCustomDamageWeapon.cs`); the test pack is
`tools/tests/csdamageclass`.
