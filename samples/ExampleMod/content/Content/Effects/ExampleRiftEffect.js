// Efeito de tela com shader próprio: o ExampleRiftEffect.frag ao lado é o
// shader (carregado sozinho pelo nome), Uniforms leva o uOpen até ele e o
// DrawDistortion gira e puxa a imagem em volta. Abre com um Tween no OnSpawn.
//   ExampleRiftEffect.Spawn(position);  // e /fx spawn examplerifteffect
export class ExampleRiftEffect extends ModScreenEffect {
    Open = 0;
    Spin = 1.6;
    Pull = 12;

    SetDefaults() {
        this.Radius = 110;
        this.Color = Color.new(180, 90, 255);
    }

    OnSpawn() {
        this.Tween('Open', 1, 25, ScreenUtils.Easing.OutBack);
    }

    Update() {
        this.Uniforms.uOpen = this.Open;
    }

    DrawDistortion(d) {
        d.Swirl(this.Center, this.Radius, this.Spin * this.Open);
        d.Radial(this.Center, this.Radius * 0.8, -this.Pull * this.Open);
    }

    // Fecha a fenda e morre.
    Close(ticks = 12) {
        this.Tween('Open', 0, ticks, ScreenUtils.Easing.InQuad, { onDone: () => this.Kill() });
    }
}
