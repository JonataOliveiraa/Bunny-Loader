using ExampleMod.Content.Projectiles;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace ExampleMod.Content.Items.Weapons.Magic;

public class ExampleRiftStaff : ModItem
{
    public override string Texture => "ExampleMod/Content/Items/Weapons/ExampleMagicWeapon";

    public override void SetDefaults()
    {
        Item.DefaultToStaff(ModContent.ProjectileType<ExampleRiftProjectile>(), 9, 40, 20);
        Item.width = 34;
        Item.height = 40;
        Item.SetWeaponValues(35, 2, 4);
        Item.rare = ItemRarityID.LightRed;
        Item.value = Item.sellPrice(0, 3, 0, 0);
        Item.UseSound = SoundID.Item8;
        Item.color = new Color(200, 140, 255);
    }

    public override void AddRecipes()
    {
        CreateRecipe()
            .AddIngredient<ExampleItem>(10)
            .AddTile<Tiles.Furniture.ExampleWorkbench>()
            .Register();
    }
}
