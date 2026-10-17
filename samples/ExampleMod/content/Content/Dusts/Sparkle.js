const AddLight = Terraria.Lighting['void AddLight(Vector2 position, float r, float g, float b)'];

// A poeira brilhante do ExampleMod: anda sozinha (Update devolve false) e
// some quando encolhe. A textura é Sparkle.png, ao lado deste arquivo.
export class Sparkle extends ModDust {
    OnSpawn(dust) {
        dust.velocity.X *= 0.4;   // nasce mais devagar
        dust.velocity.Y *= 0.4;
        dust.noGravity = true;    // sem gravidade
        dust.noLight = true;      // não emite a luz do jogo
        dust.scale *= 1.5;        // 50% maior
    }

    // Todo quadro em que a poeira está ativa.
    Update(dust) {
        dust.position.X += dust.velocity.X;
        dust.position.Y += dust.velocity.Y;
        dust.rotation += dust.velocity.X * 0.15;
        dust.scale *= 0.99;

        const light = 0.35 * dust.scale;
        AddLight(dust.position, light, light, light);

        if (dust.scale < 0.5) dust.active = false;
        return false;   // false: o jogo não move a poeira por conta própria
    }
}
