using System;
using System.Collections.Generic;
using BunnyLoader.Interop;
using Terraria.ID;
using Terraria.Localization;

namespace Terraria.ModLoader;

internal static class DamageClassHooks
{
    public static void Install()
    {
        On_Player.ResetEffects += (orig, self) =>
        {
            orig(self);
            DamageClassLoader.ResetEffects(self);
        };

        On_Player.CapAttackSpeeds += (orig, self) =>
        {
            DamageClassLoader.CapAttackSpeeds(self);
            orig(self);
        };

        On_Player.GetWeaponDamageMultiplier += (orig, self, item) =>
            DamageClassLoader.WeaponDamageMultiplier(self, item, () => orig(self, item));

        On_Player.GetWeaponCrit += (orig, self, item) => DamageClassLoader.WeaponCrit(self, item, orig(self, item));

        On_Player.GetWeaponKnockback += (orig, self, item, knockBack) =>
            DamageClassLoader.WeaponKnockback(self, item, orig(self, item, knockBack));

        On_Player.ApplyItemAnimation += (orig, self, item) =>
        {
            orig(self, item);
            DamageClassLoader.ItemAnimation(self, item);
        };

        On_Player.ApplyItemTime_Item += (orig, self, item) =>
        {
            orig(self, item);
            DamageClassLoader.ItemTime(self, item);
        };

        On_Player.ProcessHitAgainstNPC += (orig, self, item, itemRectangle, originalDamage, knockBack, npcIndex) =>
        {
            int extra = DamageClassLoader.HitPenetration(self, item);
            if (extra <= 0)
            {
                orig(self, item, itemRectangle, originalDamage, knockBack, npcIndex);
                return;
            }
            self.armorPenetration += extra;
            try
            {
                orig(self, item, itemRectangle, originalDamage, knockBack, npcIndex);
            }
            finally
            {
                self.armorPenetration -= extra;
            }
        };

        On_Projectile.NewProjectile_IEntitySource_float_float_float_float_int_int_float_int_float_float_float_NewProjectileModifier +=
            (orig, spawnSource, x, y, speedX, speedY, type, damage, knockBack, owner, ai0, ai1, ai2, modifier) =>
            {
                int i = orig(spawnSource, x, y, speedX, speedY, type, damage, knockBack, owner, ai0, ai1, ai2, modifier);
                if (i >= 0 && i < 1000) DamageClassLoader.ProjectileSpawned(Main.projectile[i]);
                return i;
            };

        On_Projectile.Damage += (orig, self) =>
        {
            if (!DamageClassLoader.WantsFiring)
            {
                orig(self);
                return;
            }
            DamageClassLoader.BeginFiring(self, out Projectile outer, out bool wasFiring);
            try
            {
                orig(self);
            }
            finally
            {
                DamageClassLoader.EndFiring(outer, wasFiring);
            }
        };

        On_Player.TryConsumingTimerCrit += (orig, self, hitbox, ref crit) =>
        {
            DamageClassLoader.TimerCrit(self, ref crit);
            orig(self, hitbox, ref crit);
        };

        On_Projectile.Update += (orig, self, i) =>
        {
            orig(self, i);
            DamageClassLoader.MinionDamage(self);
        };
    }

    public static void ModifyTooltips(Item item, List<TooltipLine> lines)
    {
        if (!DamageClassLoader.WantsTooltip(item)) return;
        Player me = Main.LocalPlayer;
        if (me.IsNull) return;
        DamageClass damageClass = DamageClassLoader.ItemClass(item);
        int damageAt = Find(lines, "Damage", NameLines(item, lines), l => StartsWithNumber(l.Text));
        if (damageAt < 0) return;
        bool Show(string line) =>
            DamageClassLoader.Safe(damageClass, nameof(DamageClass.ShowStatTooltipLine), () => damageClass.ShowStatTooltipLine(me, line), true);

        int knockbackAt = Find(lines, "Knockback", damageAt + 1, l => IsTip(l.Text, 14, 22));
        if (knockbackAt >= 0)
        {
            if (!Show("Knockback")) lines.RemoveAt(knockbackAt);
            else if (DamageClassLoader.KnockbackIfAny(me, damageClass) is StatModifier modifier && modifier != StatModifier.Default)
                lines[knockbackAt].Text = Tip(KnockbackTip(Knockback(item, me, modifier)));
        }

        int speedAt = Find(lines, "Speed", damageAt + 1, l => IsTip(l.Text, 6, 13));
        if (speedAt >= 0 && !Show("Speed")) lines.RemoveAt(speedAt);

        int critAt = Find(lines, "CritChance", damageAt + 1, l => StartsWithNumber(l.Text) && l.Text.Trim().EndsWith(Tip(5).Trim(), StringComparison.Ordinal));
        int? crit = null;
        if (DamageClassLoader.UsesCrit(damageClass) && Show("CritChance"))
        {
            int total = DamageClassLoader.RoundHalfUp(me.GetTotalCritChance(damageClass)) + item.GetVisualCritChance(me);
            if (total > 0) crit = total;
        }
        if (crit is int shown)
        {
            string text = shown + Tip(5);
            if (critAt >= 0) lines[critAt].Text = text;
            else lines.Insert(damageAt + 1, new TooltipLine("Terraria", "CritChance", text));
        }
        else if (critAt >= 0)
        {
            lines.RemoveAt(critAt);
        }

        if (!Show("Damage"))
        {
            lines.RemoveAt(damageAt);
            return;
        }
        int damage = (int)(item.damage * ItemID.Sets.ToolTipDamageMultiplier[item.type]);
        if (ItemID.Sets.RapidAttackBonusDamage[item.type]) damage = me.ApplyRapidAttackBonus(damage, item.type, false);
        int value = (int)(me.GetWeaponDamageMultiplier(item) * damage + 5e-6f);
        lines[damageAt].Text = value + (damageClass.IsVanilla ? "" : " ") + DamageClassLoader.NameOf(damageClass);
    }

    static int Find(List<TooltipLine> lines, string name, int from, Predicate<TooltipLine> fallback)
    {
        int at = lines.FindIndex(l => l.Mod == "Terraria" && l.Name == name);
        if (at >= 0) return at;
        for (int i = Math.Max(from, 0); i < lines.Count; i++)
            if (lines[i].Mod == "Terraria" && !lines[i].IsModifier && fallback(lines[i])) return i;
        return -1;
    }

    static int NameLines(Item item, List<TooltipLine> lines)
    {
        if (lines.Count == 0) return 0;
        string name = "";
        try { name = item.HoverName ?? ""; }
        catch (Exception e) { ItemHooks.Report("DamageClass tooltip (the item name)", e); }
        return name.Length > 0 && lines[0].Text == name ? 1 : 0;
    }

    static bool StartsWithNumber(string? text) => !string.IsNullOrEmpty(text) && char.IsDigit(text[0]);

    static bool IsTip(string? text, int first, int last)
    {
        string trimmed = (text ?? "").Trim();
        for (int t = first; t <= last; t++)
            if (trimmed.Length > 0 && trimmed == Tip(t).Trim()) return true;
        return false;
    }

    static string Tip(int index)
    {
        GameArray<LocalizedText> tips = Lang.tip;
        return tips.IsNull || index < 0 || index >= tips.Length || tips[index].IsNull ? "" : tips[index].Value ?? "";
    }

    static float Knockback(Item item, Player me, StatModifier modifier)
    {
        const int ArrowAmmo = 40;
        const int StakeAmmo = 1835;
        const int PsychoKnife = 3106;
        float kb = item.knockBack;
        if (item.summon) kb += me.minionKB;
        if ((me.magicQuiver && item.useAmmo == ArrowAmmo) || item.useAmmo == StakeAmmo) kb = (int)(kb * 1.1f);
        if (me.inventory[me.selectedItem].type == PsychoKnife && item.type == PsychoKnife) kb += kb * (1f - me.stealth);
        return modifier.ApplyTo(kb);
    }

    static int KnockbackTip(float kb) =>
        kb == 0f ? 14 : kb <= 1.5f ? 15 : kb <= 3f ? 16 : kb <= 4f ? 17 : kb <= 6f ? 18 : kb <= 7f ? 19 : kb <= 9f ? 20 : kb <= 11f ? 21 : 22;
}
