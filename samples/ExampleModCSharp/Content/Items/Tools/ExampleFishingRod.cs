using ExampleMod.Content.Projectiles;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace ExampleMod.Content.Items.Tools;

public class ExampleFishingRod : ModItem
{
    public override void SetStaticDefaults()
    {
        ItemID.Sets.CanFishInLava[Type] = true;
    }

    public override void SetDefaults()
    {
        Item.CloneDefaults(ItemID.WoodFishingPole);

        Item.fishingPole = 30;
        Item.shootSpeed = 12f;
        Item.shoot = ModContent.ProjectileType<ExampleBobber>();
    }

    public override void HoldItem(Player player)
    {
        player.accFishingLine = true;
    }

    public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
    {
        int bobberAmount = Main.rand.Next(3, 6);
        float spreadAmount = 75f;

        for (int index = 0; index < bobberAmount; ++index)
        {
            Vector2 bobberSpeed = velocity + new Vector2(Main.rand.NextFloat(-spreadAmount, spreadAmount) * 0.05f, Main.rand.NextFloat(-spreadAmount, spreadAmount) * 0.05f);

            Projectile.NewProjectile(source, position, bobberSpeed, type, 0, 0f, player.whoAmI);
        }
        return false;
    }

    public override void ModifyFishingLine(Projectile bobber, ref Vector2 lineOriginOffset, ref Color lineColor)
    {
        lineOriginOffset = new Vector2(43, -30);

        if (bobber.ModProjectile is ExampleBobber exampleBobber)
        {
            lineColor = exampleBobber.FishingLineColor;
        }
        else
        {
            lineColor = Main.DiscoColor;
        }
    }

    public override void AddRecipes()
    {
        CreateRecipe()
            .AddIngredient<ExampleItem>(10)
            .AddTile(TileID.WorkBenches)
            .Register();
    }
}
