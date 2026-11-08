using System;
using log4net;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CsLangs;

public sealed class CsLangsMod : Mod
{
    static readonly (string Culture, string Sword, string Buff, string Spray, string Tooltip, string Npc)[] Expected =
    {
        ("pt-BR", "Espada de Exemplo", "Absorção de dano da equipe", "Spray de Exemplo", "Esta é uma espada de mod.", "Zumbi Festeiro"),
        ("ru-RU", "Пример меча", "Эффект поглощения урона команды", "Пример спрея", "Это меч из мода.", "Праздничный зомби"),
        ("zh-Hans", "示例剑", "吸收队伍伤害增益", "示例喷雾", "这是一把模组剑。", "派对僵尸"),
        ("en-US", "Example Sword", "Absorb Team Damage Buff", "Example Spray", "This is a modded sword.", "Party Zombie"),
    };

    static int frame, pass, fail, stage = -1, waitUntil;
    static string original = "";
    static bool done;

    static ILog Log => ModContent.GetInstance<CsLangsMod>().Logger;

    public override void Load() => On_Player.Update += Update;

    static void Report(string name, bool ok, string detail = "")
    {
        if (ok) pass++;
        else fail++;
        Log.Info($"cslangs {name}: {(ok ? "ok" : "FAIL")} {detail}");
    }

    static void Update(On_Player.orig_Update orig, Player self, int i)
    {
        orig(self, i);
        if (Main.gameMenu || i != Main.myPlayer || done) return;
        frame++;
        try
        {
            if (frame == 30)
            {
                original = LanguageManager.Instance.ActiveCulture.Name ?? "en-US";
                Next();
            }
            else if (stage >= 0 && frame == waitUntil)
            {
                Check();
                Next();
            }
        }
        catch (Exception e)
        {
            Report("frame " + frame, false, e.ToString());
            Finish();
        }
    }

    static void Next()
    {
        stage++;
        if (stage >= Expected.Length)
        {
            Finish();
            return;
        }
        LanguageManager.Instance.SetLanguage(Expected[stage].Culture);
        waitUntil = frame + 45;
    }

    static void Check()
    {
        (string culture, string sword, string buff, string spray, string tooltip, string npc) = Expected[stage];
        int swordType = ModContent.Find<ModItem>("ExampleMod/ExampleSword").Type;
        string seenSword = Language.GetTextValue("Mods.ExampleMod.Items.ExampleSword.DisplayName");
        string cachedSword = Lang.GetItemNameValue(swordType);
        string seenBuff = Language.GetTextValue("Mods.ExampleMod.Buffs.AbsorbTeamDamageBuff.DisplayName");
        string seenSpray = Language.GetTextValue("Mods.ExampleMod.Projectiles.ExampleSolutionProjectile.DisplayName");
        Report($"{culture} names", seenSword == sword && cachedSword == sword && seenBuff == buff && seenSpray == spray,
            $"active={LanguageManager.Instance.ActiveCulture.Name} sword={seenSword}/{cachedSword} buff={seenBuff} spray={seenSpray}");
        Terraria.UI.ItemTooltip tip = Lang.GetTooltip(swordType);
        string line = tip.Lines > 0 ? tip.GetLine(0) ?? "" : "";
        string buffName = Lang.GetBuffName(ModContent.Find<ModBuff>("ExampleMod/AbsorbTeamDamageBuff").Type) ?? "";
        string projectileName = Lang.GetProjectileName(ModContent.Find<ModProjectile>("ExampleMod/ExampleSolutionProjectile").Type).Value ?? "";
        string npcName = Lang.GetNPCNameValue(ModContent.Find<ModNPC>("ExampleMod/PartyZombie").Type) ?? "";
        Report($"{culture} game tables", line == tooltip && buffName == buff && projectileName == spray && npcName == npc,
            $"tooltip={line} buff={buffName} projectile={projectileName} npc={npcName}");
    }

    static void Finish()
    {
        done = true;
        if (original.Length > 0) LanguageManager.Instance.SetLanguage(original);
        Log.Info($"cslangs END pass={pass} fail={fail}");
    }
}
