using System;
using System.Collections.Generic;
using log4net;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CsFishing;

public static class Checks
{
    static int pass, fail, frames;
    static bool done;

    static ILog Log => ModContent.GetInstance<CsFishingMod>().Logger;

    static int Calls(string name) => FishingPlayer.Calls.GetValueOrDefault(name);

    public static void Reset()
    {
        frames = pass = fail = 0;
        done = false;
    }

    public static void Frame(Player p)
    {
        if (done || ++frames != 120) return;
        done = true;
        int held = p.selectedItem;
        Item heldItem = p.inventory[held], baitSlot = p.inventory[54];
        p.inventory[held] = new Item(ItemID.WoodFishingPole);
        p.inventory[54] = new Item(ItemID.MasterBait, 10);
        Projectile bobber = Main.projectile[999];
        int owner = bobber.owner;
        bobber.owner = Main.myPlayer;
        bobber.position = p.position;
        bobber.localAI[2] = ItemID.MasterBait;
        foreach (Action<Player, Projectile> step in new Action<Player, Projectile>[] { Level, Attempt, Catch, Caught, Bait })
        {
            try { step(p, bobber); }
            catch (Exception e) { Report(step.Method.Name, false, e.ToString()); }
        }
        bobber.owner = owner;
        p.inventory[held] = heldItem;
        p.inventory[54] = baitSlot;
        foreach (Action<Player> step in new Action<Player>[] { Angler, DyeTrader })
        {
            try { step(p); }
            catch (Exception e) { Report(step.Method.Name, false, e.ToString()); }
        }
        Log.Info($"csfishing END pass={pass} fail={fail}");
    }

    static int CountOf(Player p, int type)
    {
        int count = 0;
        for (int i = 0; i < 58; i++)
        {
            Item item = p.inventory[i];
            if (item.type == type) count += item.stack;
        }
        return count;
    }

    static int Spawn(Player p, int type) => NPC.NewNPC(default, (int)p.Center.X + 40, (int)p.Center.Y, type);

    static void Level(Player p, Projectile bobber)
    {
        FishingPlayer.LevelFactor = 1f;
        PlayerFishingConditions plain = p.GetFishingConditions();
        FishingPlayer.LevelFactor = 2f;
        PlayerFishingConditions doubled = p.GetFishingConditions();
        FishingPlayer.LevelFactor = 1f;
        int expected = (int)(plain.FinalFishingLevel / plain.LevelMultipliers * plain.LevelMultipliers * 2f);
        Report("GetFishingLevel gets the rod and the bait", FishingPlayer.RodType == ItemID.WoodFishingPole && FishingPlayer.BaitType == ItemID.MasterBait,
            $"rod={FishingPlayer.RodType} bait={FishingPlayer.BaitType}");
        Report("GetFishingLevel scales the level multiplier", plain.FinalFishingLevel > 0 && doubled.LevelMultipliers == plain.LevelMultipliers * 2f &&
            Math.Abs(doubled.FinalFishingLevel - plain.FinalFishingLevel * 2) <= 2,
            $"plain={plain.FinalFishingLevel} x{plain.LevelMultipliers} doubled={doubled.FinalFishingLevel} x{doubled.LevelMultipliers} expected~{expected}");
    }

    static void Attempt(Player p, Projectile bobber)
    {
        FishingPlayer.ForceCrate = true;
        var attempt = new FishingAttempt { rolledItemDrop = 0, fishingLevel = 30 };
        bobber.FishingCheck_ProbeForQuestFish(ref attempt);
        FishingPlayer.ForceCrate = false;
        Report("ModifyFishingAttempt changes the attempt but not the rolled drop", attempt.crate && attempt.rolledItemDrop == 0 &&
            Calls(nameof(FishingPlayer.ModifyFishingAttempt)) == 1,
            $"crate={attempt.crate} drop={attempt.rolledItemDrop} calls={Calls(nameof(FishingPlayer.ModifyFishingAttempt))}");
    }

    static void Catch(Player p, Projectile bobber)
    {
        bool sonar = p.sonarPotion;
        int before = PopupText.sonarText;
        FishingPlayer.CatchItem = ItemID.Bass;
        FishingPlayer.Sonar = true;
        p.sonarPotion = true;
        var attempt = new FishingAttempt { fishingLevel = 30, common = true, X = (int)(p.Center.X / 16), Y = (int)(p.Center.Y / 16) };
        try
        {
            if (Projectile._context.IsNull) Report("the game's fishing context exists", false, "null");
            bobber.PrepareFishingContext(Projectile._context, attempt);
            bobber.FishingCheck_RollItemDrop(ref attempt);
        }
        finally
        {
            p.sonarPotion = sonar;
            FishingPlayer.CatchItem = 0;
            FishingPlayer.Sonar = false;
        }
        Report("CatchFish sets the drop and the enemy", attempt.rolledItemDrop == ItemID.Bass && attempt.rolledEnemySpawn == -1 &&
            Calls(nameof(FishingPlayer.CatchFish)) == 1,
            $"drop={attempt.rolledItemDrop} npc={attempt.rolledEnemySpawn} calls={Calls(nameof(FishingPlayer.CatchFish))}");
        int text = PopupText.sonarText;
        bool shown = text >= 0 && text != before && PopupText.popupText[text].active && PopupText.popupText[text].name == "csfishing sonar";
        Report("CatchFish sonar shows the custom text", shown, $"sonarText={before}->{text}");
    }

    static void Caught(Player p, Projectile bobber)
    {
        int bass = CountOf(p, ItemID.Bass);
        bobber.AI_061_FishingBobber_GiveItemToPlayer(p, ItemID.Bass);
        Report("ModifyCaughtFish changes the caught stack", CountOf(p, ItemID.Bass) == bass + 3 && Calls(nameof(FishingPlayer.ModifyCaughtFish)) == 1,
            $"bass={bass}->{CountOf(p, ItemID.Bass)} calls={Calls(nameof(FishingPlayer.ModifyCaughtFish))}");
    }

    static void Bait(Player p, Projectile bobber)
    {
        FishingPlayer.BaitDecision = 0;
        bool found = p.ItemCheck_CheckFishingBobber_ConsumeBait(bobber, out int kept);
        int afterVeto = p.inventory[54].stack;
        FishingPlayer.BaitDecision = 1;
        p.ItemCheck_CheckFishingBobber_ConsumeBait(bobber, out int used);
        int afterForce = p.inventory[54].stack;
        FishingPlayer.BaitDecision = -1;
        Report("CanConsumeBait false keeps the bait", found && afterVeto == 10 && kept == ItemID.MasterBait, $"found={found} stack={afterVeto} used={kept}");
        Report("CanConsumeBait true always consumes it", afterForce == 9 && used == ItemID.MasterBait && Calls(nameof(FishingPlayer.CanConsumeBait)) == 2,
            $"stack={afterForce} used={used} calls={Calls(nameof(FishingPlayer.CanConsumeBait))}");
    }

    static void Angler(Player p)
    {
        int angler = Spawn(p, NPCID.Angler);
        int gel = CountOf(p, ItemID.Gel);
        p.GetAnglerReward(Main.npc[angler], ItemID.Bass);
        Main.npc[angler].active = false;
        Report("AnglerQuestReward gets the multiplier and the game's items", FishingPlayer.RewardMultiplier > 0.1f && FishingPlayer.RewardMultiplier <= 1f &&
            FishingPlayer.RewardCount > 0 && Calls(nameof(FishingPlayer.AnglerQuestReward)) == 1,
            $"multiplier={FishingPlayer.RewardMultiplier} items={FishingPlayer.RewardCount} calls={Calls(nameof(FishingPlayer.AnglerQuestReward))}");
        Report("AnglerQuestReward items reach the player", CountOf(p, ItemID.Gel) == gel + 13, $"gel={gel}->{CountOf(p, ItemID.Gel)}");
    }

    static void DyeTrader(Player p)
    {
        int trader = Spawn(p, NPCID.DyeTrader);
        int red = CountOf(p, ItemID.RedDye);
        p.GetDyeTraderReward(Main.npc[trader]);
        Main.npc[trader].active = false;
        int got = CountOf(p, ItemID.RedDye) - red;
        Report("GetDyeTraderReward gets the game's pool and its changes are used", FishingPlayer.DyePool != null && FishingPlayer.DyePool.Count > 0 && got > 0,
            $"pool={FishingPlayer.DyePool?.Count} got={got} calls={Calls(nameof(FishingPlayer.GetDyeTraderReward))}");
    }

    static void Report(string name, bool ok, string detail)
    {
        if (ok) pass++;
        else fail++;
        Log.Info($"csfishing {name}: {(ok ? "ok" : "FAIL")} {detail}");
    }
}
