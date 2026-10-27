using System;
using System.Collections.Generic;
using BunnyLoader;
using CsDamageClass.Content.Items;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CsDamageClass;

// Teste da DamageClass do C# (BunnyLoader/Loaders/DamageClassLoader.cs), no
// molde do tools/tests/damageclass (JS). Os bonus de cada passo sao postos a
// cada quadro depois do UpdateEquips (como um acessorio); as medidas saem no
// fim do Player.Update, depois da dobra nos campos do jogo.
// Loga "csdamageclass <caso>: ok | FAIL". Projetil e sentinela ficam para
// quando houver ModProjectile em C#.
public sealed class CsDamageClassMod : Mod
{
    public override void Load()
    {
        On_Player.UpdateEquips += (orig, self, i) =>
        {
            orig(self, i);
            if (i == Main.myPlayer) Steps.Bonus?.Invoke(self);
        };
        On_Player.Update += (orig, self, i) =>
        {
            orig(self, i);
            if (i == Main.myPlayer && !Main.gameMenu) Steps.Tick(self);
        };
        Log.Info("csdamageclass: loaded");
    }
}

// Herda o Generic, conta como magica; 4 de critico e 10 de penetracao a mais.
public sealed class TestClass : DamageClass
{
    public override string? DisplayName => "test damage";

    public override bool GetEffectInheritance(DamageClass damageClass) => damageClass == Magic;

    public override void SetDefaultStats(Player player)
    {
        player.GetCritChance(this) += 4;
        player.GetArmorPenetration(this) += 10;
    }
}

static class Steps
{
    public static Action<Player>? Bonus;

    static readonly List<(Action<Player>? Setup, Action<Player> Measure)> All = new();
    static int frames, current = -1, wait, fails;
    static bool done;

    static int baseSword, baseBlade, baseBladeCrit, baseMeleeCrit;
    static float baseBladeKb;

    static Item Sample(int type)
    {
        Item item = Item.New();
        item.SetDefaults(type, default);
        return item;
    }

    static int Blade => ModContent.ItemType<TestBlade>();
    static DamageClass Test => ModContent.GetInstance<TestClass>();

    static void Check(string name, Func<string?> body)
    {
        string? problem;
        try
        {
            problem = body();
        }
        catch (Exception e)
        {
            problem = "threw " + e;
        }
        if (problem != null) fails++;
        Log.Info($"csdamageclass {name}: {(problem == null ? "ok" : "FAIL (" + problem + ")")}");
    }

    static Steps()
    {
        All.Add((null, p =>
        {
            Check("the 10 vanilla classes, in tModLoader's order", () =>
            {
                DamageClass[] want = { DamageClass.Default, DamageClass.Generic, DamageClass.Melee, DamageClass.MeleeNoSpeed, DamageClass.Ranged,
                                       DamageClass.Magic, DamageClass.Summon, DamageClass.SummonMeleeSpeed, DamageClass.MagicSummonHybrid, DamageClass.Throwing };
                for (int i = 0; i < want.Length; i++) if (want[i].Type != i) return $"{want[i].Name} has Type {want[i].Type}";
                return null;
            });
            Check("the mod class comes after (Type 10+)", () => Test.Type >= 10 ? null : $"Type {Test.Type}");
            Check("GetInstance of a vanilla class", () => ModContent.GetInstance<MeleeDamageClass>() == DamageClass.Melee ? null : "different instance");
            Check("class from flags: sword Melee, pistol Ranged, staff Magic, whip SummonMeleeSpeed", () =>
            {
                var got = new[] { Sample(ItemID.IronBroadsword).DamageType, Sample(ItemID.FlintlockPistol).DamageType,
                                  Sample(ItemID.AmethystStaff).DamageType, Sample(ItemID.BlandWhip).DamageType };
                var want = new[] { DamageClass.Melee, DamageClass.Ranged, DamageClass.Magic, DamageClass.SummonMeleeSpeed };
                for (int i = 0; i < got.Length; i++) if (got[i] != want[i]) return $"{i}: {got[i].Name}";
                return null;
            });
            Check("mod DamageType: the class comes back, flags of what it counts as (magic)", () =>
            {
                Item b = Sample(Blade);
                return b.DamageType == Test && b.magic && !b.melee && !b.ranged && !b.summon ? null : $"{b.DamageType.Name} melee {b.melee} magic {b.magic}";
            });
            Check("the class survives Clone", () => Sample(Blade).Clone().DamageType == Test ? null : "lost");
            Check("CountsAsClass: Magic yes, Melee no", () =>
            {
                Item b = Sample(Blade);
                return b.CountsAsClass(DamageClass.Magic) && !b.CountsAsClass(DamageClass.Melee) ? null : "wrong";
            });
            Check("DamageType = vanilla class only sets its flag (Ranged)", () =>
            {
                Item it = Sample(ItemID.IronBroadsword);
                it.DamageType = DamageClass.Ranged;
                return it.ranged && !it.melee && it.DamageType == DamageClass.Ranged ? null : $"melee {it.melee} ranged {it.ranged}";
            });
            Check("the game's GetArmorPenetration(bool) still works", () => { p.GetArmorPenetration(true); return null; });

            baseSword = p.GetWeaponDamage(Sample(ItemID.IronBroadsword));
            baseBlade = p.GetWeaponDamage(Sample(Blade));
            baseBladeCrit = p.GetWeaponCrit(Sample(Blade));
            baseMeleeCrit = p.meleeCrit;
            Item kbBlade = Sample(Blade);
            baseBladeKb = p.GetWeaponKnockback(kbBlade, kbBlade.knockBack);
            Log.Info($"csdamageclass diag: sword {baseSword}, blade {baseBlade}, blade crit {baseBladeCrit}, meleeCrit {baseMeleeCrit}");
            Check("mod class crit: the game's common (min) + 4 from the class + 6 from the item", () =>
            {
                int common = Math.Min(p.meleeCrit, Math.Min(p.rangedCrit, p.magicCrit));
                return baseBladeCrit == common + 4 + 6 ? null : $"{baseBladeCrit} (crits {p.meleeCrit}/{p.rangedCrit}/{p.magicCrit})";
            });
        }));

        All.Add((p => p.GetDamage(DamageClass.Generic) += 0.5f, p =>
        {
            int sword = p.GetWeaponDamage(Sample(ItemID.IronBroadsword));
            int blade = p.GetWeaponDamage(Sample(Blade));
            Check("Generic +50%: the vanilla sword goes up (folded field)", () => sword > baseSword ? null : $"{baseSword} -> {sword}");
            Check("Generic +50%: the mod class inherits", () => Math.Abs(blade - (int)(baseBlade * 1.5f)) <= 1 ? null : $"{baseBlade} -> {blade}");
        }));

        All.Add((p => p.GetDamage<TestClass>() += 1f, p =>
        {
            int sword = p.GetWeaponDamage(Sample(ItemID.IronBroadsword));
            int blade = p.GetWeaponDamage(Sample(Blade));
            Check("mod class damage only: blade doubles, sword unchanged", () =>
                Math.Abs(blade - baseBlade * 2) <= 1 && sword == baseSword ? null : $"blade {blade}, sword {sword}");
        }));

        All.Add((p =>
        {
            p.GetDamage(DamageClass.Generic).Base += 10;
            p.GetDamage(DamageClass.Generic).Flat += 5;
        }, p =>
        {
            Item sword = Sample(ItemID.IronBroadsword);
            int got = p.GetWeaponDamage(sword);
            int want = (int)((sword.damage + 10) * p.meleeDamage + 5);
            Check("Base and Flat (Generic) on a vanilla item", () => Math.Abs(got - want) <= 1 ? null : $"{got}, expected {want}");
        }));

        All.Add((p =>
        {
            p.GetCritChance(DamageClass.Melee) += 10;
            p.GetCritChance(DamageClass.Generic) += 5;
        }, p =>
        {
            Check("crit Melee +10 and Generic +5 go to meleeCrit", () => p.meleeCrit == baseMeleeCrit + 15 ? null : $"{baseMeleeCrit} -> {p.meleeCrit}");
            int blade = p.GetWeaponCrit(Sample(Blade));
            Check("the mod class inherits only Generic (+5), not Melee", () => blade == baseBladeCrit + 5 ? null : $"{baseBladeCrit} -> {blade}");
        }));

        All.Add((p => p.GetKnockback<TestClass>() += 1f, p =>
        {
            Item b = Sample(Blade);
            float kb = p.GetWeaponKnockback(b, b.knockBack);
            Check("mod class knockback doubles", () => Math.Abs(kb - baseBladeKb * 2) < 0.01f ? null : $"{baseBladeKb} -> {kb}");
        }));

        All.Add((p => p.GetAttackSpeed(DamageClass.Ranged) += 1f, p =>
        {
            Item gun = Sample(ItemID.FlintlockPistol);
            p.ApplyItemTime(gun);
            int t = p.itemTime;
            p.itemTime = 0;
            Check("Ranged speed x2: use time halves", () => t == Math.Max(1, gun.useTime / 2) ? null : $"useTime {gun.useTime}, itemTime {t}");
        }));

        All.Add((p => p.GetArmorPenetration(DamageClass.Magic) += 5, p =>
        {
            float total = p.GetTotalArmorPenetration(DamageClass.Magic);
            float game = p.armorPenetration;
            Check("Magic armor penetration +5 shows in the total", () => Math.Abs(total - game - 5) < 0.01f ? null : $"total {total}, armorPenetration {game}");
        }));
    }

    public static void Tick(Player self)
    {
        if (done) return;
        // Vivo e de dia: morto, o jogo pula o UpdateEquips (e os bonus do passo).
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
            try
            {
                All[current].Measure(self);
            }
            catch (Exception e)
            {
                fails++;
                Log.Info($"csdamageclass step {current}: FAIL threw {e}");
            }
        }
        current++;
        if (current >= All.Count)
        {
            Bonus = null;
            done = true;
            Log.Info("csdamageclass END: " + (fails == 0 ? "all ok" : fails + " failure(s)"));
            return;
        }
        Bonus = All[current].Setup;
        wait = 3;
    }
}
