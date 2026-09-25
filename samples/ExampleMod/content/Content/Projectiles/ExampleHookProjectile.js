const { ProjectileID, TileID } = Terraria.ID;
const { SpriteEffects } = Microsoft.Xna.Framework.Graphics;
const EntitySpriteDraw = Terraria.Main['void EntitySpriteDraw(Texture2D texture, Vector2 position, Rectangle sourceRectangle, Color color, float rotation, Vector2 origin, float scale, SpriteEffects effects, float worthless)'];
const LightAt = Terraria.Lighting['Color GetColor(int x, int y)'];

export class ExampleHookProjectile extends ModProjectile {
    GrappleRange = 300;
    NumGrappleHooks = 2;

    constructor() {
        super();
        this.Texture = 'Projectiles/' + this.constructor.name;
    }

    SetDefaults() {
        this.CloneDefaults(ProjectileID.GemHookAmethyst);
    }

    AI(proj) {
        const owner = Terraria.Main.player[proj.owner];
        if (owner['float Distance(Vector2 Other)'](proj.Center) > this.GrappleRange) proj.ai.val0 = 1;
    }

    UseGrapple(player, type) {
        if (player.ownedProjectileCounts[type] < this.NumGrappleHooks) return type;
        let oldest = null;
        for (let i = 0; i < 1000; i++) {
            const p = Terraria.Main.projectile[i];
            if (p.active && p.type === type && p.owner === player.whoAmI && (!oldest || p.timeLeft < oldest.timeLeft)) oldest = p;
        }
        if (oldest) oldest.Kill();
        return type;
    }

    GrappleCanLatchOnTo(proj, player, tile) {
        if (TileID.Sets.IsATreeTrunk[tile.type] || tile.type === TileID.PalmTree) return true;
        return undefined;
    }

    PreDraw(proj) {
        if (!this.chain) this.chain = bl.loadTexture('Textures/Projectiles/ExampleHookChain.png');
        const chain = this.chain;
        const frame = Rectangle.new(0, 0, chain.Width, chain.Height);
        const origin = Vector2.new(chain.Width / 2, chain.Height / 2);
        const screen = Terraria.Main.screenPosition;
        const hand = Terraria.Main.player[proj.owner].MountedCenter;
        let center = proj.Center;
        let toPlayer = Vector2.Subtract(hand, center);
        const rotation = Vector2.ToRotation(toPlayer) - MathHelper.PiOver2;
        let distance = Vector2.Length(toPlayer);
        while (distance > 20 && !Number.isNaN(distance)) {
            center = Vector2.Add(center, Vector2.Multiply(toPlayer, chain.Height / distance));
            toPlayer = Vector2.Subtract(hand, center);
            distance = Vector2.Length(toPlayer);
            const color = LightAt(Math.floor(center.X / 16), Math.floor(center.Y / 16));
            EntitySpriteDraw(chain, Vector2.Subtract(center, screen), frame, color, rotation, origin, 1, SpriteEffects.None, 0);
        }
        return true;
    }
}
