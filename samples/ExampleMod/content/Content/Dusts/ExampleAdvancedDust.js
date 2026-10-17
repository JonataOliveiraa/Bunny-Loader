const { DustID } = Terraria.ID;

// Gira em volta de quem estiver no customData (um jogador). Nasce assim:
//   const dust = Dust.NewDustDirect(player.Center, 0, 0, ModContent.DustType(ExampleAdvancedDust), 0, 0, 0, Color.White, 2);
//   dust.rotation = Main.rand.NextFloat(6.28);
//   dust.customData = player;
//   dust.position = ... (o centro + o raio, para o primeiro quadro já sair no lugar)
// Como no tModLoader, o ExampleMod ainda não a usa: experimente no
// DrawEffects de um ModPlayer.
export class ExampleAdvancedDust extends ModDust {
    get Texture() { return null; }   // a folha de poeiras do jogo

    OnSpawn(dust) {
        dust.noGravity = true;
        const type = DustID.Confetti;
        dust.frame = Rectangle.new(type * 10 % 1000, Math.floor(type * 10 / 1000) * 30 + Rand.Next(3) * 10, 8, 8);
        dust.velocity.X = 0;
        dust.velocity.Y = 0;
    }

    Update(dust) {
        dust.rotation += 0.1 * (dust.dustIndex % 2 === 0 ? -1 : 1);
        dust.scale -= 0.05;

        const player = dust.customData;
        if (player && player.Center) {
            const center = player.Center;
            dust.position.X = center.X + Math.cos(dust.rotation) * dust.scale * 50;
            dust.position.Y = center.Y + Math.sin(dust.rotation) * dust.scale * 50;
        }

        if (dust.scale < 0.25) dust.active = false;
        return false;
    }
}
