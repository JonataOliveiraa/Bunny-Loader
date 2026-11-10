using ExampleMod.Content.Subworlds;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace ExampleMod.Content.Items.Tools;

public class ExampleSubworldKey : ModItem
{
    public override string Texture => $"Terraria/Images/Item_{ItemID.LightKey}";

    public override void SetDefaults()
    {
        Item.width = 22;
        Item.height = 30;
        Item.useTime = 30;
        Item.useAnimation = 30;
        Item.useStyle = ItemUseStyleID.HoldUp;
        Item.rare = ItemRarityID.Blue;
        Item.UseSound = SoundID.Item6;
    }

    public override bool CanUseItem(Player player) => !SubworldLoader.Traveling;

    public override bool? UseItem(Player player)
    {
        if (player.whoAmI == Main.myPlayer)
        {
            if (SubworldLoader.IsActive<ExampleSubworld>()) SubworldLoader.Exit();
            else SubworldLoader.Enter<ExampleSubworld>();
        }
        return true;
    }

    public override void AddRecipes()
    {
        CreateRecipe()
            .AddIngredient<ExampleItem>(10)
            .AddTile(TileID.WorkBenches)
            .Register();
    }
}
