using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace ExampleMod.Content.Items.Tools;

internal class ExampleHookItem : ModItem
{
    public override void SetDefaults()
    {
        Item.CloneDefaults(ItemID.AmethystHook);
        Item.shootSpeed = 18f;
        Item.shoot = ModContent.ProjectileType<ExampleHookProjectile>();
    }

    public override void AddRecipes()
    {
        CreateRecipe()
            .AddIngredient<ExampleItem>()
            .AddTile(TileID.WorkBenches)
            .Register();
    }
}

internal class ExampleHookProjectile : ModProjectile
{
    private static Asset<Texture2D> chainTexture;

    public override void Load()
    {
        chainTexture = ModContent.Request<Texture2D>("ExampleMod/Content/Items/Tools/ExampleHookChain");
    }

    public override void SetDefaults()
    {
        Projectile.CloneDefaults(ProjectileID.GemHookAmethyst);
    }

    public override bool? CanUseGrapple(Player player)
    {
        int hooksOut = 0;
        foreach (var projectile in Main.ActiveProjectiles)
        {
            if (projectile.owner == Main.myPlayer && projectile.type == Projectile.type)
            {
                hooksOut++;
            }
        }

        return hooksOut <= 2;
    }

    public override float GrappleRange()
    {
        return 500f;
    }

    public override void NumGrappleHooks(Player player, ref int numHooks)
    {
        numHooks = 2;
    }

    public override void GrappleRetreatSpeed(Player player, ref float speed)
    {
        speed = 18f;
    }

    public override void GrapplePullSpeed(Player player, ref float speed)
    {
        speed = 10;
    }

    public override void GrappleTargetPoint(Player player, ref float grappleX, ref float grappleY)
    {
        Vector2 dirToPlayer = Projectile.DirectionTo(player.Center);
        float hangDist = 50f;
        grappleX += dirToPlayer.X * hangDist;
        grappleY += dirToPlayer.Y * hangDist;
    }

    public override bool? GrappleCanLatchOnTo(Player player, int x, int y)
    {
        Tile tile = Main.tile[x, y];
        if (TileID.Sets.IsATreeTrunk[tile.TileType] || tile.TileType == TileID.PalmTree)
        {
            return true;
        }

        return null;
    }

    public override bool PreDrawExtras(Player player)
    {
        Vector2 playerCenter = player.MountedCenter;
        Vector2 center = Projectile.Center;
        Vector2 directionToPlayer = playerCenter - Projectile.Center;
        float chainRotation = directionToPlayer.ToRotation() - MathHelper.PiOver2;
        float distanceToPlayer = directionToPlayer.Length();

        while (distanceToPlayer > 20f && !float.IsNaN(distanceToPlayer))
        {
            directionToPlayer /= distanceToPlayer;
            directionToPlayer *= chainTexture.Height();

            center += directionToPlayer;
            directionToPlayer = playerCenter - center;
            distanceToPlayer = directionToPlayer.Length();

            Color drawColor = Lighting.GetColor((int)center.X / 16, (int)(center.Y / 16));

            Main.EntitySpriteDraw(chainTexture.Value, center - Main.screenPosition,
                chainTexture.Value.Bounds, drawColor, chainRotation,
                chainTexture.Size() * 0.5f, 1f, SpriteEffects.None, 0);
        }
        return false;
    }
}
