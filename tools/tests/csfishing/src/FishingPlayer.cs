using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CsFishing;

public class FishingPlayer : ModPlayer
{
    public static readonly Dictionary<string, int> Calls = new();
    public static float LevelFactor = 1f;
    public static int RodType = -1, BaitType = -1, CatchItem, BaitDecision = -1;
    public static bool ForceCrate, Sonar;
    public static float RewardMultiplier = -1f;
    public static int RewardCount = -1;
    public static List<int> DyePool;

    static void Count(string name) => Calls[name] = Calls.GetValueOrDefault(name) + 1;

    public override void GetFishingLevel(Item fishingRod, Item bait, ref float fishingLevel)
    {
        Count(nameof(GetFishingLevel));
        RodType = fishingRod.IsNull ? 0 : fishingRod.type;
        BaitType = bait.IsNull ? 0 : bait.type;
        fishingLevel *= LevelFactor;
    }

    public override void ModifyFishingAttempt(ref FishingAttempt attempt)
    {
        Count(nameof(ModifyFishingAttempt));
        if (!ForceCrate) return;
        attempt.crate = true;
        attempt.rolledItemDrop = 999;
    }

    public override void CatchFish(FishingAttempt attempt, ref int itemDrop, ref int npcSpawn, ref AdvancedPopupRequest sonar, ref Vector2 sonarPosition)
    {
        Count(nameof(CatchFish));
        if (CatchItem == 0) return;
        itemDrop = CatchItem;
        npcSpawn = -1;
        if (!Sonar) return;
        sonar.Text = "csfishing sonar";
        sonar.Color = Color.LimeGreen;
        sonar.DurationInFrames = 120;
        sonar.Velocity = Vector2.Zero;
        sonarPosition = new Vector2(Player.position.X, Player.position.Y - 64);
    }

    public override void ModifyCaughtFish(Item fish)
    {
        Count(nameof(ModifyCaughtFish));
        if (fish.type == ItemID.Bass) fish.stack += 2;
    }

    public override bool? CanConsumeBait(Item bait)
    {
        Count(nameof(CanConsumeBait));
        return BaitDecision < 0 ? null : BaitDecision == 1;
    }

    public override void AnglerQuestReward(float rareMultiplier, List<Item> rewardItems)
    {
        Count(nameof(AnglerQuestReward));
        RewardMultiplier = rareMultiplier;
        RewardCount = rewardItems.Count;
        rewardItems.Add(new Item(ItemID.Gel, 13));
    }

    public override void GetDyeTraderReward(List<int> rewardPool)
    {
        Count(nameof(GetDyeTraderReward));
        DyePool = new List<int>(rewardPool);
        rewardPool.Clear();
        rewardPool.Add(ItemID.RedDye);
    }
}
