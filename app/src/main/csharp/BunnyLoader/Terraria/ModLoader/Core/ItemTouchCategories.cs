using System;
using BunnyLoader.Interop;
using Terraria.ID;
using Category = VirtualControllerInputState.Category;

namespace Terraria.ModLoader.Core;

internal static class ItemTouchCategories
{
    public static void ContentReady()
    {
        GameArray<int> table = VirtualControllerInputState.ItemCategories;
        if (table.IsNull) return;
        Item sample = Item.New();
        using var handle = new GameHandle(sample.Pointer);
        for (int type = ItemID.Count; type < table.Length; type++)
        {
            if (table[type] > (int)Category.Generic || ItemLoader.Get(type) == null) continue;
            try
            {
                sample.SetDefaults(type, default);
                table[type] = (int)Of(sample);
            }
            catch (Exception e) { ItemHooks.Report("touch item category", e); }
        }
    }

    static Category Of(Item item)
    {
        if (item.fishingPole > 0) return Category.FishingPoles;
        if (item.healLife > 0 || item.healMana > 0) return Category.HealingPotion;
        if (item.createWall >= 0) return Category.PlacementMultiWalls;
        if (item.createTile >= 0)
        {
            var torches = ItemID.Sets.Torches;
            return (uint)item.type < (uint)torches.Length && torches[item.type] ? Category.Torches : Category.PlacementMulti;
        }
        if (item.pick > 0) return item.channel ? Category.Drills : Category.PickAxe;
        if (item.axe > 0) return item.channel ? Category.Chainsaw : Category.Axe;
        if (item.hammer > 0) return Category.Hammer;
        if (item.buffType > 0 && item.damage <= 0 && item.useStyle == ItemUseStyleID.DrinkLiquid) return Category.BuffPotion;
        if (item.damage <= 0 || item.useStyle == ItemUseStyleID.None) return Category.None;
        if (item.summon) return item.sentry ? Category.SummoningSentry : Category.SummoningMinion;
        if (item.magic)
        {
            if (item.channel) return item.autoReuse ? Category.AutoFireControlledMagicWeapons : Category.ControlledMagicWeapons;
            return item.autoReuse ? Category.AutoFireMagicWeapons : Category.MagicWeapons;
        }
        if (item.ranged) return Ranged(item);
        if (item.melee) return Melee(item);
        return item.useStyle == ItemUseStyleID.Swing ? Category.NonAutoFireMeleeSwing : Category.ManualGuns;
    }

    static Category Ranged(Item item)
    {
        if (item.useAmmo == AmmoID.Arrow) return item.autoReuse ? Category.AutoFireBow : Category.Bow;
        if (item.useAmmo == AmmoID.Rocket) return item.autoReuse ? Category.AutoFireRocketConsuming : Category.RocketConsuming;
        if (item.useAmmo <= 0 && item.noUseGraphic && item.useStyle == ItemUseStyleID.Swing) return Category.ThrowingWeapons;
        return item.autoReuse ? Category.AutomaticGuns : Category.ManualGuns;
    }

    static Category Melee(Item item)
    {
        if (item.useStyle == ItemUseStyleID.Rapier) return Category.NonAutoFireMeleeStab;
        var yoyos = ItemID.Sets.Yoyo;
        if ((uint)item.type < (uint)yoyos.Length && yoyos[item.type]) return Category.Yoyo;
        if (item.noUseGraphic && item.shoot > 0)
        {
            if (item.useStyle == ItemUseStyleID.Swing) return Category.Boomerangs;
            if (item.channel) return item.autoReuse ? Category.FlailsAutoSwing : Category.Flails;
            return Category.Spears;
        }
        if (item.shoot > 0) return item.autoReuse ? Category.AutoFireMeleeProjectile : Category.NonAutoFireMeleeSwing;
        return item.autoReuse ? Category.AutoFireMeleeSwing : Category.NonAutoFireMeleeSwing;
    }
}
