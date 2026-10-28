using System;
using System.Collections.Generic;
using BunnyLoader.Interop;
using CsDamageClass.Content.DamageClasses;
using CsDamageClass.Content.Items;
using log4net;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CsDamageClass;

public static class Steps
{
    public static Action<Player>? Bonus;

    static readonly List<(Action<Player>? Setup, Action<Player> Measure)> All = new();
    static int frames, current = -1, wait, pass, fail;
    static bool done;
    static int baseSword, baseBlade, baseBladeCrit, baseMeleeCrit;
    static float baseBladeKb;

    static ILog Log => ModContent.GetInstance<CsDamageClassMod>().Logger;

    static int Blade => ModContent.ItemType<TestBlade>();

    static DamageClass Test => ModContent.GetInstance<TestClass>();

    static Item Make(int type)
    {
        Item item = Item.New();
        item.SetDefaults(type, default);
        return item;
    }

    static float Kb(Player p, Item item) => p.GetWeaponKnockback(item, item.knockBack);

    static void Report(string name, bool ok, string detail)
    {
        if (ok) pass++;
        else fail++;
        Log.Info($"csdamageclass {name}: {(ok ? "ok" : "FAIL")} {detail}");
    }

    static void Check(string name, Func<(bool Ok, string Detail)> body)
    {
        try
        {
            (bool ok, string detail) = body();
            Report(name, ok, detail);
        }
        catch (Exception e)
        {
            Report(name, false, "threw " + e);
        }
    }

    static List<string> Tooltip(Item item)
    {
        var texts = GameArray<GameString>.New(new GameClass("System", "String"), 30);
        var prefix = GameArray<bool>.New(new GameClass("System", "Boolean"), 30);
        var bad = GameArray<bool>.New(new GameClass("System", "Boolean"), 30);
        int yoyo = -1, research = -1, materials = -1, count = 0, setBonus = -1, shared = -1;
        Color setColor = new(255, 255, 255, 255);
        Main.MouseText_DrawItemTooltip_GetLinesInfo(item, ref yoyo, ref research, ref materials, item.knockBack, ref count, texts, prefix, bad,
            ref setBonus, ref setColor, ref shared);
        var lines = new List<string>();
        for (int i = 0; i < count; i++) lines.Add(texts[i].ToString() ?? "");
        return lines;
    }

    static string Tip(int index) => Lang.tip[index].Value ?? "";

    static Steps()
    {
        All.Add((null, p =>
        {
            Check("the 10 vanilla classes, in tModLoader's order", () =>
            {
                DamageClass[] want = { DamageClass.Default, DamageClass.Generic, DamageClass.Melee, DamageClass.MeleeNoSpeed, DamageClass.Ranged,
                                       DamageClass.Magic, DamageClass.Summon, DamageClass.SummonMeleeSpeed, DamageClass.MagicSummonHybrid, DamageClass.Throwing };
                for (int i = 0; i < want.Length; i++)
                    if (want[i].Type != i) return (false, $"{want[i].Name} has Type {want[i].Type}");
                return (true, "");
            });
            Check("the mod class comes after, and is found by name", () =>
                (Test.Type >= 10 && ModContent.Find<DamageClass>("CsDamageClass/TestClass") == Test, $"Type {Test.Type}"));
            Check("GetInstance of a vanilla class", () => (ModContent.GetInstance<MeleeDamageClass>() == DamageClass.Melee, ""));
            Check("SetStaticDefaults ran once", () => (TestClass.StaticDefaults == 1, $"calls={TestClass.StaticDefaults}"));
            Check("DisplayName from the translations", () => (Test.DisplayName.Value == "test damage", $"'{Test.DisplayName.Value}'"));
            Check("class from flags: sword Melee, pistol Ranged, staff Magic, whip SummonMeleeSpeed", () =>
            {
                var got = new[] { Make(ItemID.IronBroadsword).DamageType, Make(ItemID.FlintlockPistol).DamageType,
                                  Make(ItemID.AmethystStaff).DamageType, Make(ItemID.BlandWhip).DamageType };
                var want = new[] { DamageClass.Melee, DamageClass.Ranged, DamageClass.Magic, DamageClass.SummonMeleeSpeed };
                for (int i = 0; i < got.Length; i++)
                    if (got[i] != want[i]) return (false, $"{i}: {got[i].Name}");
                return (true, "");
            });
            Check("mod DamageType: the class, and the flags of what it counts as (magic)", () =>
            {
                Item b = Make(Blade);
                return (b.DamageType == Test && b.magic && !b.melee && !b.ranged && !b.summon, $"{b.DamageType.Name} melee={b.melee} magic={b.magic}");
            });
            Check("the class survives Clone", () => (Make(Blade).Clone().DamageType == Test, ""));
            Check("CountsAsClass: Magic yes, Melee no", () =>
            {
                Item b = Make(Blade);
                return (b.CountsAsClass(DamageClass.Magic) && !b.CountsAsClass(DamageClass.Melee) && b.CountsAsClass<TestClass>(), "");
            });
            Check("DamageType on one vanilla item: only that item, flags only", () =>
            {
                Item a = Make(ItemID.IronBroadsword), b = Make(ItemID.IronBroadsword);
                a.DamageType = DamageClass.Ranged;
                return (a.ranged && !a.melee && a.DamageType == DamageClass.Ranged && b.DamageType == DamageClass.Melee,
                    $"a melee={a.melee} ranged={a.ranged}, b={b.DamageType.Name}");
            });
            Check("SetDefaults (ResetStats) drops the class set on the object", () =>
            {
                Item a = Make(ItemID.IronBroadsword);
                a.DamageType = Test;
                a.SetDefaults(ItemID.IronBroadsword, default);
                return (a.DamageType == DamageClass.Melee, a.DamageType.Name);
            });
            Check("projectile: DamageType and flags", () =>
            {
                Projectile pr = Projectile.New();
                pr.SetDefaults(ProjectileID.WoodenArrowFriendly);
                bool ranged = pr.DamageType == DamageClass.Ranged;
                pr.DamageType = Test;
                bool set = pr.magic && !pr.ranged && pr.DamageType == Test;
                pr.SetDefaults(ProjectileID.WoodenArrowFriendly);
                return (ranged && set && pr.DamageType == DamageClass.Ranged, $"ranged={ranged} set={set} after={pr.DamageType.Name}");
            });
            Check("the game's GetArmorPenetration(bool) still works", () =>
            {
                p.GetArmorPenetration(true);
                return (true, "");
            });

            baseSword = p.GetWeaponDamage(Make(ItemID.IronBroadsword));
            baseBlade = p.GetWeaponDamage(Make(Blade));
            baseBladeCrit = p.GetWeaponCrit(Make(Blade));
            baseMeleeCrit = p.meleeCrit;
            baseBladeKb = Kb(p, Make(Blade));
            Log.Info($"csdamageclass diag: sword {baseSword}, blade {baseBlade}, blade crit {baseBladeCrit}, meleeCrit {baseMeleeCrit}");
            Check("mod class crit: the game's common crit + 4 from the class + 6 from the item", () =>
            {
                int common = Math.Min(p.meleeCrit, Math.Min(p.rangedCrit, p.magicCrit));
                return (baseBladeCrit == common + 4 + 6, $"{baseBladeCrit} (crits {p.meleeCrit}/{p.rangedCrit}/{p.magicCrit})");
            });
            Check("tooltip: the class name, the class crit, no speed line", () =>
            {
                List<string> lines = Tooltip(Make(Blade));
                string dump = string.Join(" | ", lines);
                bool damage = lines.Exists(l => l.EndsWith(" test damage", StringComparison.Ordinal));
                bool crit = lines.Exists(l => l == baseBladeCrit + Tip(5));
                bool speed = false;
                for (int t = 6; t <= 13; t++) speed |= lines.Contains(Tip(t));
                return (damage && crit && !speed, dump);
            });
        }));

        All.Add((p => p.GetDamage(DamageClass.Generic) += 0.5f, p =>
        {
            int sword = p.GetWeaponDamage(Make(ItemID.IronBroadsword));
            int blade = p.GetWeaponDamage(Make(Blade));
            Check("Generic +50%: the vanilla sword goes up (folded field)", () => (sword > baseSword, $"{baseSword} -> {sword}"));
            Check("Generic +50%: the mod class inherits", () => (Math.Abs(blade - (int)(baseBlade * 1.5f)) <= 1, $"{baseBlade} -> {blade}"));
        }));

        All.Add((p => p.GetDamage<TestClass>() += 1f, p =>
        {
            int sword = p.GetWeaponDamage(Make(ItemID.IronBroadsword));
            int blade = p.GetWeaponDamage(Make(Blade));
            Check("mod class damage only: blade doubles, sword unchanged", () =>
                (Math.Abs(blade - baseBlade * 2) <= 1 && sword == baseSword, $"blade {blade}, sword {sword}"));
        }));

        All.Add((p =>
        {
            p.GetDamage(DamageClass.Generic).Base += 10;
            p.GetDamage(DamageClass.Generic).Flat += 5;
        }, p =>
        {
            Item sword = Make(ItemID.IronBroadsword);
            int got = p.GetWeaponDamage(sword);
            int want = (int)((sword.damage + 10) * p.meleeDamage + 5);
            Check("Base and Flat (Generic) on a vanilla item", () => (Math.Abs(got - want) <= 1, $"{got}, expected {want}"));
        }));

        All.Add((p =>
        {
            p.GetCritChance(DamageClass.Melee) += 10;
            p.GetCritChance(DamageClass.Generic) += 5;
        }, p =>
        {
            Check("crit Melee +10 and Generic +5 go to meleeCrit", () => (p.meleeCrit == baseMeleeCrit + 15, $"{baseMeleeCrit} -> {p.meleeCrit}"));
            int blade = p.GetWeaponCrit(Make(Blade));
            Check("the mod class inherits only Generic (+5), not Melee", () => (blade == baseBladeCrit + 5, $"{baseBladeCrit} -> {blade}"));
        }));

        All.Add((p => p.GetKnockback<TestClass>() += 1f, p =>
        {
            float kb = Kb(p, Make(Blade));
            Check("mod class knockback doubles", () => (Math.Abs(kb - baseBladeKb * 2) < 0.01f, $"{baseBladeKb} -> {kb}"));
        }));

        All.Add((p => p.GetAttackSpeed(DamageClass.Ranged) += 1f, p =>
        {
            Item gun = Make(ItemID.FlintlockPistol);
            p.ApplyItemTime(gun);
            int t = p.itemTime;
            p.itemTime = 0;
            Check("Ranged speed x2: use time halves", () => (t == Math.Max(1, gun.useTime / 2), $"useTime {gun.useTime}, itemTime {t}"));
        }));

        All.Add((p => p.GetArmorPenetration(DamageClass.Magic) += 5, p =>
        {
            float total = p.GetTotalArmorPenetration(DamageClass.Magic);
            float game = p.armorPenetration;
            Check("Magic armor penetration +5 shows in the total", () => (Math.Abs(total - game - 5) < 0.01f, $"total {total}, armorPenetration {game}"));
        }));
    }

    public static void Tick(Player self)
    {
        if (done) return;
        self.statLife = self.statLifeMax2;
        if (frames == 0)
        {
            Main.dayTime = true;
            Main.time = 20000;
        }
        if (++frames < 60) return;
        if (current >= 0 && wait > 0)
        {
            wait--;
            return;
        }
        if (current >= 0)
        {
            try { All[current].Measure(self); }
            catch (Exception e) { Report($"step {current}", false, "threw " + e); }
        }
        current++;
        if (current >= All.Count)
        {
            Bonus = null;
            done = true;
            Log.Info($"csdamageclass END pass={pass} fail={fail}");
            return;
        }
        Bonus = All[current].Setup;
        wait = 3;
    }
}
