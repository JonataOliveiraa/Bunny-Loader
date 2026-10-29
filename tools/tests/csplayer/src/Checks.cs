using System;
using System.IO;
using log4net;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.IO;
using Terraria.ModLoader;

namespace CsPlayer;

public static class Checks
{
    static int pass, fail, frames, lifeBefore;
    static bool done;

    static ILog Log => ModContent.GetInstance<CsPlayerMod>().Logger;

    public static void Reset()
    {
        frames = pass = fail = 0;
        done = false;
    }

    public static void Frame(Player p)
    {
        if (done) return;
        frames++;
        try
        {
            if (frames == 120) First(p);
            else if (frames == 121) Life(p);
            else if (frames == 122) Rest(p);
        }
        catch (Exception e)
        {
            Report("run", false, e.ToString());
            Finish();
        }
    }

    static void First(Player p)
    {
        TestPlayer m = p.GetModPlayer<TestPlayer>();
        Report("GetModPlayer gives this player's instance", m.Player == p && m.Initialized && p.ModPlayers.Length >= 1 &&
            p.TryGetModPlayer(out TestPlayer again) && ReferenceEquals(again, m), $"initialized={m.Initialized} count={p.ModPlayers.Length}");
        Report("ResetEffects, PreUpdate and PostUpdate run every frame", TestPlayer.Resets >= 100 && TestPlayer.PreUpdates >= 100 && TestPlayer.PostUpdates >= 100,
            $"reset={TestPlayer.Resets} pre={TestPlayer.PreUpdates} post={TestPlayer.PostUpdates}");
        Report("PreUpdateBuffs and PostUpdateBuffs run", TestPlayer.PreBuffs > 0 && TestPlayer.PostBuffs > 0, $"pre={TestPlayer.PreBuffs} post={TestPlayer.PostBuffs}");
        Report("UpdateEquips and PostUpdateEquips run", TestPlayer.Equips > 0 && TestPlayer.PostEquips > 0, $"equips={TestPlayer.Equips} post={TestPlayer.PostEquips}");
        Report("UpdateBadLifeRegen and UpdateLifeRegen run", TestPlayer.BadRegen > 0 && TestPlayer.Regen > 0, $"bad={TestPlayer.BadRegen} regen={TestPlayer.Regen}");
        Report("FrameEffects runs", TestPlayer.Frames > 0, $"calls={TestPlayer.Frames}");
        Report("OnEnterWorld runs on entering", TestPlayer.EnterWorlds > 0, $"calls={TestPlayer.EnterWorlds}");
        lifeBefore = p.statLifeMax2 - p.statLifeMax;
        TestPlayer.ExtraLife = 40;
    }

    static void Life(Player p)
    {
        int now = p.statLifeMax2 - p.statLifeMax;
        TestPlayer.ExtraLife = 0;
        Report("ModifyMaxStats adds to the max life", now - lifeBefore == 40, $"before=+{lifeBefore} now=+{now}");
    }

    static void Rest(Player p)
    {
        p.RecalculateLuck();
        float plain = p.luck;
        TestPlayer.Lucky = true;
        p.RecalculateLuck();
        float lucky = p.luck;
        TestPlayer.Lucky = false;
        p.RecalculateLuck();
        Report("ModifyLuck changes the luck", Math.Abs(lucky - plain - 0.25f) < 0.001f, $"plain={plain} lucky={lucky}");

        Item torch = Item.New();
        torch.SetDefaults(ItemID.Torch, default);
        bool before = p.ItemCheck_CheckCanUse_Inner(torch, false);
        TestPlayer.Blocked = true;
        bool blocked = p.ItemCheck_CheckCanUse_Inner(torch, false);
        TestPlayer.Blocked = false;
        Report("CanUseItem refuses an item", before && !blocked, $"before={before} blocked={blocked}");

        bool god = p.creativeGodMode;
        int kills = TestPlayer.PreKills;
        TestPlayer.RefuseDeath = true;
        p.creativeGodMode = false;
        try { p.KillMe(PlayerDeathReason.ByCustomReason("csplayer"), 10, 0, false); }
        finally
        {
            p.creativeGodMode = god;
            TestPlayer.RefuseDeath = false;
        }
        Report("PreKill false keeps the player alive", TestPlayer.PreKills == kills + 1 && !p.dead, $"asked={TestPlayer.PreKills - kills} dead={p.dead}");

        p.UpdateDead();
        Report("UpdateDead runs", TestPlayer.Dead > 0, $"calls={TestPlayer.Dead}");
        int respawns = TestPlayer.Respawns;
        p.Spawn(PlayerSpawnContext.ReviveFromDeath);
        int alive = TestPlayer.Respawns;
        p.dead = true;
        p.Spawn(PlayerSpawnContext.ReviveFromDeath);
        Report("OnRespawn runs only when a dead player spawns", alive == respawns && TestPlayer.Respawns == respawns + 1 && !p.dead,
            $"entering={respawns} alive={alive} dead={TestPlayer.Respawns}");

        PlayerFileData data = Main.ActivePlayerFileData;
        FileData file = data;
        string path = file.Path ?? "";
        Player.SavePlayer(data, true, false);
        string json = File.Exists(path + ".bl.json") ? File.ReadAllText(path + ".bl.json") : "";
        string key = $"{ModContent.GetInstance<CsPlayerMod>().Uid}/{nameof(TestPlayer)}";
        Report("SaveData goes into the character's .bl.json", json.Contains(key) && json.Contains("\"saved\""), $"file={path}.bl.json json={json}");
        PlayerFileData loaded = Player.LoadPlayer(path, false);
        int saved = loaded.IsNull || loaded.Player.IsNull ? -2 : loaded.Player.GetModPlayer<TestPlayer>().Saved;
        Report("LoadData reads it back on LoadPlayer", saved == 7, $"saved={saved}");
        Content(p);
        Finish();
    }

    static void Content(Player p)
    {
        Item fruit = Item.New();
        fruit.SetDefaults(ModContent.ItemType<TestFruit>(), default);
        Item life = Item.New();
        life.SetDefaults(ItemID.LifeFruit, default);
        Report("CloneDefaults copies the game item and keeps the mod type", fruit.type == ModContent.ItemType<TestFruit>() && fruit.ModItem is TestFruit &&
            fruit.consumable && fruit.useStyle == life.useStyle && fruit.useTime == life.useTime && fruit.maxStack == life.maxStack && fruit.rare == life.rare,
            $"type={fruit.type} modItem={fruit.ModItem?.Name} useStyle={fruit.useStyle}/{life.useStyle} maxStack={fruit.maxStack}/{life.maxStack}");
        Report("ResearchUnlockCount goes into the sacrifice table", fruit.ResearchUnlockCount == 7 && life.ResearchUnlockCount > 0,
            $"mod={fruit.ResearchUnlockCount} lifeFruit={life.ResearchUnlockCount}");

        int baseLife = p.statLifeMax, baseMana = p.statManaMax;
        Report("ConsumedLifeCrystals and ConsumedManaCrystals follow the saved max", p.ConsumedLifeCrystals == (System.Math.Min(baseLife, 400) - 100) / 20 &&
            p.ConsumedManaCrystals == (baseMana - 20) / 20, $"statLifeMax={baseLife} crystals={p.ConsumedLifeCrystals} fruit={p.ConsumedLifeFruit} statManaMax={baseMana} mana={p.ConsumedManaCrystals}");
        int max2 = p.statLifeMax2, life2 = p.statLife;
        p.UseHealthMaxIncreasingItem(10);
        Report("UseHealthMaxIncreasingItem raises this frame's max, not the saved one", p.statLifeMax2 == max2 + 10 && p.statLife == life2 + 10 && p.statLifeMax == baseLife,
            $"max2 {max2}->{p.statLifeMax2} life {life2}->{p.statLife} statLifeMax={p.statLifeMax}");
        p.statLife = life2;
    }

    static void Finish()
    {
        done = true;
        Log.Info($"csplayer END pass={pass} fail={fail}");
    }

    static void Report(string name, bool ok, string detail)
    {
        if (ok) pass++;
        else fail++;
        Log.Info($"csplayer {name}: {(ok ? "ok" : "FAIL")} {detail}");
    }
}
