using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using BunnyLoader;
using BunnyLoader.Interop;
using Microsoft.Xna.Framework;
using Terraria.DataStructures;

namespace Terraria.ModLoader.Core;

internal static unsafe class PlayerFishingHooks
{
    sealed class CatchState
    {
        public FishingAttempt Attempt;
        public int Item;
        public int Npc;
        public AdvancedPopupRequest Sonar;
        public Vector2 Position;
    }

    sealed class RewardScope
    {
        public Player Player;
        public readonly List<Item> Items = new();
        public GetItemSettings Settings;
        public bool HasSettings;
        public float Multiplier = 1f;
    }

    static Player fishing;
    static bool caughtPending;
    static RewardScope? rewards;
    static bool loggedLevel;

    internal static void Install(Func<string, bool> has)
    {
        if (has(nameof(ModPlayer.GetFishingLevel))) ItemHooks.Once("PlayerFishingLevel", () => On_Player.GetFishingConditions += GetFishingConditions);
        if (has(nameof(ModPlayer.ModifyFishingAttempt))) ItemHooks.Once("PlayerFishingAttempt", () => On_Projectile.FishingCheck_ProbeForQuestFish += ProbeForQuestFish);
        if (has(nameof(ModPlayer.CatchFish))) ItemHooks.Once("PlayerCatchFish", () => On_Projectile.FishingCheck_RollItemDrop += RollItemDrop);
        if (has(nameof(ModPlayer.ModifyCaughtFish)))
            ItemHooks.Once("PlayerCaughtFish", () =>
            {
                On_Projectile.AI_061_FishingBobber_GiveItemToPlayer += GiveItemToPlayer;
                On_Player.QuickSpawnItem_IEntitySource_Item += QuickSpawnItem;
                On_Player.QuickSpawnItem_IEntitySource_Item_GetItemSettings += QuickSpawnItemWithSettings;
            });
        if (has(nameof(ModPlayer.CanConsumeBait)))
            ItemHooks.Once("PlayerConsumeBait", () =>
            {
                if (Host.Native()->OnConsumeBait(&ConsumeBait) == 0) Log.Error("player hooks: CanConsumeBait is not hooked");
            });
        if (has(nameof(ModPlayer.GetDyeTraderReward)))
            ItemHooks.Once("PlayerDyeReward", () =>
            {
                if (Host.Native()->OnDyeReward(&DyeReward) == 0) Log.Error("player hooks: GetDyeTraderReward is not hooked");
            });
        if (has(nameof(ModPlayer.AnglerQuestReward)))
            ItemHooks.Once("PlayerAnglerReward", () =>
            {
                On_Player.GetAnglerReward += GetAnglerReward;
                On_Player.GetAnglerRewardRarityMultiplier += GetAnglerRewardRarityMultiplier;
                On_Player.GetOrDropItem += GetOrDropItem;
            });
    }

    static Item Find(Player player, int type, ReadOnlySpan<int> first)
    {
        if (type <= 0) return default;
        var inventory = player.inventory;
        foreach (int i in first)
        {
            Item item = inventory[i];
            if (item.type == type && item.stack > 0) return item;
        }
        for (int i = 0; i < 58; i++)
        {
            Item item = inventory[i];
            if (item.type == type && item.stack > 0) return item;
        }
        return default;
    }

    static int BaseLevel(Player player, PlayerFishingConditions result)
    {
        int bonus = 0;
        if (player.FindBuffIndex(25) != -1) bonus += 5;
        if (player.canFloatInWater && player.wet) bonus += 5;
        if (player.sitting.isSitting) bonus += 5;
        int level = result.BaitPower + result.PolePower + player.fishingSkill + bonus;
        if ((int)(level * result.LevelMultipliers) == result.FinalFishingLevel || result.LevelMultipliers <= 0) return level;
        if (!loggedLevel)
        {
            loggedLevel = true;
            Log.Info($"player hooks: fishing level {result.FinalFishingLevel} is not {level} x {result.LevelMultipliers}; using the quotient");
        }
        return (int)MathF.Round(result.FinalFishingLevel / result.LevelMultipliers);
    }

    static PlayerFishingConditions GetFishingConditions(On_Player.orig_GetFishingConditions orig, Player self)
    {
        PlayerFishingConditions result = orig(self);
        if (result.BaitItemType == 2673 || result.BaitPower == 0 || result.PolePower == 0) return result;
        Item rod = Find(self, result.PoleItemType, stackalloc int[] { self.selectedItem });
        Item bait = Find(self, result.BaitItemType, stackalloc int[] { 54, 55, 56, 57 });
        float multiplier = result.LevelMultipliers;
        PlayerLoader.Each(self, nameof(ModPlayer.GetFishingLevel), (rod, bait), ref multiplier,
            static (m, s, ref v) => m.GetFishingLevel(s.rod, s.bait, ref v));
        if (!float.IsFinite(multiplier) || multiplier == result.LevelMultipliers) return result;
        int level = BaseLevel(self, result);
        result.LevelMultipliers = multiplier;
        result.FinalFishingLevel = Math.Max(0, (int)(level * multiplier));
        return result;
    }

    static void ProbeForQuestFish(On_Projectile.orig_FishingCheck_ProbeForQuestFish orig, Projectile self, ref FishingAttempt fisher)
    {
        int owner = self.owner;
        if ((uint)owner < 255u)
        {
            var box = new StrongBox<FishingAttempt>(fisher);
            PlayerLoader.Each(Main.player[owner], nameof(ModPlayer.ModifyFishingAttempt), box, static (m, b) => m.ModifyFishingAttempt(ref b.Value));
            int item = fisher.rolledItemDrop, npc = fisher.rolledEnemySpawn;
            fisher = box.Value;
            fisher.rolledItemDrop = item;
            fisher.rolledEnemySpawn = npc;
        }
        orig(self, ref fisher);
    }

    static void RollItemDrop(On_Projectile.orig_FishingCheck_RollItemDrop orig, Projectile self, ref FishingAttempt fisher)
    {
        orig(self, ref fisher);
        int owner = self.owner;
        if ((uint)owner >= 255u) return;
        Player player = Main.player[owner];
        var state = new CatchState { Attempt = fisher, Item = fisher.rolledItemDrop, Npc = fisher.rolledEnemySpawn, Position = self.position };
        PlayerLoader.Each(player, nameof(ModPlayer.CatchFish), state,
            static (m, s) => m.CatchFish(s.Attempt, ref s.Item, ref s.Npc, ref s.Sonar, ref s.Position));
        fisher.rolledItemDrop = state.Item;
        fisher.rolledEnemySpawn = state.Npc;
        if (!state.Sonar.Text.IsNull && player.sonarPotion)
            PopupText.AssignAsSonarText(PopupText.NewText(state.Sonar, state.Position));
    }

    static void GiveItemToPlayer(On_Projectile.orig_AI_061_FishingBobber_GiveItemToPlayer orig, Projectile self, Player thePlayer, int itemType)
    {
        Player outer = fishing;
        bool pending = caughtPending;
        fishing = thePlayer;
        caughtPending = true;
        try { orig(self, thePlayer, itemType); }
        finally
        {
            fishing = outer;
            caughtPending = pending;
        }
    }

    static void Caught(Player player, Item item)
    {
        if (!caughtPending || fishing != player) return;
        caughtPending = false;
        PlayerLoader.Each(player, nameof(ModPlayer.ModifyCaughtFish), item, static (m, i) => m.ModifyCaughtFish(i));
    }

    static void QuickSpawnItem(On_Player.orig_QuickSpawnItem_IEntitySource_Item orig, Player self, IEntitySource source, Item item)
    {
        Caught(self, item);
        orig(self, source, item);
    }

    static void QuickSpawnItemWithSettings(On_Player.orig_QuickSpawnItem_IEntitySource_Item_GetItemSettings orig, Player self, IEntitySource source, Item item, GetItemSettings settings)
    {
        Caught(self, item);
        orig(self, source, item, settings);
    }

    [UnmanagedCallersOnly]
    static int ConsumeBait(IntPtr player, IntPtr bait)
    {
        try
        {
            bool? decision = PlayerLoader.Nullable(new Player(player), nameof(ModPlayer.CanConsumeBait), new Item(bait), static (m, b) => m.CanConsumeBait(b));
            return decision is bool value ? (value ? 1 : 0) : -1;
        }
        catch (Exception e)
        {
            ItemHooks.Report("CanConsumeBait", e);
            return -1;
        }
    }

    [UnmanagedCallersOnly]
    static int DyeReward(IntPtr player, int* pool, int count, int capacity)
    {
        try
        {
            var list = new List<int>(count);
            for (int i = 0; i < count; i++) list.Add(pool[i]);
            PlayerLoader.Each(new Player(player), nameof(ModPlayer.GetDyeTraderReward), list, static (m, l) => m.GetDyeTraderReward(l));
            int n = Math.Min(list.Count, capacity);
            for (int i = 0; i < n; i++) pool[i] = list[i];
            return n;
        }
        catch (Exception e)
        {
            ItemHooks.Report("GetDyeTraderReward", e);
            return -1;
        }
    }

    static void GetAnglerReward(On_Player.orig_GetAnglerReward orig, Player self, NPC angler, int questItemType)
    {
        RewardScope? outer = rewards;
        var scope = new RewardScope { Player = self };
        rewards = scope;
        try { orig(self, angler, questItemType); }
        finally { rewards = outer; }
        try
        {
            PlayerLoader.Each(self, nameof(ModPlayer.AnglerQuestReward), scope, static (m, s) => m.AnglerQuestReward(s.Multiplier, s.Items));
        }
        finally
        {
            GetItemSettings settings = scope.HasSettings ? scope.Settings : GetItemSettings.NPCEntityToPlayerInventorySettings;
            foreach (Item item in scope.Items)
            {
                if (!item.IsNull && item.type > 0 && item.stack > 0) self.GetOrDropItem(item, settings);
            }
        }
    }

    static float GetAnglerRewardRarityMultiplier(On_Player.orig_GetAnglerRewardRarityMultiplier orig, int questsDone)
    {
        float value = orig(questsDone);
        if (rewards != null) rewards.Multiplier = value;
        return value;
    }

    static void GetOrDropItem(On_Player.orig_GetOrDropItem orig, Player self, Item item, GetItemSettings settings)
    {
        RewardScope? scope = rewards;
        if (scope == null || scope.Player != self)
        {
            orig(self, item, settings);
            return;
        }
        ItemLoader.KeepAlive(item);
        scope.Items.Add(item);
        scope.Settings = settings;
        scope.HasSettings = true;
    }
}
