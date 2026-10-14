// Lente: aumenta a imagem no centro (strength > 0, lupa) ou a encolhe
// (< 0, beliscão). Só distorce (DistortionMap.Radial), sem shader próprio.
//   ScreenPresets.Lens.Spawn(player, { radius: 100, strength: 20 });
class Lens extends ModScreenEffect {
    Strength = 16;   // deslocamento máximo, em pixels do mundo

    SetDefaults() {
        this.Radius = 100;
    }

    DrawDistortion(d) {
        d.Radial(this.Center, this.Radius, this.Strength);
    }

    Draw(cmd) {}
}
