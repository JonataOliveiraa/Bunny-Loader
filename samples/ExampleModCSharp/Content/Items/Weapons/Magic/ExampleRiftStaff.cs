using ExampleMod.Content.Projectiles;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace ExampleMod.Content.Items.Weapons.Magic;

// This staff shoots an ExampleRiftProjectile, which shows off a custom ModScreenEffect (ExampleRiftEffect) together with some of the ready-made ScreenPresets.
public class ExampleRiftStaff : ModItem
{
    // Uses the Example Magic Weapon sprite, recolored by Item.color below.
    public override string Texture => "ExampleMod/Content/Items/Weapons/ExampleMagicWeapon";

    public override void SetDefaults()
    {
        // DefaultToStaff sets the use style, use time, sound, shoot speed and mana cost of a typical staff. Here: 9 shoot speed, 40 use time and 20 mana.
        Item.DefaultToStaff(ModContent.ProjectileType<ExampleRiftProjectile>(), 9, 40, 20);
        Item.width = 34;
        Item.height = 40;
        Item.SetWeaponValues(35, 2, 4); // 35 damage, 2 knockback and 4% bonus critical strike chance.
        Item.rare = ItemRarityID.LightRed;
        Item.value = Item.sellPrice(0, 3, 0, 0);
        Item.UseSound = SoundID.Item8;
        Item.color = new Color(200, 140, 255); // Tints the item's sprite.
    }

    public override void AddRecipes()
    {
        CreateRecipe()
            .AddIngredient<ExampleItem>(10)
            .AddTile<Tiles.Furniture.ExampleWorkbench>()
            .Register();
    }
}
