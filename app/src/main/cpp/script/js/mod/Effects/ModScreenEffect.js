// Um efeito de tela: shader (GLSL) + ciclo de vida. O mod estende e o
// ScreenEffects cuida do resto: tempo, seguir uma entidade, fade, quadro a
// quadro. Em Content/ o registro é automático; o shader é o .frag ao lado do
// .js (Content/Effects/Rift.js -> Content/Effects/Rift.frag) ou `static
// Source`. Tamanhos em pixels do MUNDO (acompanham o zoom); tempo em ticks.
//
//   ScreenPresets.Twirl.Spawn(projectile, { radius: 96 });
//   class Rift extends ModScreenEffect { SetDefaults() { this.Radius = 140; } }
class ModScreenEffect {
    Shader = null;
    Layer = 0;                  // ScreenEffects.Layer
    Center = null;              // { X, Y } no mundo; null = tela inteira
    Follow = null;              // entidade: o efeito vai junto e morre com ela
    FollowType = undefined;
    Radius = 64;
    Intensity = 1;
    Color = null;               // Color do jogo ou [r, g, b, a] de 0 a 1 (uColor)
    Duration = 0;               // ticks; 0 = até Kill
    FadeIn = 0;
    FadeOut = 0;
    Key = null;                 // o mesmo Key renova o efeito em vez de empilhar
    Order = 0;                  // menor desenha antes
    CaptureMargin = 8;          // pixels do mundo além do Radius que o shader lê
    Fullscreen = false;
    Uniforms = {};
    Age = 0;
    Ended = false;

    #killed = false;

    SetDefaults() {}
    OnSpawn() {}
    // A cada quadro, antes do Draw (o tempo anda em Age).
    Update() {}
    ShouldDraw() { return true; }
    // As formas que o efeito soma no mapa de distorção do quadro (DistortionMap).
    DrawDistortion(d) {}
    // Os passos do efeito; o padrão desenha o shader dele na área dele.
    Draw(cmd) { if (this.Shader) cmd.DrawShader(); }
    OnKill() {}

    Kill() {
        this.#killed = true;
    }

    get Active() {
        return !this.#killed;
    }

    // 0 a 1 ao longo da Duration (0 sem Duration).
    get Progress() {
        return this.Duration > 0 ? Math.min(this.Age / this.Duration, 1) : 0;
    }

    get TimeLeft() {
        return this.Duration > 0 ? Math.max(this.Duration - this.Age, 0) : Infinity;
    }

    // O fade de entrada e de saída, de 0 a 1 (multiplica o uIntensity).
    get Opacity() {
        let opacity = 1;
        if (this.FadeIn > 0) opacity *= Math.min(this.Age / this.FadeIn, 1);
        if (this.Duration > 0 && this.FadeOut > 0) opacity *= Math.min(this.TimeLeft / this.FadeOut, 1);
        return opacity;
    }

    static Spawn(center = null, options = {}) {
        return ScreenEffects.Spawn(this, center, options);
    }

    static register(cls) {
        ScreenEffects.Register(cls);
    }
}
