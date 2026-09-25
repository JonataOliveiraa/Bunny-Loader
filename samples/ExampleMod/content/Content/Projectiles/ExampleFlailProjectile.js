const { ProjectileID } = Terraria.ID;
const { SpriteEffects } = Microsoft.Xna.Framework.Graphics;
const EntitySpriteDraw = Terraria.Main['void EntitySpriteDraw(Texture2D texture, Vector2 position, Rectangle sourceRectangle, Color color, float rotation, Vector2 origin, float scale, SpriteEffects effects, float worthless)'];

export class ExampleFlailProjectile extends ModProjectile {
    constructor() {
        super();
        this.Texture = 'Projectiles/' + this.constructor.name;
    }

    SetDefaults() {
        this.Projectile.width = this.Projectile.height = 22;
        this.Projectile.friendly = true;
        this.Projectile.penetrate = -1;
        this.Projectile.melee = true;
        this.Projectile.scale = 0.8;
        this.Projectile.drawLayer = 7;
        this.Projectile.usesLocalNPCImmunity = true;
        this.Projectile.localNPCHitCooldown = 10;
        this.Projectile.aiStyle = ProjAIStyleID.Flail;
        this.AIType = ProjectileID.Sunfury;
    }

    GetAlpha() {
        return Color.White;
    }

    PreDraw(proj) {
        if (proj.ai.val0 !== 1) return true;
        const texture = Terraria.GameContent.TextureAssets.Projectile[this.Type].Value;
        const frame = Rectangle.new(0, 0, texture.Width, texture.Height);
        const center = Vector2.new(proj.position.X + proj.width / 2, proj.position.Y + proj.height / 2 + proj.gfxOffY);
        const drawPosition = Vector2.Subtract(center, Terraria.Main.screenPosition);
        const origin = Vector2.new(texture.Width / 2, texture.Height / 2);
        const base = Color.Multiply(Color.new(255, 255, 255, 127), 0.5);
        const launchTimer = Math.min(proj.ai.val1, 5);
        const effects = proj.spriteDirection === 1 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;
        for (let transparency = 1; transparency >= 0; transparency -= 0.125) {
            const opacity = 1 - transparency;
            const offset = Vector2.Multiply(proj.velocity, -launchTimer * transparency);
            EntitySpriteDraw(texture, Vector2.Add(drawPosition, offset), frame, Color.Multiply(base, opacity),
                             proj.rotation, origin, proj.scale * 1.15 * MathHelper.Lerp(0.5, 1, opacity), effects, 0);
        }
        return true;
    }
}
