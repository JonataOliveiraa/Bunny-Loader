using System;
using System.Collections.Generic;
using System.Linq;
using BunnyLoader.Interop;
using CsNpcLoot.Content;
using log4net;
using Terraria;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;

namespace CsNpcLoot;

public static class Checks
{
    public static readonly List<string> Order = new();
    public static int DropsAtKill;
    static int frames, pass, fail;
    static bool done;
    static ILog Log => ModContent.GetInstance<CsNpcLootMod>().Logger;

    public static void Reset() { frames = pass = fail = 0; done = false; }

    public static void Frame(Player player)
    {
        if (done || ++frames != 120) return;
        done = true;
        try { Run(player); }
        catch (Exception e) { Report("run", false, e.ToString()); }
        finally
        {
            LootNpc.Allow = LootGlobal.Allow = ToggleCondition.Allow = true;
            for (int i = 0; i < 200; i++) if (Main.npc[i].type == ModContent.NPCType<LootNpc>() || Main.npc[i].type == ModContent.NPCType<BossNpc>() || Main.npc[i].type == ModContent.NPCType<LegacyBossNpc>()) Main.npc[i].active = false;
            Log.Info($"csnpcloot END pass={pass} fail={fail}");
        }
    }

    static void Run(Player player)
    {
        int type = ModContent.NPCType<LootNpc>();
        var loot = new NPCLoot(type, Main.ItemDropsDB);
        int setup = LootNpc.SetupCalls;
        Report("ModifyNPCLoot runs once for each mod NPC type", setup == 3, $"calls={setup}");
        Report("NPC loot uses initialized per-entity globals for mod and vanilla samples", InstanceLootGlobal.Valid && InstanceLootGlobal.Calls > 1, $"valid={InstanceLootGlobal.Valid} calls={InstanceLootGlobal.Calls}");
        Report("item loot uses initialized per-entity globals", InstanceBagGlobal.Valid && InstanceBagGlobal.Calls == 2, $"valid={InstanceBagGlobal.Valid} calls={InstanceBagGlobal.Calls}");
        Report("a global removes a native NPC rule", LootGlobal.Removed > 0 && !Main.ItemDropsDB.GetRulesForNPCID(NPCID.BlueSlime, false).OfType<DropBasedOnExtraGel>().Any(r => r.ruleForNormal is CommonDrop common && common.itemId == ItemID.Gel), $"removed={LootGlobal.Removed}");
        Report("global rules are included only on request", loot.Get().Count > loot.Get(false).Count, $"local={loot.Get(false).Count} all={loot.Get().Count}");

        ClearItems();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        NPC npc = Spawn(player, type);
        Order.Clear();
        npc.NPCLoot();
        Report("native CommonDrop creates the configured stack", Count(ItemID.Wood) == 2, $"wood={Count(ItemID.Wood)}");
        Report("a custom condition and success chain run in the native resolver", Count(ItemID.StoneBlock) == 3 && Count(ItemID.Acorn) == 1 && ToggleCondition.Calls > 0, $"stone={Count(ItemID.StoneBlock)} acorn={Count(ItemID.Acorn)} calls={ToggleCondition.Calls}");
        Report("OnFailedRoll executes its child", Count(ItemID.Torch) == 0 && Count(ItemID.IronBar) == 1, $"torch={Count(ItemID.Torch)} iron={Count(ItemID.IronBar)}");
        Report("LeadingConditionRule executes its child", Count(ItemID.SilverBar) == 1, $"silver={Count(ItemID.SilverBar)}");
        Report("a managed IItemDropRule produces real drops", Count(ItemID.Amethyst) == 2, $"amethyst={Count(ItemID.Amethyst)}");
        Report("ModifyGlobalLoot contributes to the native table", Count(ItemID.CopperBar) == 1, $"copper={Count(ItemID.CopperBar)}");
        Report("death hooks preserve order and OnKill sees the drops", string.Join(",", Order) == "PreG,PreG2,PreN,KillN,KillG" && DropsAtKill == 2, $"order={string.Join(",", Order)} woodAtKill={DropsAtKill}");

        ClearItems();
        ToggleCondition.Allow = false;
        npc.NPCLoot();
        Report("false conditions select OnFailedConditions", Count(ItemID.StoneBlock) == 0 && Count(ItemID.Acorn) == 0 && Count(ItemID.DirtBlock) == 4 && Count(ItemID.SilverBar) == 0, $"stone={Count(ItemID.StoneBlock)} dirt={Count(ItemID.DirtBlock)} silver={Count(ItemID.SilverBar)}");
        ToggleCondition.Allow = true;

        ClearItems();
        Order.Clear();
        LootGlobal.Allow = false;
        int kills = LootNpc.Kills;
        npc.NPCLoot();
        Report("a global PreKill veto calls the remaining globals and skips ModNPC and OnKill", string.Join(",", Order) == "PreG,PreG2" && LootNpc.Kills == kills && TotalItems() == 0, $"order={string.Join(",", Order)} items={TotalItems()}");
        LootGlobal.Allow = true;
        LootNpc.Allow = false;
        Order.Clear();
        npc.NPCLoot();
        Report("ModNPC.PreKill false prevents every drop and OnKill", string.Join(",", Order) == "PreG,PreG2,PreN" && LootNpc.Kills == kills && TotalItems() == 0, $"order={string.Join(",", Order)} items={TotalItems()}");
        LootNpc.Allow = true;

        var common = loot.Get(false).OfType<CommonDrop>().First(r => r.itemId == ItemID.Wood);
        common.amountDroppedMinimum = common.amountDroppedMaximum = 5;
        npc.NPCLoot();
        Report("editing a retrieved CommonDrop updates the native rule", Count(ItemID.Wood) == 5, $"wood={Count(ItemID.Wood)}");
        common.amountDroppedMinimum = common.amountDroppedMaximum = 2;
        ClearItems();
        npc.life = 1;
        npc.StrikeNPC(1000, 0f, 1);
        Report("a lethal native strike reaches loot and OnKill", !npc.active && Count(ItemID.Wood) == 2 && LootNpc.Kills > kills, $"active={npc.active} wood={Count(ItemID.Wood)} kills={LootNpc.Kills}");

        ClearItems();
        NPC slime = Spawn(player, NPCID.BlueSlime);
        slime.NPCLoot();
        Report("native NPC loot reflects removal and a global addition", Count(ItemID.Gel) == 0 && Count(ItemID.GoldBar) == 1, $"gel={Count(ItemID.Gel)} gold={Count(ItemID.GoldBar)}");
        slime.active = false;

        RatesAndFactories(player);
        Bags(player);
        ClearItems();
        NPC boss = Spawn(player, ModContent.NPCType<BossNpc>());
        BossNpc.Empty = false;
        boss.DoDeathEvents_DropBossPotionsAndHearts();
        int hearts = Count(ItemID.Heart) + Count(ItemID.CandyApple) + Count(ItemID.CandyCane);
        Report("BossLoot changes potion type, potion stack and heart count", BossNpc.Calls == 1 && Count(ItemID.Ruby) == 2 && hearts == 3, $"calls={BossNpc.Calls} ruby={Count(ItemID.Ruby)} hearts={hearts}");
        ClearItems();
        BossNpc.Empty = true;
        boss.DoDeathEvents_DropBossPotionsAndHearts();
        Report("BossLoot accepts zero potion and heart stacks", TotalItems() == 0, $"items={TotalItems()}");
        boss.active = false;
        NPC legacy = Spawn(player, ModContent.NPCType<LegacyBossNpc>());
        legacy.DoDeathEvents_DropBossPotionsAndHearts();
        Report("the one-parameter BossLoot override is reached through the compatibility bridge", LegacyBossNpc.Calls == 1 && Count(ItemID.Emerald) >= 5 && Count(ItemID.Emerald) <= 15, $"calls={LegacyBossNpc.Calls} emerald={Count(ItemID.Emerald)}");
        legacy.active = false;
        Report("loot hooks do not rerun setup during deaths or bag opening", LootNpc.SetupCalls == setup && LootBag.Calls == 1 && BagGlobal.Calls == 1, $"npc={LootNpc.SetupCalls} bag={LootBag.Calls} globalBag={BagGlobal.Calls}");
    }

    static void RatesAndFactories(Player player)
    {
        var nativeBag = (DropBasedOnExpertMode)ItemDropRule.BossBag(ItemID.KingSlimeBossBag);
        Report("BossBag preserves the native CommonDrop inheritance", nativeBag.ruleForExpertMode is CommonDrop bagDrop && bagDrop.itemId == ItemID.KingSlimeBossBag, $"rule={nativeBag.ruleForExpertMode.GetType().Name}");
        var rule = ItemDropRule.ByCondition(new ToggleCondition(), ItemID.Wood, 2, 2, 4);
        rule.OnSuccess(ItemDropRule.Common(ItemID.Acorn));
        var drops = new List<DropRateInfo>();
        rule.ReportDroprates(drops, new DropRateInfoChainFeed(1f));
        Report("native drop reporting converts rates, stacks and custom conditions", drops.Count == 2 && drops[0].itemId == ItemID.Wood && drops[0].stackMin == 2 && drops[0].stackMax == 4 && Math.Abs(drops[0].dropRate - 0.5f) < 0.001f && drops[0].conditions?.Count == 1 && drops[0].conditions![0] is ToggleCondition, $"count={drops.Count}");
        var custom = new CustomRule();
        var leading = new LeadingConditionRule(new ToggleCondition());
        leading.OnSuccess(custom);
        drops.Clear();
        leading.ReportDroprates(drops, new(1f));
        Report("native reporting calls a custom rule through its adapter", drops.Any(r => r.itemId == ItemID.Amethyst && r.conditions?.Count == 1), $"count={drops.Count}");

        NPC target = Spawn(player, ModContent.NPCType<LootNpc>());
        var info = new DropAttemptInfo { npc = target, player = player, rng = Main.rand };
        ClearItems();
        Main.ItemDropSolver.ResolveRule(ItemDropRule.OneFromOptions(1, ItemID.Wood, ItemID.StoneBlock), info);
        Report("OneFromOptions uses the native factory", Count(ItemID.Wood) + Count(ItemID.StoneBlock) == 1, $"items={TotalItems()}");
        ClearItems();
        Main.ItemDropSolver.ResolveRule(ItemDropRule.NormalvsExpert(ItemID.Wood, 1, 100000), info);
        Report("NormalvsExpert uses the native nested rule", Count(ItemID.Wood) == 1, $"wood={Count(ItemID.Wood)}");
        ClearItems();
        Main.ItemDropSolver.ResolveRule(ItemDropRule.MasterModeCommonDrop(ItemID.Wood), info);
        Report("MasterModeCommonDrop rejects a normal attempt", Count(ItemID.Wood) == 0, $"wood={Count(ItemID.Wood)}");
        Main.ItemDropSolver.ResolveRule(new NestedRule(), info);
        Report("a custom nested rule uses the native resolver callback", Count(ItemID.Amber) == 1, $"amber={Count(ItemID.Amber)}");
        var customParent = ItemDropRule.Common(ItemID.Wood);
        customParent.ChainedRules.Add(new CustomChain());
        Main.ItemDropSolver.ResolveRule(customParent, info);
        Report("a custom chain receives the parent result and resolves its child", CustomChain.Calls == 1 && Count(ItemID.Obsidian) == 1, $"calls={CustomChain.Calls} obsidian={Count(ItemID.Obsidian)}");
        drops.Clear();
        customParent.ReportDroprates(drops, new(1f));
        Report("a custom chain participates in native rate reporting", drops.Any(r => r.itemId == ItemID.Obsidian), $"count={drops.Count}");
        Main.ItemDropSolver.ResolveRule(new DerivedDrop(), info);
        Report("CommonDrop subclasses dispatch managed overrides", DerivedDrop.Calls == 1 && Count(ItemID.Diamond) == 0, $"calls={DerivedDrop.Calls} diamond={Count(ItemID.Diamond)}");
        Main.ItemDropSolver.ResolveRule(ItemDropRule.ByCondition(new DerivedCondition(), ItemID.Diamond), info);
        Report("native condition subclasses dispatch managed overrides", DerivedCondition.Calls == 1 && Count(ItemID.Diamond) == 1, $"calls={DerivedCondition.Calls} diamond={Count(ItemID.Diamond)}");
        var chainReport = new Chains.TryIfDoesntFillConditions(ItemDropRule.Common(ItemID.Wood));
        drops.Clear();
        chainReport.ReportDroprates(0.25f, drops, new(1f));
        Report("failed-condition chain reporting follows tModLoader", drops.Count == 1 && Math.Abs(drops[0].dropRate - 0.25f) < 0.001f, $"count={drops.Count}");
        target.active = false;
    }

    static void Bags(Player player)
    {
        int type = ModContent.ItemType<LootBag>(), reward = ModContent.ItemType<LootItem>();
        Item bag = Item.New();
        using var pin = new GameHandle(bag.Pointer);
        bag.SetDefaults(type, default);
        int before = Inventory(player, reward), diamond = Inventory(player, ItemID.Diamond);
        bool opened = ItemSlot.TryOpenContainer_GrantItems(bag, player);
        Report("ModifyItemLoot opens a mod bag and drops a mod item through the native rule", opened && Inventory(player, reward) - before == 3, $"opened={opened} reward={Inventory(player, reward) - before}");
        Report("item conditions receive item, player, expert mode and no NPC", BagCondition.SawContext && Inventory(player, ItemID.Diamond) - diamond == 1, $"context={BagCondition.SawContext} diamond={Inventory(player, ItemID.Diamond) - diamond}");
        var info = new DropAttemptInfo { item = type, player = player, rng = Main.rand, IsExpertMode = true };
        before = Inventory(player, reward);
        Main.ItemDropSolver.TryDropping(info);
        Report("TryDropping accepts a managed item attempt", Inventory(player, reward) - before == 3, $"reward={Inventory(player, reward) - before}");
        before = Inventory(player, ItemID.Topaz);
        Main.ItemDropSolver.ResolveRule(ItemDropRule.Common(ItemID.Topaz), info);
        Report("ResolveRule accepts a direct item attempt", Inventory(player, ItemID.Topaz) - before == 1, $"topaz={Inventory(player, ItemID.Topaz) - before}");
        before = Inventory(player, ItemID.Emerald);
        ItemDropRule.Common(ItemID.Emerald).TryDroppingItem(info);
        Report("a native rule accepts a direct item attempt", Inventory(player, ItemID.Emerald) - before == 1, $"emerald={Inventory(player, ItemID.Emerald) - before}");
        bag.SetDefaults(ItemID.KingSlimeBossBag, default);
        before = Inventory(player, ItemID.Sapphire);
        opened = ItemSlot.TryOpenContainer_GrantItems(bag, player);
        Report("GlobalItem.ModifyItemLoot adds to a vanilla boss bag", opened && Inventory(player, ItemID.Sapphire) - before == 4, $"opened={opened} sapphire={Inventory(player, ItemID.Sapphire) - before}");
        var loot = new ItemLoot(type, Main.ItemDropsDB);
        var added = loot.Add(ItemDropRule.Common(ItemID.Wood));
        int count = loot.Get().Count;
        loot.Remove(added);
        loot.RemoveWhere(r => r is CommonDrop c && c.itemId == reward);
        Report("ItemLoot Add, Get, Remove and RemoveWhere edit the item table", loot.Get().Count == count - 2, $"before={count} after={loot.Get().Count}");
    }

    static NPC Spawn(Player player, int type)
    {
        int index = NPC.NewNPC(player.GetSource_FromThis(), (int)player.Center.X + 300, (int)player.Center.Y - 100, type);
        NPC npc = Main.npc[index];
        npc.aiStyle = npc.damage = 0;
        npc.townNPC = true;
        npc.noGravity = npc.noTileCollide = true;
        return npc;
    }

    public static int Count(int type)
    {
        int count = 0;
        for (int i = 0; i < Main.item.Length; i++) if (Main.item[i].active && Main.item[i].type == type) count += Main.item[i].stack;
        return count;
    }
    static int Inventory(Player player, int type)
    {
        int count = Count(type);
        for (int i = 0; i < player.inventory.Length; i++) if (player.inventory[i].type == type) count += player.inventory[i].stack;
        return count;
    }
    static int TotalItems() { int count = 0; for (int i = 0; i < Main.item.Length; i++) if (Main.item[i].active) count += Main.item[i].stack; return count; }
    static void ClearItems() { for (int i = 0; i < Main.item.Length; i++) Main.item[i].TurnToAir(); }
    static void Report(string name, bool ok, string detail) { if (ok) pass++; else fail++; Log.Info($"csnpcloot {name}: {(ok ? "ok" : "FAIL")} {detail}"); }
}
