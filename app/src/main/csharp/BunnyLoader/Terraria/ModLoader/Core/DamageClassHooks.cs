using System.Collections.Generic;
using System.Threading;
using System;
using Microsoft.Xna.Framework;
using Terraria.ID;
using Terraria.ModLoader;

namespace Terraria.ModLoader.Core;

internal static class DamageClassHooks
{
    static bool[] projectileTypes = Array.Empty<bool>();
    static Projectile firing;

    internal static void Install() => ItemHooks.Once("DamageClass", () =>
    {
        On_Player.ResetEffects += ResetEffects;
        On_Player.CapAttackSpeeds += CapAttackSpeeds;
        On_Player.GetWeaponDamageMultiplier += GetWeaponDamageMultiplier;
        On_Player.GetWeaponCrit += GetWeaponCrit;
        On_Player.GetWeaponKnockback += GetWeaponKnockback;
        On_Player.ApplyItemAnimation += ApplyItemAnimation;
        On_Player.ApplyItemTime_Item += ApplyItemTime;
        On_Player.ProcessHitAgainstNPC += ProcessHitAgainstNPC;
        On_Projectile.NewProjectile_IEntitySource_float_float_float_float_int_int_float_int_float_float_float_NewProjectileModifier += NewProjectile;
        ItemTooltipHooks.WantDamageLines();
    });

    internal static void WantProjectile(int type)
    {
        if (type < 0) return;
        lock (projectileTypes)
        {
            bool[] table = projectileTypes;
            if (type >= table.Length)
            {
                var grown = new bool[Math.Max(type + 1, table.Length * 2)];
                Array.Copy(table, grown, table.Length);
                table = grown;
            }
            table[type] = true;
            Volatile.Write(ref projectileTypes, table);
        }
        ItemHooks.Once("DamageClassProjectiles", () =>
        {
            On_Projectile.Damage += Damage;
            On_Player.TryConsumingTimerCrit += TryConsumingTimerCrit;
            On_Projectile.Update += Update;
        });
    }

    static bool Marked(int type)
    {
        bool[] table = Volatile.Read(ref projectileTypes);
        return (uint)type < (uint)table.Length && table[type];
    }

    static bool HasData(Player player) => DamageClassLoader.Players.Count > 0 && DamageClassLoader.Players.Get(player.Pointer) != null;

    static bool MeleeLike(DamageClass cls) => cls == DamageClass.Melee || cls == DamageClass.MeleeNoSpeed || cls == DamageClass.SummonMeleeSpeed;

    static void ResetEffects(On_Player.orig_ResetEffects orig, Player self)
    {
        orig(self);
        try
        {
            if (DamageClassLoader.Players.Get(self.Pointer) is DamageClassLoader.PlayerStats d) DamageClassLoader.Reset(self, d);
        }
        catch (Exception e) { ItemHooks.Report("DamageClass (ResetEffects)", e); }
    }

    static void CapAttackSpeeds(On_Player.orig_CapAttackSpeeds orig, Player self)
    {
        try { DamageClassLoader.Fold(self); }
        catch (Exception e) { ItemHooks.Report("DamageClass (CapAttackSpeeds)", e); }
        orig(self);
    }

    static float GetWeaponDamageMultiplier(On_Player.orig_GetWeaponDamageMultiplier orig, Player self, Item item)
    {
        DamageClass cls;
        bool hooked;
        try
        {
            cls = DamageClassLoader.ItemClass(item);
            hooked = DamageClassLoader.IsHooked(cls);
            if (!hooked && !HasData(self)) return orig(self, item);
        }
        catch (Exception e)
        {
            ItemHooks.Report("DamageClass (GetWeaponDamageMultiplier)", e);
            return orig(self, item);
        }
        StatModifier t = DamageClassLoader.Total(self, cls).Damage;
        float mult = hooked ? t.Additive * t.Multiplicative : orig(self, item);
        int damage = item.damage;
        if ((t.Base == 0 && t.Flat == 0) || damage <= 0) return mult;
        return ((damage + t.Base) * mult + t.Flat) / damage;
    }

    static int GetWeaponCrit(On_Player.orig_GetWeaponCrit orig, Player self, Item item)
    {
        int value = orig(self, item);
        try
        {
            DamageClass cls = DamageClassLoader.ItemClass(item);
            if (!DamageClassLoader.IsHooked(cls)) return value;
            return DamageClassLoader.UsesCrit(cls) ? DamageClassLoader.Round(DamageClassLoader.Total(self, cls).Crit) + item.crit : 0;
        }
        catch (Exception e)
        {
            ItemHooks.Report("DamageClass (GetWeaponCrit)", e);
            return value;
        }
    }

    static float GetWeaponKnockback(On_Player.orig_GetWeaponKnockback orig, Player self, Item item, float knockBack)
    {
        float vanilla = orig(self, item, knockBack);
        try
        {
            DamageClass cls = DamageClassLoader.ItemClass(item);
            if (!DamageClassLoader.IsHooked(cls) && !HasData(self)) return vanilla;
            StatModifier modifier = DamageClassLoader.Total(self, cls).Knockback;
            return modifier == StatModifier.Default ? vanilla : modifier.ApplyTo(vanilla);
        }
        catch (Exception e)
        {
            ItemHooks.Report("DamageClass (GetWeaponKnockback)", e);
            return vanilla;
        }
    }

    static void ApplyItemAnimation(On_Player.orig_ApplyItemAnimation orig, Player self, Item item)
    {
        orig(self, item);
        try
        {
            DamageClass cls = DamageClassLoader.ItemClass(item);
            bool hooked = DamageClassLoader.IsHooked(cls);
            if (!hooked && (MeleeLike(cls) || !HasData(self))) return;
            float speed = DamageClassLoader.Total(self, cls).Speed;
            if (!hooked && MathF.Abs(speed - 1) < 1e-6f) return;
            int baseFrames = hooked ? item.useAnimation : self.itemAnimationMax;
            int frames = Math.Max(1, (int)(baseFrames / Math.Clamp(speed, 0.01f, 3f)));
            self.itemAnimation = frames;
            self.itemAnimationMax = frames;
        }
        catch (Exception e) { ItemHooks.Report("DamageClass (ApplyItemAnimation)", e); }
    }

    static void ApplyItemTime(On_Player.orig_ApplyItemTime_Item orig, Player self, Item item)
    {
        orig(self, item);
        try
        {
            DamageClass cls = DamageClassLoader.ItemClass(item);
            bool hooked = DamageClassLoader.IsHooked(cls);
            if (!hooked && (MeleeLike(cls) || !HasData(self))) return;
            float speed = DamageClassLoader.Total(self, cls).Speed;
            if (MathF.Abs(speed - 1) < 1e-6f) return;
            int frames = Math.Max(1, (int)(item.useTime / Math.Clamp(speed, 0.01f, 3f)));
            self.itemTime = frames;
            self.itemTimeMax = frames;
        }
        catch (Exception e) { ItemHooks.Report("DamageClass (ApplyItemTime)", e); }
    }

    static void ProcessHitAgainstNPC(On_Player.orig_ProcessHitAgainstNPC orig, Player self, Item item, Rectangle itemRectangle, int originalDamage, float knockBack, int npcIndex)
    {
        int extra = 0;
        try
        {
            DamageClass cls = DamageClassLoader.ItemClass(item);
            if (DamageClassLoader.IsHooked(cls) || HasData(self)) extra = DamageClassLoader.ExtraPen(self, cls, item.melee);
        }
        catch (Exception e) { ItemHooks.Report("DamageClass (ProcessHitAgainstNPC)", e); }
        if (extra <= 0)
        {
            orig(self, item, itemRectangle, originalDamage, knockBack, npcIndex);
            return;
        }
        self.armorPenetration += extra;
        try { orig(self, item, itemRectangle, originalDamage, knockBack, npcIndex); }
        finally { self.armorPenetration -= extra; }
    }

    static int NewProjectile(On_Projectile.orig_NewProjectile_IEntitySource_float_float_float_float_int_int_float_int_float_float_float_NewProjectileModifier orig,
                             DataStructures.IEntitySource spawnSource, float x, float y, float speedX, float speedY, int type, int damage, float knockBack,
                             int owner, float ai0, float ai1, float ai2, NewProjectileModifier modifier)
    {
        int i = orig(spawnSource, x, y, speedX, speedY, type, damage, knockBack, owner, ai0, ai1, ai2, modifier);
        if (i < 0 || i >= 1000) return i;
        try
        {
            Projectile p = Main.projectile[i];
            int who = p.owner;
            if (!p.active || who < 0 || who >= 255) return i;
            Player player = Main.player[who];
            if (player.IsNull || !player.active || !HasData(player)) return i;
            int extra = DamageClassLoader.ExtraPen(player, DamageClassLoader.ProjectileClass(p), p.melee);
            if (extra > 0) p.armorPenetration += extra;
        }
        catch (Exception e) { ItemHooks.Report("DamageClass (NewProjectile)", e); }
        return i;
    }

    static void Damage(On_Projectile.orig_Damage orig, Projectile self)
    {
        if (!Marked(self.type))
        {
            orig(self);
            return;
        }
        Projectile outer = firing;
        firing = self;
        try { orig(self); }
        finally { firing = outer; }
    }

    static void TryConsumingTimerCrit(On_Player.orig_TryConsumingTimerCrit orig, Player self, Rectangle hitbox, ref bool crit)
    {
        Projectile p = firing;
        if (!p.IsNull)
        {
            try
            {
                DamageClass cls = DamageClassLoader.ProjectileClass(p);
                if (DamageClassLoader.IsHooked(cls))
                {
                    float chance = DamageClassLoader.UsesCrit(cls) ? DamageClassLoader.Total(self, cls).Crit + p.bonusCritChance : -1;
                    Item held = self.HeldItem;
                    if (chance >= 0 && !held.IsNull && DamageClassLoader.ItemClass(held) == cls) chance += held.crit;
                    crit = chance >= 0 && Random.Shared.NextSingle() * 100 < chance;
                }
            }
            catch (Exception e) { ItemHooks.Report("DamageClass (TryConsumingTimerCrit)", e); }
        }
        orig(self, hitbox, ref crit);
    }

    static void Update(On_Projectile.orig_Update orig, Projectile self, int i)
    {
        orig(self, i);
        if (!Marked(self.type)) return;
        try
        {
            if (!self.active || !(self.minion || self.sentry) || self.originalDamage <= 0) return;
            Player owner = Main.player[self.owner];
            if (owner.IsNull) return;
            StatModifier damage = DamageClassLoader.Total(owner, DamageClassLoader.ProjectileClass(self)).Damage;
            self.damage = (int)(damage.ApplyTo(self.originalDamage) + 5e-6f);
        }
        catch (Exception e) { ItemHooks.Report("DamageClass (Projectile.Update)", e); }
    }

    internal static void Tooltip(Item item, List<TooltipLine> lines)
    {
        if (item.damage <= 0) return;
        Player me = Main.LocalPlayer;
        if (me.IsNull) return;
        DamageClass cls = DamageClassLoader.ItemClass(item);
        bool hooked = DamageClassLoader.IsHooked(cls);
        bool Show(string line)
        {
            try { return cls.ShowStatTooltipLine(me, line); }
            catch (Exception e)
            {
                ItemHooks.Report($"{cls.FullName}.ShowStatTooltipLine", e);
                return true;
            }
        }
        int Find(string name) => lines.FindIndex(l => l.Mod == "Terraria" && l.Name == name);

        int damage = Find("Damage");
        if (damage >= 0)
        {
            if (!Show("Damage")) lines.RemoveAt(damage);
            else if (hooked)
            {
                string text = lines[damage].Text;
                int digits = 0;
                while (digits < text.Length && char.IsDigit(text[digits])) digits++;
                lines[damage].Text = text[..digits] + (cls is VanillaDamageClass ? "" : " ") + (cls.DisplayName.Value ?? cls.PrettyPrintName());
            }
        }

        int crit = Find("CritChance");
        if (hooked)
        {
            string? text = null;
            if (DamageClassLoader.UsesCrit(cls))
            {
                int c = DamageClassLoader.Round(DamageClassLoader.Total(me, cls).Crit) + item.GetVisualCritChance(me);
                if (c > 0) text = c + Lang.tip[5].Value;
            }
            if (text == null || !Show("CritChance"))
            {
                if (crit >= 0) lines.RemoveAt(crit);
            }
            else if (crit >= 0) lines[crit].Text = text;
            else lines.Insert(After(lines, "Damage", "ItemName"), new TooltipLine("Terraria", "CritChance", text));
        }
        else if (crit >= 0 && !Show("CritChance")) lines.RemoveAt(crit);

        int speed = Find("Speed");
        if (speed >= 0 && !Show("Speed")) lines.RemoveAt(speed);
        else if (speed < 0 && hooked && item.useStyle != 0 && Show("Speed"))
            lines.Insert(After(lines, "CritChance", "Damage", "ItemName"), new TooltipLine("Terraria", "Speed", Lang.tip[SpeedTip(item.useAnimation)].Value ?? ""));

        int knockback = Find("Knockback");
        if (knockback < 0) return;
        if (!Show("Knockback"))
        {
            lines.RemoveAt(knockback);
            return;
        }
        if (!HasData(me)) return;
        StatModifier km = DamageClassLoader.Total(me, cls).Knockback;
        if (km == StatModifier.Default) return;
        float kb = item.knockBack;
        if (item.summon) kb += me.minionKB;
        if (me.magicQuiver && item.useAmmo == ItemID.WoodenArrow || item.useAmmo == ItemID.Stake) kb = (int)(kb * 1.1f);
        Item selected = me.inventory[me.selectedItem];
        if (!selected.IsNull && selected.type == ItemID.PsychoKnife && item.type == ItemID.PsychoKnife) kb += kb * (1 - me.stealth);
        lines[knockback].Text = Lang.tip[KnockbackTip(km.ApplyTo(kb))].Value ?? "";
    }

    static int After(List<TooltipLine> lines, params string[] names)
    {
        foreach (string name in names)
        {
            int at = lines.FindIndex(l => l.Mod == "Terraria" && l.Name == name);
            if (at >= 0) return at + 1;
        }
        return lines.Count;
    }

    static int SpeedTip(int a) => a <= 8 ? 6 : a <= 20 ? 7 : a <= 25 ? 8 : a <= 30 ? 9 : a <= 35 ? 10 : a <= 45 ? 11 : a <= 55 ? 12 : 13;

    static int KnockbackTip(float kb) => kb == 0 ? 14 : kb <= 1.5f ? 15 : kb <= 3 ? 16 : kb <= 4 ? 17 : kb <= 6 ? 18 : kb <= 7 ? 19 : kb <= 9 ? 20 : kb <= 11 ? 21 : 22;
}
