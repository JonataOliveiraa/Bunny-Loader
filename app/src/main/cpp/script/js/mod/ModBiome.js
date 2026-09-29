// Um bioma de mod, como no tModLoader: IsBiomeActive(player) diz se o jogador
// está nele (chamado a cada quadro, depois de o jogo contar os blocos em volta;
// a contagem vem pelo ModSystem.TileCountsAvailable). OnEnter e OnLeave na
// troca, OnInBiome a cada quadro dentro. Consulta: player.InModBiome(Classe).
// Padrões (no protótipo): Priority BiomeLow e Music 0, o silêncio, como no
// tModLoader; um bioma sem música própria declara Music = -1.
// BestiaryIcon e BackgroundPath: caminhos em Assets/Textures, por padrão o da
// classe + _Icon e + _Background (Content/Biomes/X.js -> Biomes/X_Background),
// como no tModLoader. O fundo do mapa pode reusar o do Bestiário:
// `get MapBackground() { return this.BackgroundPath; }`.
class ModBiome extends ModSceneEffect {
    IsBiomeActive(player) { return false; }

    OnEnter(player) {}
    OnInBiome(player) {}
    OnLeave(player) {}

    // O resultado do IsBiomeActive deste quadro; não repete a detecção.
    IsSceneEffectActive(player) {
        const flags = player.ModBiomeFlags;
        return !!(flags && flags[this.Type]);
    }
}
ModBiome.prototype.Music = 0;
ModBiome.prototype.Priority = SceneEffectPriority.BiomeLow;
ModBiome.prototype.BackgroundColor = null;   // a cor do fundo do Bestiário (Color), ou null
