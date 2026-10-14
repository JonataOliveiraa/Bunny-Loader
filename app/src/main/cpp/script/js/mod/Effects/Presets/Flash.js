// Clarão: a tela acende na cor do efeito e apaga sozinha. É um Tint com
// mistura Add e fim marcado; tudo do Tint vale (blend, mask, pulse...).
//   ScreenPresets.Flash.Spawn(null, { color: '#ffffff', duration: 10 });
//   ScreenPresets.Flash.Spawn(null, { color: Color.new(255, 80, 40), amount: 0.5, mask: ScreenUtils.Mask.Vignette() });
class Flash extends Tint {
    Blend = 5;
    Amount = 0.8;

    SetDefaults() {
        super.SetDefaults();
        this.Color = [1, 1, 1, 1];
        this.Duration = 12;
        this.FadeOut = 12;
    }
}
