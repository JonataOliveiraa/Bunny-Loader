using ExampleMod.Content.Items;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace ExampleMod.Content.Projectiles;

public class ExampleAdvancedAnimatedProjectile : ModProjectile
{
    public override void SetStaticDefaults()
    {
        Main.projFrames[Type] = 4;
    }

    public override void SetDefaults()
    {
        Projectile.width = 40;
        Projectile.height = 40;
        Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Melee;
        Projectile.ignoreWater = true;
        Projectile.tileCollide = false;
        Projectile.penetrate = -1;
        Projectile.alpha = 255;
    }

    public override Color? GetAlpha(Color lightColor) => new Color(255, 255, 255, 0) * Projectile.Opacity;

    public override void AI()
    {
        Projectile.ai[0] += 1f;
        FadeInAndOut();

        Projectile.velocity *= 0.98f;

        if (++Projectile.frameCounter >= 5)
        {
            Projectile.frameCounter = 0;
            if (++Projectile.frame >= Main.projFrames[Type])
                Projectile.frame = 0;
        }

        if (Projectile.ai[0] >= 60f)
            Projectile.Kill();

        Projectile.direction = Projectile.spriteDirection = (Projectile.velocity.X > 0f) ? 1 : -1;

        Projectile.rotation = Projectile.velocity.ToRotation();
        if (Projectile.spriteDirection == -1)
        {
            Projectile.rotation += MathHelper.Pi;
        }
    }

    public void FadeInAndOut()
    {
        if (Projectile.ai[0] <= 50f)
        {
            Projectile.alpha -= 25;
            if (Projectile.alpha < 100)
                Projectile.alpha = 100;

            return;
        }

        Projectile.alpha += 25;
        if (Projectile.alpha > 255)
            Projectile.alpha = 255;
    }

    public override bool PreDraw(Player player, ref Color lightColor)
    {
        SpriteEffects spriteEffects = SpriteEffects.None;
        if (Projectile.spriteDirection == -1)
            spriteEffects = SpriteEffects.FlipHorizontally;

        Texture2D texture = TextureAssets.Projectile[Type].Value;

        int frameHeight = texture.Height / Main.projFrames[Type];
        int startY = frameHeight * Projectile.frame;

        Rectangle sourceRectangle = new Rectangle(0, startY, texture.Width, frameHeight);

        Vector2 origin = sourceRectangle.Size() / 2f;

        float offsetX = 20f;
        origin.X = (float)(Projectile.spriteDirection == 1 ? sourceRectangle.Width - offsetX : offsetX);

        Color drawColor = Projectile.GetAlpha(lightColor);
        Main.EntitySpriteDraw(texture,
            Projectile.Center - Main.screenPosition + new Vector2(0f, Projectile.gfxOffY),
            sourceRectangle, drawColor, Projectile.rotation, origin, Projectile.scale, spriteEffects, 0);

        return false;
    }
}

internal class ExampleAdvancedAnimatedProjectileItem : ModItem
{
    public override string Texture => $"Terraria/Images/Item_{ItemID.NebulaBlaze}";

    public override void SetDefaults()
    {
        Item.CloneDefaults(ItemID.NebulaBlaze);
        Item.mana = 3;
        Item.damage = 3;
        Item.shoot = ModContent.ProjectileType<ExampleAdvancedAnimatedProjectile>();
    }

    public override void AddRecipes()
    {
        CreateRecipe()
            .AddIngredient<ExampleItem>()
            .AddTile<Tiles.Furniture.ExampleWorkbench>()
            .Register();
    }
}
