const { ProjectileID } = Terraria.ID;
const { SpriteEffects } = Microsoft.Xna.Framework.Graphics;
const GetLerpValue = Terraria.Utils['float GetLerpValue(float from, float to, float t, bool clamped)'];
const LightAt = Terraria.Lighting['Color GetColor(int x, int y)'];
const FillWhipControlPoints = Terraria.Projectile['void FillWhipControlPoints(Projectile proj, List`1 controlPoints, Player owner, bool getActualCollisionPoints)'];
const EntitySpriteDraw = Terraria.Main['void EntitySpriteDraw(Texture2D texture, Vector2 position, Rectangle sourceRectangle, Color color, float rotation, Vector2 origin, float scale, SpriteEffects effects, float worthless)'];
const Vector2List = System.Collections.Generic.List.makeGeneric(Vector2.Type);

export class ExampleWhipProjectile extends ModProjectile {
    constructor() {
        super();
        this.Texture = 'Projectiles/' + this.constructor.name;
    }

    SetStaticDefaults() {
        ProjectileID.Sets.IsAWhip[this.Type] = true;
    }

    SetDefaults() {
        this.DefaultToWhip();
    }

    PreDraw(proj) {
        const player = Terraria.Main.player[proj.owner];
        if (!this.points) {
            this.points = Vector2List.new();
            this.points['void .ctor()']();
        }
        this.points.Clear();
        FillWhipControlPoints(proj, this.points, player, true);
        const list = this.points.ToArray();

        const flip = proj.spriteDirection < 0 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;
        const texture = Terraria.GameContent.TextureAssets.Projectile[this.Type].Value;
        const frame = Rectangle.new(0, 0, 10, 26);
        const origin = Vector2.new(5, 8);
        const screen = Terraria.Main.screenPosition;
        let pos = list[0];
        const last = list.length - 1;
        for (let i = 0; i < last; i++) {
            let scale = 1;
            if (i === last - 1) {
                frame.Y = 74;
                frame.Height = 18;
                const t = proj.ai.val0 / (player.itemAnimationMax * proj.MaxUpdates);
                scale = MathHelper.Lerp(0.5, 1.5, GetLerpValue(0.1, 0.7, t, true) * GetLerpValue(0.9, 0.7, t, true));
            } else if (i > 10) {
                frame.Y = 58;
                frame.Height = 16;
            } else if (i > 5) {
                frame.Y = 42;
                frame.Height = 16;
            } else if (i > 0) {
                frame.Y = 26;
                frame.Height = 16;
            }
            const element = list[i];
            const diff = Vector2.Subtract(list[i + 1], element);
            const rotation = Vector2.ToRotation(diff) - MathHelper.PiOver2;
            const color = LightAt(Math.floor(element.X / 16), Math.floor(element.Y / 16));
            EntitySpriteDraw(texture, Vector2.Subtract(pos, screen), frame, color, rotation, origin, scale, flip, 0);
            pos = Vector2.Add(pos, diff);
        }
        return false;
    }
}
