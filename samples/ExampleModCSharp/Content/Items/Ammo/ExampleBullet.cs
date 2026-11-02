using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace ExampleMod.Content.Items.Ammo;

public class ExampleBullet : ModItem
{
    public override void SetStaticDefaults()
    {
        Item.ResearchUnlockCount = 99;
    }

    public override void SetDefaults()
    {
        Item.damage = 12;
        Item.DamageType = DamageClass.Ranged;
        Item.width = 8;
        Item.height = 8;
        Item.maxStack = Item.CommonMaxStack;
        Item.consumable = true;
        Item.knockBack = 1.5f;
        Item.value = 10;
        Item.rare = ItemRarityID.Green;
        Item.shoot = ModContent.ProjectileType<Projectiles.ExampleBullet>();
        Item.shootSpeed = 4.5f;
        Item.ammo = AmmoID.Bullet;
    }

    public override void AddRecipes()
    {
        CreateRecipe()
            .AddIngredient<ExampleItem>()
            .AddTile(TileID.WorkBenches)
            .Register();
    }
}
