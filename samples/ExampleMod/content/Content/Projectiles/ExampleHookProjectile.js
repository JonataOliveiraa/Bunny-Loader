const { ProjectileID, TileID } = Terraria.ID;
const { SpriteEffects } = Microsoft.Xna.Framework.Graphics;
const EntitySpriteDraw = Terraria.Main['void EntitySpriteDraw(Texture2D texture, Vector2 position, Rectangle sourceRectangle, Color color, float rotation, Vector2 origin, float scale, SpriteEffects effects, float worthless)'];
const LightAt = Terraria.Lighting['Color GetColor(int x, int y)'];

export class ExampleHookProjectile extends ModProjectile {
    SetDefaults() {
        this.CloneDefaults(ProjectileID.GemHookAmethyst);
    }

    // Para ganchos com vários no ar ao mesmo tempo (Gancho Duplo, Gancho de
    // Peixe, Gancho Lunar...): até 3 deste no ar.
    CanUseGrapple(player, type) {
        let hooksOut = 0;
        for (let i = 0; i < 1000; i++) {
            const p = Terraria.Main.projectile[i];
            if (p.active && p.owner === Terraria.Main.myPlayer && p.type === this.Type) hooksOut++;
        }
        return hooksOut <= 2;
    }

    // O alcance em pixels: longe assim, ele volta. O de Ametista é 300; o
    // Gancho Estático, 600.
    GrappleRange(proj) {
        return 500;
    }

    // Quantos ficam presos ao mesmo tempo (o mais velho solta).
    NumGrappleHooks(proj, player, numHooks) {
        numHooks.value = 2;
    }

    // A volta depois do alcance máximo (o jogo: 11; o Lunar: 24).
    GrappleRetreatSpeed(proj, player, speed) {
        speed.value = 18;
    }

    // O puxão no jogador, até o ponto onde o gancho prendeu.
    GrapplePullSpeed(proj, player, speed) {
        speed.value = 10;
    }

    // O ponto para onde o jogador é puxado: aqui, 50 px antes do bloco, para
    // ele ficar pendurado longe da parede.
    GrappleTargetPoint(proj, player, grappleX, grappleY) {
        const dirToPlayer = proj['Vector2 DirectionTo(Vector2 Destination)'](player.Center);
        const hangDist = 50;
        grappleX.value += dirToPlayer.X * hangDist;
        grappleY.value += dirToPlayer.Y * hangDist;
    }

    // Em que blocos ele prende: além dos de sempre, troncos de árvore, como o
    // Gancho de Esquilo. undefined = a regra do jogo.
    GrappleCanLatchOnTo(proj, player, tile) {
        if (TileID.Sets.IsATreeTrunk[tile.type] || tile.type === TileID.PalmTree) return true;
        return undefined;
    }

    // A corrente própria; false: sem a do jogo por baixo.
    PreDrawExtras(proj) {
        if (!this.chain) this.chain = bl.loadTexture('Content/Projectiles/ExampleHookChain.png');
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
        return false;
    }
}
