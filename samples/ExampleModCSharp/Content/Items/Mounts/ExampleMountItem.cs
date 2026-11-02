using ExampleMod.Content.Mounts;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace ExampleMod.Content.Items.Mounts;

public class ExampleMountItem : ModItem
{
    public override void SetDefaults()
    {
        Item.width = 20;
        Item.height = 30;
        Item.useTime = 20;
        Item.useAnimation = 20;
        Item.useStyle = ItemUseStyleID.Swing;
        Item.value = Item.sellPrice(gold: 3);
        Item.rare = ItemRarityID.Green;
        Item.UseSound = SoundID.Item79;
        Item.noMelee = true;
        Item.mountType = ModContent.MountType<ExampleMount>();
    }

    public override void AddRecipes()
    {
        CreateRecipe()
            .AddIngredient<ExampleItem>()
            .AddTile(TileID.WorkBenches)
            .Register();
    }
}
