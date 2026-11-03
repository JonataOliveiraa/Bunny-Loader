using ExampleMod.Content.Projectiles;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace ExampleMod.Content.Items.Weapons;

internal class ExampleFlail : ModItem
{
    public override void SetStaticDefaults()
    {
        ItemID.Sets.ToolTipDamageMultiplier[Type] = 2f;
    }

    public override void SetDefaults()
    {
        Item.useStyle = ItemUseStyleID.Shoot;
        Item.useAnimation = 45;
        Item.useTime = 45;
        Item.knockBack = 6.75f;
        Item.width = 30;
        Item.height = 10;
        Item.damage = 32;
        Item.crit = 7;
        Item.scale = 1.1f;
        Item.noUseGraphic = true;
        Item.shoot = ModContent.ProjectileType<ExampleFlailProjectile>();
        Item.shootSpeed = 12f;
        Item.UseSound = SoundID.Item1;
        Item.rare = ItemRarityID.Orange;
        Item.value = Item.sellPrice(gold: 2, silver: 50);
        Item.DamageType = DamageClass.MeleeNoSpeed;
        Item.channel = true;
        Item.noMelee = true;
    }

    public override Color? GetAlpha(Color lightColor) => Color.White;

    public override void AddRecipes()
    {
        CreateRecipe()
            .AddIngredient<ExampleItem>()
            .AddTile<Tiles.Furniture.ExampleWorkbench>()
            .Register();
    }
}
