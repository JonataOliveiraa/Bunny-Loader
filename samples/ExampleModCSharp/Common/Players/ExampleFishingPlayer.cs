using ExampleMod.Content.Items;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace ExampleMod.Common.Players;

// This class showcases things you can do with fishing
public class ExampleFishingPlayer : ModPlayer
{
    public bool hasExampleCrateBuff;

    public override void ResetEffects()
    {
        hasExampleCrateBuff = false;
    }

    public override void ModifyFishingAttempt(ref FishingAttempt attempt)
    {
        // If the player has the Example Crate buff (given by Example Crate Potion), 10% additional chance that the catch will be a crate
        // The "tier" of the crate depends on the rarity, which we don't modify here, see the comments in CatchFish for details
        if (hasExampleCrateBuff && !attempt.crate)
        {
            if (Main.rand.Next(100) < 10)
            {
                attempt.crate = true;
            }
        }
    }

    public override void CatchFish(FishingAttempt attempt, ref int itemDrop, ref int npcSpawn, ref AdvancedPopupRequest sonar, ref Vector2 sonarPosition)
    {
        bool inWater = !attempt.inLava && !attempt.inHoney;

        // Here we will set the catch conditions for our ExampleQuestFish
        // First we check if today's quest matches our quest fish
        // Our ExampleQuestFish states that it can only be caught whilst upside-down, so we'll have to check the gravity
        // Normal gravity is positive, whilst reversed gravity is negative
        // Finally, most vanilla quest fish only appear on an uncommon roll, so we'll do the same
        if (inWater && Player.gravDir == -1f && attempt.uncommon && attempt.questFish == ModContent.ItemType<ExampleQuestFish>())
        {
            itemDrop = ModContent.ItemType<ExampleQuestFish>();
        }
    }

    public override bool? CanConsumeBait(Item bait)
    {
        // Player.GetFishingConditions() returns you the best fishing pole Item, type and power, the best bait Item, type and Power, and the total fishing level, including modded values
        // These are the same Pole and Bait the game considers when calculating the obtained fish.
        // during CanConsumeBait, Player.GetFishingConditions() == attempt.playerFishingConditions from CatchFish.
        PlayerFishingConditions conditions = Player.GetFishingConditions();

        // The golden fishing rod will never consume a ladybug
        if ((bait.type == ItemID.LadyBug || bait.type == ItemID.GoldLadyBug) && conditions.PoleItemType == ItemID.GoldenFishingRod)
        {
            return false;
        }

        return null; // Let the default logic run
    }

    // If fishing with ladybug, we will receive multiple "fish" per bobber. Does not apply to quest fish
    public override void ModifyCaughtFish(Item fish)
    {
        // In this example, we make sure that we got a Ladybug as bait, and later on use that to determine what we catch
        if (Player.GetFishingConditions().BaitItemType == ItemID.LadyBug && fish.rare != ItemRarityID.Quest)
        {
            fish.stack += Main.rand.Next(1, 4);
        }
    }
}
