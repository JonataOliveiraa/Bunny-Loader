using ExampleMod.Common.Players;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace ExampleMod.Content.Items;

public class ExampleResourcePickup : ModItem
{
    public static readonly int ExampleResourceHealAmount = 50;

    public override LocalizedText Tooltip => LocalizedText.Empty;

    public override void SetStaticDefaults()
    {
        ItemID.Sets.ItemsThatShouldNotBeInInventory[Type] = true;
        ItemID.Sets.IgnoresEncumberingStone[Type] = true;
        ItemID.Sets.IsAPickup[Type] = true;
    }

    public override void SetDefaults()
    {
        Item.height = 12;
        Item.width = 12;
    }

    public override bool OnPickup(WorldItem item, Player player)
    {
        player.GetModPlayer<ExampleResourcePlayer>().HealExampleResource(ExampleResourceHealAmount);

        SoundEngine.PlaySound(SoundID.Grab, player.Center);

        return false;
    }

    public override void GrabRange(WorldItem item, Player player, ref int grabRange)
    {
        if (player.GetModPlayer<ExampleResourcePlayer>().exampleResourceMagnet)
        {
            grabRange += ExampleResourcePlayer.exampleResourceMagnetGrabRange;
        }
    }
}
