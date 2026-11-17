using ExampleMod.Content.Subworlds;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace ExampleMod.Content.Items.Tools;

// This item takes the player to ExampleSubworld, and back to the main world when used inside it.
// ExampleAdvancedSubworldKey and ExampleWorldFileSubworldKey do the same for the other example subworlds.
public class ExampleSubworldKey : ModItem
{
    // Uses the vanilla Light Key sprite, so this item doesn't need a texture of its own.
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

    // Traveling is true while the player is already being moved between worlds, so the key can't be used twice in a row.
    public override bool CanUseItem(Player player) => !SubworldLoader.Traveling;

    public override bool? UseItem(Player player)
    {
        // Only the local player starts the trip. In multiplayer, only the host can start one; clients are refused.
        if (player.whoAmI == Main.myPlayer)
        {
            // Exit returns to the main world, and Enter starts the trip to the subworld.
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
