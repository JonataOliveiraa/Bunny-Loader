// Efeito de tela ligado ao jogador: com menos de 25% da vida, as bordas da
// tela ficam vermelhas e pulsam; ao se curar, o efeito some aos poucos
// (Tween) e morre. Só no aparelho do próprio jogador.
export class ExampleLowLifePlayer extends ModPlayer {
    static Threshold = 0.25;

    Vignette = null;

    PostUpdate(player) {
        if (player.whoAmI !== Terraria.Main.myPlayer) return;

        const low = !player.dead && player.statLife < player.statLifeMax2 * ExampleLowLifePlayer.Threshold;
        if (low && (!this.Vignette || !this.Vignette.Active)) {
            this.Vignette = ScreenPresets.Tint.Spawn(null, {
                color: Color.new(200, 20, 20),
                blend: ScreenGraphics.BlendMode.Multiply,
                amount: 0.6,
                saturation: 0.6,
                mask: ScreenUtils.Mask.Vignette({ start: 0.35, softness: 0.55 }),
                pulse: { period: 50, amount: 0.4 },
                fadeIn: 20,
                key: 'ExampleMod:lowLife',
            });
        } else if (!low && this.Vignette && this.Vignette.Active && !this.Vignette.Fading) {
            const fx = this.Vignette;
            fx.Fading = true;
            fx.Tween('Intensity', 0, 30, ScreenUtils.Easing.OutQuad, { onDone: () => fx.Kill() });
        }
    }
}
