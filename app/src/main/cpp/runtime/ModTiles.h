#pragma once
#include <string>
#include <utility>
#include <vector>

#include "il2cpp/Types.h"

namespace bl::runtime {

/**
 * Tiles de mod: tipos NOVOS, com id depois dos do jogo — o mesmo modelo dos
 * buffs (ModBuffs.h), com o que e so do tile:
 *   - as tabelas por tipo (Main.tileSolid..., TileID.Sets..., TextureAssets,
 *     WorldGen, MapHelper, Recipe, TileMaterials) nascem com TileID.Count (753)
 *     e o tipo novo entra ZERADO nelas (o [0] e a terra);
 *   - Main.tileMerge e bool[753][753]: cada linha tambem cresce, e o tipo novo
 *     ganha linha propria (crescer so a de fora repetiria a linha da terra);
 *   - por instancia: Player.adjTile, GUICrafting.oldAdjTile e o
 *     SceneMetrics._tileCounts (a contagem dos biomas: sem crescer, contar um
 *     tile de mod escreve alem do fim);
 *   - o limite esta COMPILADO em PlaceTile, KillTile, TileFrame...: `tipo >
 *     752` recusado ou desviado. Trocado pelo total;
 *   - o mundo salvo NAO leva tile de mod (ModTileSave): o jogo sem o mod abre
 *     o mesmo mundo, sem tipo desconhecido.
 */

/** TileID.Count do jogo. E `const` no C#: vem do dump. */
constexpr int kVanillaTileCount = 753;

struct ModTileDef {
    std::string mod;       // uid do mod que registrou
    std::string name;      // chave estavel dentro do mod
    std::string texture;   // caminho absoluto do PNG (a folha de quadros 16x16 + 2)
};

/** Reserva o proximo id. THREAD-SAFE. -1 se o mesmo mod ja registrou esse nome. */
int registerModTile(ModTileDef def);

bool isModTile(int type);

/** Vanilla + tiles de mod JA instalados nas tabelas do jogo. */
int tileTypeCount();

/** Instala o registrado quando o jogo ja criou as tabelas; vigia as refeitas. Thread do jogo. */
void tickModTiles();

/** Tudo que foi registrado ja esta nas tabelas do jogo (ou falhou de vez). */
bool modTilesSettled();

/** Chamado uma vez por lote instalado (thread do jogo): a hora do SetStaticDefaults. */
using TilesInstalledHook = void (*)(int first, int last);
void setTilesInstalledHook(TilesInstalledHook hook);

/** O tipo do tile `name` do mod `mod`, ou -1. */
int modTileTypeByName(const std::string& mod, const std::string& name);

/** "<uid>/<nome>" do tile de mod `type`, ou "". */
std::string modTileKey(int type);

/** O tipo pela chave "<uid>/<nome>", ou -1 (mod nao carregado). */
int modTileTypeByKey(const std::string& key);

struct ModTileInfo {
    int type;
    std::string mod, name, texture;
};

/**
 * A cor do tile no mapa (o AddMapEntry). O mapa do jogo aponta cada tipo para
 * uma cor da tabela dele, e o `.map` salva esse INDICE: uma cor nova levaria
 * ao arquivo um indice que o jogo sem o mod nao conhece. Entao o tile de mod
 * usa a cor DO JOGO mais proxima da pedida. Sem cor: fora do mapa.
 */
void setModTileMapColor(int type, int r, int g, int b);

/** Os registrados, na ordem do id. Copia: serve a qualquer thread. */
std::vector<ModTileInfo> modTiles();

} // namespace bl::runtime
