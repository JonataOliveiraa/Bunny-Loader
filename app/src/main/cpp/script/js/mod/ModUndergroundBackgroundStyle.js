// Um fundo de subsolo de mod, como no tModLoader. Aparece quando o
// ModSceneEffect (ou ModBiome) que o devolve em `UndergroundBackgroundStyle`
// ganha a cena do jogador. Slot: o número do estilo, depois dos 22 do jogo.
class ModUndergroundBackgroundStyle {
    // textureSlots[0]: a borda entre o céu e a terra (160x16); [1]: a terra;
    // [2]: a borda entre a terra e a pedra (160x16); [3]: a pedra (160x96).
    // [4]: a passagem para o inferno (vem com a do jogo). Números do
    // BackgroundTextureLoader.GetBackgroundSlot.
    FillTextureArray(textureSlots) {}

    SetStaticDefaults() {}

    static register(cls) {
        if (typeof cls !== 'function' || !(cls.prototype instanceof ModUndergroundBackgroundStyle)) {
            throw new TypeError('ModUndergroundBackgroundStyle.register(Classe): passe a classe, que estende ModUndergroundBackgroundStyle');
        }

        const inst = new cls();
        Templates.Adopt(cls, inst);
        UndergroundBackgroundLoader.Add(inst);
        Safe.Run(cls.name + '.SetStaticDefaults', () => inst.SetStaticDefaults());
        return inst;
    }
}
