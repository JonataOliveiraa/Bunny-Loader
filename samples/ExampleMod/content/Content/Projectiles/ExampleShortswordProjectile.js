const { Main, Utils } = Terraria;
const { SpriteEffects } = Microsoft.Xna.Framework.Graphics;
const { TextureAssets } = Terraria.GameContent;
const GetLerpValue = Utils['float GetLerpValue(float from, float to, float t, bool clamped)'];
const LineHits = Terraria.Collision['bool CheckAABBvLineCollision(Vector2 objectPosition, Vector2 objectDimensions, Vector2 lineStart, Vector2 lineEnd, float lineWidth, ref float collisionPoint)'];

// A estocada da ExampleShortsword. Espada curta é um projétil: o jogo o
// desenha na mão e o dano sai dele, mais perto da ponta. Os valores são os
// da Espada Curta de Ferro.
export class ExampleShortswordProjectile extends ModProjectile {
    static FadeInDuration = 7;
    static FadeOutDuration = 4;
    static TotalDuration = 16;

    // A "largura" da lâmina.
    CollisionWidth(proj) { return 10 * proj.scale; }

    SetDefaults(proj) {
        proj.width = proj.height = 18;   // quadrado: importa quando o projétil gira
        proj.aiStyle = -1;               // a IA é a daqui
        proj.friendly = true;
        proj.penetrate = -1;
        proj.tileCollide = false;
        proj.scale = 1;
        proj.DamageType = DamageClass.Melee;
        proj.ownerHitCheck = true;       // não acerta através de blocos
        proj.extraUpdates = 1;           // 2 atualizações por quadro
        proj.timeLeft = 360;             // tanto faz: a IA o mata antes
        proj.usesOwnerLight = true;
    }

    AI(proj) {
        const player = Main.player[proj.owner];

        const timer = ++proj.ai[0];
        if (timer >= ExampleShortswordProjectile.TotalDuration) {
            proj.Kill();
            return;
        }
        // Para o desenho ficar "na" mão, nem na frente nem atrás do jogador.
        player.heldProj = proj.whoAmI;

        // Aparece e some aos poucos.
        const { FadeInDuration, FadeOutDuration, TotalDuration } = ExampleShortswordProjectile;
        proj.Opacity = GetLerpValue(0, FadeInDuration, timer, true) * GetLerpValue(TotalDuration, TotalDuration - FadeOutDuration, timer, true);

        // Presa à mão, avançando com a velocidade. Funciona porque o
        // ShouldUpdatePosition devolve false: o jogo não soma a velocidade à
        // posição depois da IA.
        const hand = player['Vector2 RotatedRelativePoint(Vector2 pos, bool reverseRotation, bool addGfxOffY, int pushFromOriginHack)'](player.MountedCenter, false, true, 0);
        proj.Center = Vector2.Add(hand, Vector2.Multiply(proj.velocity, timer - 1));

        // Virada para onde vai: o desenho aponta para cima e para a direita.
        proj.spriteDirection = proj.velocity.X >= 0 ? 1 : -1;
        proj.rotation = Vector2.ToRotation(proj.velocity) + MathHelper.PiOver2 - MathHelper.PiOver4 * proj.spriteDirection;
    }

    // false: a posição é do mod (a IA acima a põe a cada atualização).
    ShouldUpdatePosition(proj) {
        return false;
    }

    // Acerta tudo entre o jogador e a ponta da espada.
    Colliding(proj, projHitbox, targetHitbox) {
        const start = proj.Center;
        const end = Vector2.Add(start, Vector2.Multiply(proj.velocity, 6));
        return LineHits(Vector2.new(targetHitbox.X, targetHitbox.Y), Vector2.new(targetHitbox.Width, targetHitbox.Height),
            start, end, this.CollisionWidth(proj), new Ref(0));
    }

    // O desenho com o centro da textura no centro do projétil.
    PreDraw(proj, lightColor) {
        const texture = TextureAssets.Projectile[this.Type].Value;
        const origin = Vector2.new(texture.Width / 2, texture.Height / 2);
        const effects = proj.spriteDirection === -1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
        Main.spriteBatch['void Draw(Texture2D texture, Vector2 position, Nullable`1 sourceRectangle, Color color, float rotation, Vector2 origin, float scale, SpriteEffects effects, float layerDepth)'](
            texture, Vector2.Subtract(proj.Center, Main.screenPosition), null, proj['Color GetAlpha(Color newColor)'](lightColor),
            proj.rotation, origin, proj.scale, effects, 0);
        return false;
    }
}
