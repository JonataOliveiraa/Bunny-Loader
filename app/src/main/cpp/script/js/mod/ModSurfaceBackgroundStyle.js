// Um fundo de superfície de mod, como no tModLoader. Aparece quando o
// ModSceneEffect (ou ModBiome) que o devolve em `SurfaceBackgroundStyle` ganha
// a cena do jogador. Slot: o número do estilo, depois dos 16 do jogo. As
// texturas vêm do BackgroundTextureLoader.GetBackgroundSlot.
class ModSurfaceBackgroundStyle {
    // fades: um por estilo (Main.bgAlphaFrontLayer). O padrão sobe o deste
    // estilo e desce os outros, o que o Example Mod do tModLoader escreve.
    ModifyFarFades(fades, transitionSpeed) {
        for (let i = 0; i < fades.length; i++) {
            fades[i] = i === this.Slot
                ? Math.min(1, fades[i] + transitionSpeed)
                : Math.max(0, fades[i] - transitionSpeed);
        }
    }

    // O número da textura, ou -1 para não desenhar essa camada.
    ChooseFarTexture() { return -1; }
    ChooseMiddleTexture() { return -1; }

    // false: não desenha a camada da frente (o mod desenha a dele aqui).
    PreDrawCloseBackground(spriteBatch) { return true; }

    // scale, parallax, a e b são Ref (.value), como os ref do tModLoader:
    // escala 1.25, parallax 0.37, e a altura = a × (posição da tela) + b.
    ChooseCloseTexture(scale, parallax, a, b) { return -1; }

    SetStaticDefaults() {}

    static register(cls) {
        if (typeof cls !== 'function' || !(cls.prototype instanceof ModSurfaceBackgroundStyle)) {
            throw new TypeError('ModSurfaceBackgroundStyle.register(Classe): passe a classe, que estende ModSurfaceBackgroundStyle');
        }

        const inst = new cls();
        Templates.Adopt(cls, inst);
        SurfaceBackgroundLoader.Add(inst);
        Safe.Run(cls.name + '.SetStaticDefaults', () => inst.SetStaticDefaults());
        return inst;
    }
}
