using System;
using BunnyLoader.Interop;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;

namespace Terraria.ModLoader.Core;

internal static class ItemLootHooks
{
    static bool wanted;
    [ThreadStatic] static NPC currentNPC;
    [ThreadStatic] static Player currentPlayer;
    [ThreadStatic] static int currentItem;

    internal static NPC CurrentNPC => currentNPC;
    internal static int CurrentItem => currentItem;
    internal static bool IsOpening(NPC npc) => currentItem > 0 && npc == currentNPC;

    internal static void Install(Func<string, bool> has, bool global)
    {
        if (!has(nameof(ModItem.ModifyItemLoot))) return;
        Enable();
    }

    internal static void Enable()
    {
        wanted = true;
        ItemHooks.Once("ItemLootDrops", () => On_CommonCode.DropItemFromNPC += DropItemFromNPC);
        ItemOpenHooks.Install(static name => name == nameof(ModItem.ModifyItemLoot), true);
    }

    internal static void ContentReady()
    {
        if (!wanted || Main.ItemDropsDB.IsNull) return;
        Item sample = Item.New();
        using var pin = new GameHandle(sample.Pointer);
        for (int type = 1; type < ItemID.Sets.OpenableBag.Length; type++)
        {
            sample.SetDefaults(type, default);
            var loot = new ItemLoot(type, Main.ItemDropsDB);
            ModItem? own = ItemLoader.InstanceOf(sample);
            if (own != null && ItemHooks.Overrides(own.GetType(), typeof(ModItem), nameof(ModItem.ModifyItemLoot)))
            {
                try { own.ModifyItemLoot(loot); }
                catch (Exception e) { ItemHooks.Report($"{own.FullName}.ModifyItemLoot", e); }
            }
            foreach (GlobalItem? global in ItemHooks.GlobalsOf(sample))
            {
                if (global == null) continue;
                if (!ItemHooks.Overrides(global.GetType(), typeof(GlobalItem), nameof(GlobalItem.ModifyItemLoot))) continue;
                try { if (global.AppliesToEntity(sample, false)) global.ModifyItemLoot(sample, loot); }
                catch (Exception e) { ItemHooks.Report($"{global.FullName}.ModifyItemLoot", e); }
            }
        }
    }

    internal static void Open(Item item, Player player)
    {
        if (!wanted || Main.netMode == 2) return;
        DropFromItem(Main.ItemDropSolver, new DropAttemptInfo { item = item.type, player = player, rng = Main.rand, IsExpertMode = Main.expertMode || ItemID.Sets.BossBag[item.type], IsMasterMode = Main.masterMode });
    }

    internal static void DropFromItem(ItemDropResolver resolver, DropAttemptInfo info)
    {
        var rules = resolver._database.GetRulesForItemID(info.item);
        if (rules.Count == 0) return;
        using var scope = new AttemptScope(info);
        foreach (IItemDropRule rule in rules) resolver.ResolveRule(rule, info);
    }

    internal readonly struct AttemptScope : IDisposable
    {
        readonly NPC outerNPC;
        readonly Player outerPlayer;
        readonly int outerItem;
        readonly GameHandle? pin;

        internal AttemptScope(DropAttemptInfo info)
        {
            outerNPC = currentNPC;
            outerPlayer = currentPlayer;
            outerItem = currentItem;
            pin = null;
            if (!info.npc.IsNull || info.item <= 0 || (currentItem == info.item && currentPlayer == info.player && !currentNPC.IsNull)) return;
            if (info.player.IsNull) throw new ArgumentException("Item drop attempts need a player.", nameof(info));
            Enable();
            NPC fake = NPC.New();
            pin = new GameHandle(fake.Pointer);
            fake.position = info.player.position;
            fake.width = info.player.width;
            fake.height = info.player.height;
            currentNPC = fake;
            currentPlayer = info.player;
            currentItem = info.item;
        }

        public void Dispose()
        {
            if (pin == null) return;
            currentNPC = outerNPC;
            currentPlayer = outerPlayer;
            currentItem = outerItem;
            pin.Dispose();
        }
    }

    static void DropItemFromNPC(On_CommonCode.orig_DropItemFromNPC orig, NPC npc, int itemId, int stack, bool scattered)
    {
        if (IsOpening(npc)) currentPlayer.QuickSpawnItem(currentPlayer.GetItemSource_OpenItem(currentItem), itemId, stack);
        else orig(npc, itemId, stack, scattered);
    }
}
