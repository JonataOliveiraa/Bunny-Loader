using System;
using log4net;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CsBuff;

public static class Checks
{
    static int pass, fail, frames, npcIndex = -1, defenseWithout;
    static bool done;

    static ILog Log => ModContent.GetInstance<CsBuffMod>().Logger;

    public static void Reset()
    {
        frames = pass = fail = 0;
        npcIndex = -1;
        done = false;
    }

    public static void Frame(Player p)
    {
        if (done) return;
        frames++;
        try
        {
            if (frames == 120) Start(p);
            else if (frames == 400) Finish(p);
        }
        catch (Exception e)
        {
            Report("run", false, e.ToString());
            done = true;
            Log.Info($"csbuff END pass={pass} fail={fail}");
        }
    }

    static void Start(Player p)
    {
        int type = ModContent.BuffType<TestBuff>();
        Report("the ModBuff gets a type after the game's", type >= BuffID.Count && BuffLoader.GetBuff(type) is TestBuff && ModContent.GetModBuff(type) is TestBuff,
            $"type={type} count={BuffID.Count}");
        Report("SetStaticDefaults runs with the game's tables grown", TestBuff.StaticRan && Main.buffNoSave[type] && Main.debuff.Length > type,
            $"ran={TestBuff.StaticRan} noSave={Main.buffNoSave[type]} tables={Main.debuff.Length}");
        string name = Lang.GetBuffName(type) ?? "";
        string tip = Main.GetBuffTooltip(p, type) ?? "";
        Report("ModifyBuffText changes the name and the tip", (name == "Test Buff (csbuff)" || name == "Buff de Teste (csbuff)") &&
            (tip == "A test buff +7" || tip == "Um buff de teste +7"), $"name='{name}' tip='{tip}'");
        string vanilla = Lang.GetBuffName(BuffID.Regeneration) ?? "";
        Report("GlobalBuff.ModifyBuffText on a game buff", vanilla.StartsWith("[g] ", StringComparison.Ordinal), $"name='{vanilla}'");

        defenseWithout = p.statDefense;
        p.AddBuff(type, 600);
        p.AddBuff(BuffID.Regeneration, 600);
        int x = (int)p.position.X + 300, y = (int)p.position.Y;
        npcIndex = NPC.NewNPC(default, x, y, NPCID.Bunny);
        if ((uint)npcIndex < 200u) Main.npc[npcIndex].AddBuff(type, 600);
        Main.playerInventory = true;
        Log.Info("csbuff inventory open for the buff icons");
    }

    static void Finish(Player p)
    {
        done = true;
        int type = ModContent.BuffType<TestBuff>();
        int index = p.FindBuffIndex(type);
        Report("the mod buff stays on the player", index >= 0, $"index={index}");
        Report("Update runs for the player", TestBuff.PlayerUpdates >= 60 && p.statDefense >= defenseWithout + 7,
            $"calls={TestBuff.PlayerUpdates} defense {defenseWithout}->{p.statDefense}");
        Report("Update runs for an NPC", TestBuff.NpcUpdates > 0, $"calls={TestBuff.NpcUpdates} npc={npcIndex}");
        Main.playerInventory = false;
        Report("PreDraw and PostDraw run for the icon", TestBuff.PreDraws > 0 && TestBuff.PostDraws > 0 && TestBuff.LastPost.DrawColor.R == 255 &&
            TestBuff.LastPost.DrawColor.G == 80 && TestGlobalBuff.Hidden > 0,
            $"pre={TestBuff.PreDraws} post={TestBuff.PostDraws} color={TestBuff.LastPost.DrawColor.R},{TestBuff.LastPost.DrawColor.G} hidden={TestGlobalBuff.Hidden} at={TestBuff.LastPost.Position.X},{TestBuff.LastPost.Position.Y}");
        Report("GlobalBuff.Update runs for a game buff", TestGlobalBuff.VanillaUpdates >= 60, $"calls={TestGlobalBuff.VanillaUpdates}");
        if ((uint)npcIndex < 200u) Main.npc[npcIndex].active = false;

        if (index >= 0)
        {
            var times = p.buffTime;
            int before = times[index];
            TestBuff.BlockReApply = true;
            p.AddBuff(type, 5000);
            int blocked = times[index];
            TestBuff.BlockReApply = false;
            p.AddBuff(type, 5000);
            int renewed = times[index];
            Report("ReApply true keeps the game from renewing the time", blocked == before && renewed == 5000,
                $"before={before} blocked={blocked} renewed={renewed}");

            GUIBuffs gui = GUIBuffs.New();
            TestBuff.Keep = true;
            gui.RemoveBuff(index);
            bool kept = p.FindBuffIndex(type) >= 0;
            TestBuff.Keep = false;
            int regeneration = p.FindBuffIndex(BuffID.Regeneration);
            TestGlobalBuff.KeepVanilla = true;
            if (regeneration >= 0) gui.RemoveBuff(regeneration);
            bool keptVanilla = p.FindBuffIndex(BuffID.Regeneration) >= 0;
            TestGlobalBuff.KeepVanilla = false;
            gui.RemoveBuff(p.FindBuffIndex(type));
            bool removed = p.FindBuffIndex(type) < 0;
            Report("RightClick false keeps the buff, true lets it go", kept && keptVanilla && removed,
                $"kept={kept} keptVanilla={keptVanilla} removed={removed}");
        }
        p.ClearBuff(BuffID.Regeneration);
        Log.Info($"csbuff END pass={pass} fail={fail}");
    }

    static void Report(string name, bool ok, string detail)
    {
        if (ok) pass++;
        else fail++;
        Log.Info($"csbuff {name}: {(ok ? "ok" : "FAIL")} {detail}");
    }
}
