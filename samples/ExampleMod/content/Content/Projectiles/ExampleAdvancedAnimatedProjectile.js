const { SpriteEffects } = Microsoft.Xna.Framework.Graphics;
const LightAt = Terraria.Lighting['Color GetColor(int x, int y)'];
const EntitySpriteDraw = Terraria.Main['void EntitySpriteDraw(Texture2D texture, Vector2 position, Rectangle sourceRectangle, Color color, float rotation, Vector2 origin, float scale, SpriteEffects effects, float worthless)'];

export class ExampleAdvancedAnimatedProjectile extends ModProjectile {
    constructor() {
        super();
        this.Texture = 'Projectiles/' + this.constructor.name;
    }

    SetStaticDefaults() {
        Terraria.Main.projFrames[this.Type] = 4;
    }

    SetDefaults() {
        this.Projectile.width = 40;
        this.Projectile.height = 40;
        this.Projectile.friendly = true;
        this.Projectile.magic = true;
        this.Projectile.penetrate = -1;
        this.Projectile.ignoreWater = true;
        this.Projectile.tileCollide = false;
        this.Projectile.alpha = 255;
        this.Projectile.usesLocalNPCImmunity = true;
        this.Projectile.localNPCHitCooldown = -1;
    }

    GetAlpha(proj) {
        return Color.Multiply(Color.new(255, 255, 255, 0), proj.Opacity);
    }

    AI(proj) {
        const ai = new ProjAI(proj);
        ai[0]++;
        if (ai[0] <= 50) proj.alpha = Math.max(100, proj.alpha - 25);
        else proj.alpha = Math.min(255, proj.alpha + 25);

        proj.velocity = Vector2.Multiply(proj.velocity, 0.98);
        if (++proj.frameCounter >= 5) {
            proj.frameCounter = 0;
            if (++proj.frame >= Terraria.Main.projFrames[this.Type]) proj.frame = 0;
        }
        if (ai[0] >= 60) proj.Kill();

        proj.direction = proj.spriteDirection = proj.velocity.X > 0 ? 1 : -1;
        proj.rotation = Vector2.ToRotation(proj.velocity);
        if (proj.spriteDirection === -1) proj.rotation += Math.PI;
    }

    PreDraw(proj) {
        const effects = proj.spriteDirection === -1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
        const texture = Terraria.GameContent.TextureAssets.Projectile[this.Type].Value;
        const frameHeight = texture.Height / Terraria.Main.projFrames[this.Type];
        const source = Rectangle.new(0, frameHeight * proj.frame, texture.Width, frameHeight);
        const origin = Vector2.Multiply(Rectangle.Size(source), 0.5);
        origin.X = proj.spriteDirection === 1 ? source.Width - 20 : 20;
        const light = LightAt(Math.floor(proj.position.X / 16), Math.floor(proj.position.Y / 16));
        const color = this.GetAlpha(proj, light);
        const position = Vector2.Add(Vector2.Subtract(proj.Center, Terraria.Main.screenPosition), proj.gfxOffY);
        EntitySpriteDraw(texture, position, source, color, proj.rotation, origin, proj.scale, effects, 0);
        return false;
    }
}
