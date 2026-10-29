using System.Collections.Generic;
using System;
using BunnyLoader.Interop;
using BunnyLoader;
using Microsoft.Xna.Framework;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace Terraria.ModLoader.Core;

internal static class ItemShootHooks
{
    sealed class Shot
    {
        public Player Player;
        public Item Item;
    }

    sealed class AmmoScope
    {
        public Player Player;
        public Item Weapon;
        public bool DontConsume;
        public Item Item;
        public int Stack;
        public bool Consumable;
        public bool Blocked;
        public bool Clear;
    }

    static bool globalShoot, globalSelection, globalAvailability, globalAmmo;

    static Shot? shot;

    static AmmoScope? ammo;

    static readonly Dictionary<int, Item> DefaultAmmoItems = new();

    static readonly List<GameHandle> Pins = new();

    public static void Install(Func<string, bool> has, bool global)
    {
        if (has(nameof(ModItem.CanShoot)) || has(nameof(ModItem.ModifyShootStats)) || has(nameof(ModItem.Shoot)))
        {
            if (global) globalShoot = true;
            ItemHooks.Once("Shoot", () =>
            {
                On_Player.ItemCheck_Shoot += ItemCheck_Shoot;
                On_Projectile.NewProjectile_IEntitySource_float_float_float_float_int_int_float_int_float_float_float_NewProjectileModifier += NewProjectile;
            });
        }
        bool chosen = has(nameof(ModItem.CanBeChosenAsAmmo));
        bool ammoSide = chosen || has(nameof(ModItem.CanBeConsumedAsAmmo)) || has(nameof(ModItem.OnConsumedAsAmmo)) || has(nameof(ModItem.PickAmmo));
        if (chosen || has(nameof(ModItem.CanChooseAmmo)))
        {
            if (global || chosen) globalSelection = true;
            ItemHooks.Once("AmmoSelection", () => On_Player.PickAmmo_IterateRange += PickAmmo_IterateRange);
        }
        if (chosen || has(nameof(ModItem.CanChooseAmmo)) || has(nameof(ModItem.NeedsAmmo)))
        {
            if (global || chosen) globalAvailability = true;
            ItemHooks.Once("AmmoAvailability", () => On_Player.HasAmmo += HasAmmo);
        }
        if (ammoSide || has(nameof(ModItem.NeedsAmmo)) || has(nameof(ModItem.CanChooseAmmo)) || has(nameof(ModItem.CanConsumeAmmo)) || has(nameof(ModItem.OnConsumeAmmo)))
        {
            if (global || ammoSide) globalAmmo = true;
            ItemHooks.Once("Ammo", () =>
            {
                On_Player.PickAmmo += PickAmmo;
                On_Player.PickAmmo_PickAmmoItem += PickAmmo_PickAmmoItem;
                On_Item.TurnToAir += TurnToAir;
            });
        }
    }

    static bool CanShoot(Item item, Player player)
    {
        foreach (GlobalItem? g in ItemHooks.GlobalsOf(item))
        {
            if (g == null) continue;
            try
            {
                if (!g.CanShoot(item, player)) return false;
            }
            catch (Exception e) { ItemHooks.Report($"{g.FullName}.CanShoot", e); }
        }
        if (ItemHooks.ModItemOf(item) is ModItem m)
        {
            try { return m.CanShoot(player); }
            catch (Exception e) { ItemHooks.Report($"{m.FullName}.CanShoot", e); }
        }
        return true;
    }

    static void ItemCheck_Shoot(On_Player.orig_ItemCheck_Shoot orig, Player self, int i, Item sItem, int weaponDamage, bool withAudioVisualFeedback)
    {
        if (!ItemCombatHooks.Wanted(globalShoot, sItem))
        {
            orig(self, i, sItem, weaponDamage, withAudioVisualFeedback);
            return;
        }
        if (!CanShoot(sItem, self))
        {
            self.ApplyItemTime(sItem);
            return;
        }
        Shot? outer = shot;
        shot = new Shot { Player = self, Item = sItem };
        try { orig(self, i, sItem, weaponDamage, withAudioVisualFeedback); }
        finally { shot = outer; }
    }

    static int NewProjectile(On_Projectile.orig_NewProjectile_IEntitySource_float_float_float_float_int_int_float_int_float_float_float_NewProjectileModifier orig,
                             IEntitySource spawnSource, float x, float y, float speedX, float speedY, int type, int damage, float knockBack,
                             int owner, float ai0, float ai1, float ai2, NewProjectileModifier modifier)
    {
        Shot? current = shot;
        if (current == null || current.Player.whoAmI != owner)
            return orig(spawnSource, x, y, speedX, speedY, type, damage, knockBack, owner, ai0, ai1, ai2, modifier);
        shot = null;
        try
        {
            Item item = current.Item;
            Player player = current.Player;
            var position = new Vector2(x, y);
            var velocity = new Vector2(speedX, speedY);
            if (ItemHooks.ModItemOf(item) is ModItem m)
            {
                try { m.ModifyShootStats(player, ref position, ref velocity, ref type, ref damage, ref knockBack); }
                catch (Exception e) { ItemHooks.Report($"{m.FullName}.ModifyShootStats", e); }
            }
            foreach (GlobalItem? g in ItemHooks.GlobalsOf(item))
            {
                if (g == null) continue;
                try { g.ModifyShootStats(item, player, ref position, ref velocity, ref type, ref damage, ref knockBack); }
                catch (Exception e) { ItemHooks.Report($"{g.FullName}.ModifyShootStats", e); }
            }
            var source = EntitySource_ItemUse_WithAmmo.Is(spawnSource) ? EntitySource_ItemUse_WithAmmo.From(spawnSource) : default;
            if (!Shoot(item, player, source, position, velocity, type, damage, knockBack)) return 1000;
            return orig(spawnSource, position.X, position.Y, velocity.X, velocity.Y, type, damage, knockBack, owner, ai0, ai1, ai2, modifier);
        }
        finally { shot = current; }
    }

    static bool Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
    {
        bool result = true;
        foreach (GlobalItem? g in ItemHooks.GlobalsOf(item))
        {
            if (g == null) continue;
            try { result &= g.Shoot(item, player, source, position, velocity, type, damage, knockback); }
            catch (Exception e) { ItemHooks.Report($"{g.FullName}.Shoot", e); }
        }
        if (!result) return false;
        if (ItemHooks.ModItemOf(item) is ModItem m)
        {
            try { return m.Shoot(player, source, position, velocity, type, damage, knockback); }
            catch (Exception e) { ItemHooks.Report($"{m.FullName}.Shoot", e); }
        }
        return true;
    }

    static bool NeedsAmmo(Item weapon, Player player)
    {
        if (ItemHooks.ModItemOf(weapon) is ModItem m)
        {
            try
            {
                if (!m.NeedsAmmo(player)) return false;
            }
            catch (Exception e) { ItemHooks.Report($"{m.FullName}.NeedsAmmo", e); }
        }
        foreach (GlobalItem? g in ItemHooks.GlobalsOf(weapon))
        {
            if (g == null) continue;
            try
            {
                if (!g.NeedsAmmo(weapon, player)) return false;
            }
            catch (Exception e) { ItemHooks.Report($"{g.FullName}.NeedsAmmo", e); }
        }
        return true;
    }

    static bool CanChooseAmmo(Item weapon, Item ammo, Player player)
    {
        bool? result = null;
        foreach (GlobalItem? g in ItemHooks.GlobalsOf(weapon))
        {
            if (g == null) continue;
            bool? r = null;
            try { r = g.CanChooseAmmo(weapon, ammo, player); }
            catch (Exception e) { ItemHooks.Report($"{g.FullName}.CanChooseAmmo", e); }
            if (r is false) return false;
            result ??= r;
        }
        foreach (GlobalItem? g in ItemHooks.GlobalsOf(ammo))
        {
            if (g == null) continue;
            bool? r = null;
            try { r = g.CanBeChosenAsAmmo(ammo, weapon, player); }
            catch (Exception e) { ItemHooks.Report($"{g.FullName}.CanBeChosenAsAmmo", e); }
            if (r is false) return false;
            result ??= r;
        }
        if (ItemHooks.ModItemOf(weapon) is ModItem w)
        {
            bool? r = null;
            try { r = w.CanChooseAmmo(ammo, player); }
            catch (Exception e) { ItemHooks.Report($"{w.FullName}.CanChooseAmmo", e); }
            if (r is false) return false;
            result ??= r;
        }
        if (ItemHooks.ModItemOf(ammo) is ModItem a)
        {
            bool? r = null;
            try { r = a.CanBeChosenAsAmmo(weapon, player); }
            catch (Exception e) { ItemHooks.Report($"{a.FullName}.CanBeChosenAsAmmo", e); }
            if (r is false) return false;
            result ??= r;
        }
        return result ?? ammo.ammo == weapon.useAmmo;
    }

    static bool CanConsumeAmmo(Item weapon, Item ammo, Player player)
    {
        foreach (GlobalItem? g in ItemHooks.GlobalsOf(weapon))
        {
            if (g == null) continue;
            try
            {
                if (!g.CanConsumeAmmo(weapon, ammo, player)) return false;
            }
            catch (Exception e) { ItemHooks.Report($"{g.FullName}.CanConsumeAmmo", e); }
        }
        foreach (GlobalItem? g in ItemHooks.GlobalsOf(ammo))
        {
            if (g == null) continue;
            try
            {
                if (!g.CanBeConsumedAsAmmo(ammo, weapon, player)) return false;
            }
            catch (Exception e) { ItemHooks.Report($"{g.FullName}.CanBeConsumedAsAmmo", e); }
        }
        if (ItemHooks.ModItemOf(weapon) is ModItem w)
        {
            try
            {
                if (!w.CanConsumeAmmo(ammo, player)) return false;
            }
            catch (Exception e) { ItemHooks.Report($"{w.FullName}.CanConsumeAmmo", e); }
        }
        if (ItemHooks.ModItemOf(ammo) is ModItem a)
        {
            try
            {
                if (!a.CanBeConsumedAsAmmo(weapon, player)) return false;
            }
            catch (Exception e) { ItemHooks.Report($"{a.FullName}.CanBeConsumedAsAmmo", e); }
        }
        return true;
    }

    static void OnConsumeAmmo(Item weapon, Item ammo, Player player)
    {
        if (ItemHooks.ModItemOf(weapon) is ModItem w)
        {
            try { w.OnConsumeAmmo(ammo, player); }
            catch (Exception e) { ItemHooks.Report($"{w.FullName}.OnConsumeAmmo", e); }
        }
        if (ItemHooks.ModItemOf(ammo) is ModItem a)
        {
            try { a.OnConsumedAsAmmo(weapon, player); }
            catch (Exception e) { ItemHooks.Report($"{a.FullName}.OnConsumedAsAmmo", e); }
        }
        foreach (GlobalItem? g in ItemHooks.GlobalsOf(weapon))
        {
            if (g == null) continue;
            try { g.OnConsumeAmmo(weapon, ammo, player); }
            catch (Exception e) { ItemHooks.Report($"{g.FullName}.OnConsumeAmmo", e); }
        }
        foreach (GlobalItem? g in ItemHooks.GlobalsOf(ammo))
        {
            if (g == null) continue;
            try { g.OnConsumedAsAmmo(ammo, weapon, player); }
            catch (Exception e) { ItemHooks.Report($"{g.FullName}.OnConsumedAsAmmo", e); }
        }
    }

    static void PickAmmo(Item weapon, Item item, Player player, ref int type, ref float speed, ref StatModifier damage, ref float knockback)
    {
        if (ItemHooks.ModItemOf(item) is ModItem m)
        {
            try { m.PickAmmo(weapon, player, ref type, ref speed, ref damage, ref knockback); }
            catch (Exception e) { ItemHooks.Report($"{m.FullName}.PickAmmo", e); }
        }
        foreach (GlobalItem? g in ItemHooks.GlobalsOf(item))
        {
            if (g == null) continue;
            try { g.PickAmmo(weapon, item, player, ref type, ref speed, ref damage, ref knockback); }
            catch (Exception e) { ItemHooks.Report($"{g.FullName}.PickAmmo", e); }
        }
    }

    static bool Eligible(Player player, Item weapon, Item item) =>
        !item.IsNull && item.type > 0 && item.stack > 0 && CanChooseAmmo(weapon, item, player);

    static Item PickAmmo_IterateRange(On_Player.orig_PickAmmo_IterateRange orig, Player self, Item sItem, GameArray<Item> items, GameArray<int> slotIterationOrder, bool allowAmmoCycling)
    {
        if (!ItemCombatHooks.Wanted(globalSelection, sItem)) return orig(self, sItem, items, slotIterationOrder, allowAmmoCycling);
        try
        {
            bool cycling = allowAmmoCycling && (int)self.ammoCyclingMode != 0 && sItem.type != 5134 && sItem.type != 779;
            List<Item>? eligible = null;
            for (int i = 0; i < slotIterationOrder.Length; i++)
            {
                Item candidate = items[slotIterationOrder[i]];
                if (!Eligible(self, sItem, candidate)) continue;
                if (!cycling) return candidate;
                (eligible ??= new List<Item>()).Add(candidate);
            }
            return eligible is { Count: > 0 } ? eligible[Math.Abs(self.ammoCyclingOffset) % eligible.Count] : default;
        }
        catch (Exception e)
        {
            ItemHooks.Report("CanChooseAmmo", e);
            return orig(self, sItem, items, slotIterationOrder, allowAmmoCycling);
        }
    }

    static bool HasAmmo(On_Player.orig_HasAmmo orig, Player self, Item sItem, bool canUse)
    {
        if (!ItemCombatHooks.Wanted(globalAvailability, sItem)) return orig(self, sItem, canUse);
        if (sItem.useAmmo <= 0) return canUse;
        try { return !self.PickAmmo_PickAmmoItem(sItem).IsNull || !NeedsAmmo(sItem, self); }
        catch (Exception e)
        {
            ItemHooks.Report("NeedsAmmo", e);
            return orig(self, sItem, canUse);
        }
    }

    static Item DefaultAmmo(Item weapon)
    {
        int type = weapon.useAmmo;
        if (type <= 0) return default;
        lock (DefaultAmmoItems)
        {
            if (DefaultAmmoItems.TryGetValue(type, out Item cached)) return cached;
            Item item = Item.New();
            Pins.Add(new GameHandle(item.Pointer));
            item.SetDefaults(type, default);
            if (item.ammo != type) item = default;
            else item.consumable = false;
            DefaultAmmoItems[type] = item;
            return item;
        }
    }

    static void PickAmmo(On_Player.orig_PickAmmo orig, Player self, Item sItem, ref int projToShoot, ref float speed, ref bool canShoot, ref int Damage, ref float KnockBack, out int usedAmmoItemId, bool dontConsume)
    {
        if (!ItemCombatHooks.Wanted(globalAmmo, sItem))
        {
            orig(self, sItem, ref projToShoot, ref speed, ref canShoot, ref Damage, ref KnockBack, out usedAmmoItemId, dontConsume);
            return;
        }
        AmmoScope? outer = ammo;
        var scope = new AmmoScope { Player = self, Weapon = sItem, DontConsume = dontConsume };
        ammo = scope;
        try
        {
            orig(self, sItem, ref projToShoot, ref speed, ref canShoot, ref Damage, ref KnockBack, out usedAmmoItemId, dontConsume);
            Finish(scope, ref projToShoot, ref speed, canShoot, ref Damage, ref KnockBack);
        }
        finally
        {
            if (!scope.Item.IsNull && scope.Blocked) scope.Item.consumable = scope.Consumable;
            ammo = null;
            try
            {
                if (scope.Clear) scope.Item.TurnToAir();
            }
            finally { ammo = outer; }
        }
    }

    static void Finish(AmmoScope scope, ref int type, ref float speed, bool canShoot, ref int damage, ref float knockback)
    {
        if (scope.Item.IsNull || !canShoot) return;
        try
        {
            int baseDamage = damage, previousType = type;
            float previousSpeed = speed, previousKnockback = knockback;
            StatModifier modifier = StatModifier.Default;
            PickAmmo(scope.Weapon, scope.Item, scope.Player, ref type, ref speed, ref modifier, ref knockback);
            if (modifier != StatModifier.Default)
            {
                float value = modifier.ApplyTo(baseDamage);
                damage = float.IsFinite(value) ? Math.Max(0, (int)value) : baseDamage;
            }
            if (type < 0) type = previousType;
            if (!float.IsFinite(speed)) speed = previousSpeed;
            if (!float.IsFinite(knockback)) knockback = previousKnockback;
            else knockback = Math.Max(0f, knockback);
        }
        catch (Exception e) { ItemHooks.Report("PickAmmo", e); }
        if (!scope.DontConsume && !scope.Blocked && scope.Item.stack < scope.Stack)
            OnConsumeAmmo(scope.Weapon, scope.Item, scope.Player);
    }

    static Item PickAmmo_PickAmmoItem(On_Player.orig_PickAmmo_PickAmmoItem orig, Player self, Item sItem)
    {
        Item item = orig(self, sItem);
        AmmoScope? scope = ammo;
        if (scope == null || scope.Player != self || scope.Weapon != sItem) return item;
        try
        {
            if (item.IsNull && !NeedsAmmo(sItem, self)) item = DefaultAmmo(sItem);
            if (item.IsNull) return item;
            scope.Item = item;
            scope.Stack = item.stack;
            scope.Consumable = item.consumable;
            if (!scope.DontConsume && item.consumable)
            {
                scope.Blocked = !CanConsumeAmmo(sItem, item, self);
                if (scope.Blocked) item.consumable = false;
            }
        }
        catch (Exception e) { ItemHooks.Report("CanConsumeAmmo", e); }
        return item;
    }

    static void TurnToAir(On_Item.orig_TurnToAir orig, Item self)
    {
        AmmoScope? scope = ammo;
        if (scope != null && !scope.DontConsume && !scope.Blocked && scope.Item == self && self.stack < scope.Stack)
        {
            scope.Clear = true;
            return;
        }
        orig(self);
    }
}
