using ExampleMod.Content.Projectiles;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace ExampleMod.Content.Items.Tools;

public class ExampleDrill : ModItem
{
    public override void SetStaticDefaults()
    {
        ItemID.Sets.IsDrill[Type] = true;
    }

    public override void SetDefaults()
    {
        Item.damage = 27;
        Item.DamageType = DamageClass.MeleeNoSpeed;
        Item.width = 20;
        Item.height = 12;
        Item.useTime = 4;
        Item.useAnimation = 15;
        Item.useStyle = ItemUseStyleID.Shoot;
        Item.knockBack = 0.5f;
        Item.value = Item.buyPrice(gold: 12, silver: 60);
        Item.rare = ItemRarityID.LightRed;
        Item.UseSound = SoundID.Item23;
        Item.shoot = ModContent.ProjectileType<ExampleDrillProjectile>();
        Item.shootSpeed = 32f;
        Item.noMelee = true;
        Item.noUseGraphic = true;
        Item.channel = true;

        Item.tileBoost = 10;
        Item.pick = 190;
    }

    public override void AddRecipes()
    {
        CreateRecipe()
            .AddIngredient<ExampleItem>()
            .AddTile<Tiles.Furniture.ExampleWorkbench>()
            .Register();
    }
}
