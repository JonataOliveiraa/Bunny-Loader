using ExampleMod.Content.Projectiles;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace ExampleMod.Content.Items.Weapons;

public class ExampleLastPrism : ModItem
{
    public override string Texture => "Terraria/Images/Item_" + ItemID.LastPrism;

    public static Color OverrideColor = new(122, 173, 255);

    public override void SetDefaults()
    {
        Item.CloneDefaults(ItemID.LastPrism);
        Item.mana = 4;
        Item.damage = 42;
        Item.shoot = ModContent.ProjectileType<ExampleLastPrismHoldout>();
        Item.shootSpeed = 30f;
        Item.color = OverrideColor;
    }

    public override void AddRecipes()
    {
        CreateRecipe()
            .AddIngredient<ExampleItem>(10)
            .AddTile<Tiles.Furniture.ExampleWorkbench>()
            .Register();
    }

    public override bool CanUseItem(Player player) => player.ownedProjectileCounts[ModContent.ProjectileType<ExampleLastPrismHoldout>()] <= 0;
}
