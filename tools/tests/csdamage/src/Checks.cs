using System;
using System.Collections.Generic;
using BunnyLoader.Interop;
using CsDamage.Content.DamageClasses;
using CsDamage.Content.Items;
using log4net;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CsDamage;

public static class Checks
{
    const int Slot = 3;

    static int pass, fail, frames;
    static bool done;
    static Item saved;
    static GameHandle? pin;
    static float knockback0, damageGap0;
    static int critGap0;

    static ILog Log => ModContent.GetInstance<CsDamageMod>().Logger;

    public static void Reset()
    {
        frames = pass = fail = 0;
        done = false;
    }

    public static void Frame(Player p)
    {
        if (done) return;
        frames++;
        if (frames == 120)
        {
            try { Static(); Baseline(p); }
            catch (Exception e) { Report("setup", false, e.ToString()); }
            return;
        }
        if (frames != 240) return;
        done = true;
        try { Worn(p); }
        catch (Exception e) { Report("run", false, e.ToString()); }
        finally { Restore(p); }
        Log.Info($"csdamage END pass={pass} fail={fail}");
    }

    static Item Make(int type)
    {
        Item item = Item.New();
        item.SetDefaults(type, default);
        return item;
    }

    static TestDamageClass Class => ModContent.GetInstance<TestDamageClass>();

    static void Static()
    {
        DamageClass[] vanilla = { DamageClass.Default, DamageClass.Generic, DamageClass.Melee, DamageClass.MeleeNoSpeed, DamageClass.Ranged, DamageClass.Magic,
            DamageClass.Summon, DamageClass.SummonMeleeSpeed, DamageClass.MagicSummonHybrid, DamageClass.Throwing };
        bool ordered = true;
        for (int i = 0; i < vanilla.Length; i++) ordered &= vanilla[i].Type == i && DamageClassLoader.GetDamageClass(i) == vanilla[i];
        Report("the game's classes keep tModLoader's types", ordered, "");

        TestDamageClass cls = Class;
        Report("a mod class is registered after them", cls.Type == 10 && DamageClassLoader.DamageClassCount == 11 && DamageClassLoader.GetDamageClass(10) == cls &&
            TestDamageClass.StaticDefaults == 1, $"type={cls.Type} count={DamageClassLoader.DamageClassCount} static={TestDamageClass.StaticDefaults}");
        Report("DisplayName falls back to the pretty name", cls.DisplayName.Value == "Test Damage Class", $"'{cls.DisplayName.Value}'");

        Item sword = Make(ModContent.ItemType<TestClassSword>()), again = Make(ModContent.ItemType<TestClassSword>());
        Report("DamageType set in SetDefaults is the type's", sword.DamageType == cls && again.DamageType == cls &&
            sword.melee && sword.magic && !sword.ranged && !sword.summon, $"melee={sword.melee} magic={sword.magic} ranged={sword.ranged}");
        Report("CountsAsClass follows GetEffectInheritance", sword.CountsAsClass(DamageClass.Melee) && sword.CountsAsClass<TestDamageClass>() &&
            sword.CountsAsClass(DamageClass.Magic) && !sword.CountsAsClass(DamageClass.Ranged) &&
            DamageClass.MeleeNoSpeed.CountsAsClass(DamageClass.Melee) && !DamageClass.Melee.CountsAsClass(DamageClass.MeleeNoSpeed), "");
        Report("GetsPrefixesFor", DamageClass.Throwing.GetsPrefixesFor(DamageClass.Ranged) && DamageClass.Summon.GetsPrefixesFor(DamageClass.Magic) &&
            !DamageClass.Ranged.GetsPrefixesFor(DamageClass.Melee), "");

        var expected = new (int type, DamageClass cls)[]
        {
            (ItemID.CopperShortsword, DamageClass.Melee), (ItemID.WoodenBow, DamageClass.Ranged), (ItemID.WandofSparking, DamageClass.Magic),
            (ItemID.SlimeStaff, DamageClass.Summon), (ItemID.BlandWhip, DamageClass.SummonMeleeSpeed), (ItemID.DirtBlock, DamageClass.Default),
        };
        var wrong = new List<string>();
        foreach (var (type, c) in expected)
        {
            DamageClass got = Make(type).DamageType;
            if (got != c) wrong.Add($"{type}:{got.Name}");
        }
        Report("the game's items get their class from the flags", wrong.Count == 0, string.Join(" ", wrong));

        Item a = Make(ItemID.WoodenSword), b = Make(ItemID.WoodenSword);
        a.DamageType = DamageClass.Ranged;
        Report("DamageType set later is the item's own", a.DamageType == DamageClass.Ranged && a.ranged && !a.melee && b.DamageType == DamageClass.Melee, "");

        Projectile proj = Main.projectile[999];
        bool melee = proj.melee, ranged = proj.ranged, magic = proj.magic;
        proj.DamageType = cls;
        bool took = proj.DamageType == cls && proj.melee && proj.magic && !proj.ranged;
        proj.DamageType = DamageClass.Default;
        proj.melee = melee;
        proj.ranged = ranged;
        proj.magic = magic;
        Report("a projectile takes a class", took, "");
    }

    static void Baseline(Player p)
    {
        Item sword = Make(ModContent.ItemType<TestClassSword>());
        knockback0 = p.GetWeaponKnockback(sword, 4f);
        critGap0 = p.meleeCrit - p.rangedCrit;
        damageGap0 = p.rangedDamage - p.magicDamage;
        var armor = p.armor;
        saved = armor[Slot];
        pin = saved.IsNull ? null : new GameHandle(saved.Pointer);
        armor[Slot] = Make(ModContent.ItemType<TestBonusCharm>());
    }

    static void Worn(Player p)
    {
        TestDamageClass cls = Class;
        Item sword = Make(ModContent.ItemType<TestClassSword>());
        Report("SetDefaultStats and UpdateAccessory add to the class", p.GetCritChance(cls) == 14 && p.GetArmorPenetration<TestDamageClass>() == 10,
            $"crit={p.GetCritChance(cls)} pen={p.GetArmorPenetration(cls)}");

        int critGap = p.meleeCrit - p.rangedCrit;
        float damageGap = p.rangedDamage - p.magicDamage;
        Report("the game's classes are folded into the player's fields", critGap - critGap0 == 10 && MathF.Abs(damageGap - (1.12f * damageGap0 + 0.2f)) < 1e-3f,
            $"crit {critGap0}->{critGap} damage {damageGap0}->{damageGap}");
        Report("GetTotalAttackSpeed", MathF.Abs(p.GetTotalAttackSpeed(DamageClass.Ranged) - 1.15f) < 1e-4f && MathF.Abs(p.GetTotalAttackSpeed(cls) - 1.5f) < 1e-4f,
            $"ranged={p.GetTotalAttackSpeed(DamageClass.Ranged)} class={p.GetTotalAttackSpeed(cls)}");

        StatModifier t = p.GetTotalDamage(cls);
        float expected = ((70 + t.Base) * t.Additive * t.Multiplicative + t.Flat) / 70f, multiplier = p.GetWeaponDamageMultiplier(sword);
        Report("GetWeaponDamageMultiplier uses the class totals", t.Base == 4 && t.Flat == 5 && t.Additive > 1.39f && MathF.Abs(multiplier - expected) < 1e-4f,
            $"add={t.Additive} mult={t.Multiplicative} base={t.Base} flat={t.Flat} got={multiplier} want={expected}");

        int commonCrit = Math.Min(p.meleeCrit, Math.Min(p.rangedCrit, p.magicCrit));
        float totalCrit = p.GetTotalCritChance(cls);
        int weaponCrit = p.GetWeaponCrit(sword);
        Report("GetWeaponCrit of a mod class", MathF.Abs(totalCrit - (14 + commonCrit)) < 1e-4f && weaponCrit == (int)MathF.Floor(totalCrit + 0.5f) + 6,
            $"total={totalCrit} common={commonCrit} weapon={weaponCrit}");

        float knockback = p.GetWeaponKnockback(sword, 4f);
        Report("GetWeaponKnockback applies the class knockback", MathF.Abs(knockback - 2 * knockback0) < 1e-3f, $"before={knockback0} after={knockback}");

        Report("armor penetration by class", p.GetWeaponArmorPenetration(sword) == sword.armorPenetration + 10 + p.armorPenetration &&
            MathF.Abs(p.GetTotalArmorPenetration(DamageClass.Magic) - (5 + p.armorPenetration)) < 1e-4f,
            $"weapon={p.GetWeaponArmorPenetration(sword)} player={p.armorPenetration} magic={p.GetTotalArmorPenetration(DamageClass.Magic)}");

        int time = p.itemTime, timeMax = p.itemTimeMax;
        p.ApplyItemTime(sword);
        int applied = p.itemTime;
        p.itemTime = time;
        p.itemTimeMax = timeMax;
        Report("the class speed sets the item time", applied == 20, $"itemTime={applied}");

        Tooltip(p, sword, totalCrit);
    }

    static void Tooltip(Player p, Item sword, float totalCrit)
    {
        var texts = GameArray<GameString>.New(new GameClass("System", "String"), 64);
        var prefix = GameArray<bool>.New(new GameClass("System", "Boolean"), 64);
        var bad = GameArray<bool>.New(new GameClass("System", "Boolean"), 64);
        int yoyo = -1, research = -1, materials = -1, count = 1, setBonus = -1, shared = -1;
        Color setColor = new(255, 255, 255, 255);
        Main.MouseText_DrawItemTooltip_GetLinesInfo(sword, ref yoyo, ref research, ref materials, sword.knockBack, ref count, texts, prefix, bad,
            ref setBonus, ref setColor, ref shared);
        var lines = new List<string>();
        for (int i = 0; i < count && i < texts.Length; i++) lines.Add(texts[i].ToString() ?? "");
        int crit = (int)MathF.Floor(totalCrit + 0.5f) + sword.GetVisualCritChance(p);
        bool damage = lines.Exists(l => l.EndsWith(" Test Damage Class", StringComparison.Ordinal) && char.IsDigit(l[0]));
        bool critLine = lines.Contains(crit + Lang.tip[5].Value);
        bool speed = false;
        for (int t = 6; t <= 13; t++) speed |= lines.Contains(Lang.tip[t].Value ?? "");
        Report("the tooltip names the class, shows its crit and hides Speed", damage && critLine && !speed, string.Join(" | ", lines));
    }

    static void Restore(Player p)
    {
        try
        {
            if (!saved.IsNull) p.armor[Slot] = saved;
            pin?.Dispose();
            pin = null;
        }
        catch (Exception e) { Report("restore", false, e.Message); }
    }

    static void Report(string name, bool ok, string detail)
    {
        if (ok) pass++;
        else fail++;
        Log.Info($"csdamage {name}: {(ok ? "ok" : "FAIL")} {detail}");
    }
}
