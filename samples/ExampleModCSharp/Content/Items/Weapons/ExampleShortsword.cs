using ExampleMod.Content.Projectiles;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace ExampleMod.Content.Items.Weapons;

public class ExampleShortsword : ModItem
{
    public override void SetDefaults()
    {
        Item.damage = 8;
        Item.knockBack = 4f;
        Item.useStyle = ItemUseStyleID.Rapier;
        Item.useAnimation = 12;
        Item.useTime = 12;
        Item.width = 32;
        Item.height = 32;
        Item.UseSound = SoundID.Item1;
        Item.DamageType = DamageClass.MeleeNoSpeed;
        Item.autoReuse = false;
        Item.noUseGraphic = true;
        Item.noMelee = true;

        Item.rare = ItemRarityID.White;
        Item.value = Item.sellPrice(0, 0, 0, 10);

        Item.shoot = ModContent.ProjectileType<ExampleShortswordProjectile>();
        Item.shootSpeed = 2.1f;
    }

    public override bool MeleePrefix() => true;

    public override void AddRecipes()
    {
        CreateRecipe()
            .AddIngredient<ExampleItem>()
            .AddTile<Tiles.Furniture.ExampleWorkbench>()
            .Register();
    }
}
