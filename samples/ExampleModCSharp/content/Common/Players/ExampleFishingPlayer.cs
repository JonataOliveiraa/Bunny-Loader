using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace ExampleMod.Common.Players;

public class ExampleFishingPlayer : ModPlayer
{
    public bool hasExampleCrateBuff;

    public override void ResetEffects()
    {
        hasExampleCrateBuff = false;
    }

    public override void ModifyFishingAttempt(ref FishingAttempt attempt)
    {
        if (hasExampleCrateBuff && !attempt.crate)
        {
            if (Main.rand.Next(100) < 10)
            {
                attempt.crate = true;
            }
        }
    }

    public override bool? CanConsumeBait(Item bait)
    {
        PlayerFishingConditions conditions = Player.GetFishingConditions();

        if ((bait.type == ItemID.LadyBug || bait.type == ItemID.GoldLadyBug) && conditions.PoleItemType == ItemID.GoldenFishingRod)
        {
            return false;
        }

        return null;
    }

    public override void ModifyCaughtFish(Item fish)
    {
        if (Player.GetFishingConditions().BaitItemType == ItemID.LadyBug && fish.rare != ItemRarityID.Quest)
        {
            fish.stack += Main.rand.Next(1, 4);
        }
    }
}
