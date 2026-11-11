using ExampleMod.Content.Subworlds;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace ExampleMod.Content.Items.Tools;

public class ExampleWorldFileSubworldKey : ModItem
{
    public override string Texture => $"Terraria/Images/Item_{ItemID.ShadowKey}";

    public override void SetDefaults()
    {
        Item.width = 22;
        Item.height = 30;
        Item.useTime = 30;
        Item.useAnimation = 30;
        Item.useStyle = ItemUseStyleID.HoldUp;
        Item.rare = ItemRarityID.Orange;
        Item.UseSound = SoundID.Item6;
    }

    public override bool CanUseItem(Player player) => !SubworldLoader.Traveling;

    public override bool? UseItem(Player player)
    {
        if (player.whoAmI == Main.myPlayer)
        {
            if (SubworldLoader.IsActive<ExampleWorldFileSubworld>()) SubworldLoader.Exit();
            else SubworldLoader.Enter<ExampleWorldFileSubworld>();
        }
        return true;
    }

    public override void AddRecipes()
    {
        CreateRecipe()
            .AddIngredient<ExampleItem>(10)
            .AddIngredient(ItemID.StoneBlock, 20)
            .AddTile(TileID.WorkBenches)
            .Register();
    }
}
