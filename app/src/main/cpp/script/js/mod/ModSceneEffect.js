// Um efeito de cena, como no tModLoader: quando ativo para o jogador, disputa
// com os outros a música (e, nas próximas etapas, fundo, água e mapa) pela
// Priority + GetWeight. Os padrões ficam no protótipo: a classe do mod pode
// escrever `get Music() { ... }` (como no tModLoader) ou `this.Music = ...`.
class ModSceneEffect {
    // Music: MusicLoader.GetMusicSlot(...) ou um MusicID do jogo; -1 = não
    // escolhe música, 0 = silêncio. SurfaceBackgroundStyle e
    // UndergroundBackgroundStyle: a instância do estilo
    // (ModContent.GetInstance(Classe)), ou null. Os padrões ficam no
    // protótipo, logo abaixo da classe. Mod: o mod dono (posto no registro).

    // 0 a 1: desempata efeitos da mesma Priority.
    GetWeight(player) { return 0.5; }

    IsSceneEffectActive(player) { return false; }

    // Todo quadro, ativo ou não: é onde se liga e desliga um filtro de tela.
    SpecialVisuals(player, isActive) {}

    SetStaticDefaults() {}

    static register(cls) {
        if (typeof cls !== 'function' || !(cls.prototype instanceof ModSceneEffect)) {
            throw new TypeError('ModSceneEffect.register(Classe): passe a classe, que estende ModSceneEffect');
        }

        const inst = new cls();
        Templates.Adopt(cls, inst);
        if (inst instanceof ModBiome) BiomeLoader.Add(inst);
        SceneEffectLoader.Add(inst);
        Safe.Run(cls.name + '.SetStaticDefaults', () => inst.SetStaticDefaults());
        return inst;
    }
}
ModSceneEffect.prototype.Music = -1;
ModSceneEffect.prototype.SurfaceBackgroundStyle = null;
ModSceneEffect.prototype.UndergroundBackgroundStyle = null;
ModSceneEffect.prototype.Priority = SceneEffectPriority.None;
