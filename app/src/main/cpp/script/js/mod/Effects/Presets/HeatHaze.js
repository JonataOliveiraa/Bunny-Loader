// Ar quente: a imagem ondula em volta do centro, como em cima do fogo. Só
// distorce (DistortionMap.Haze), sem shader próprio.
//   ScreenPresets.HeatHaze.Spawn(player, { radius: 80, strength: 3, duration: 30, fadeOut: 15 });
class HeatHaze extends ModScreenEffect {
    Strength = 2.5;   // deslocamento máximo, em pixels do mundo
    Scale = 40;       // ondas por altura da tela
    Speed = 1.5;      // o quanto sobe por segundo

    SetDefaults() {
        this.Radius = 80;
    }

    DrawDistortion(d) {
        d.Haze(this.Center, this.Radius, this.Strength, { scale: this.Scale, speed: this.Speed });
    }

    Draw(cmd) {}
}
