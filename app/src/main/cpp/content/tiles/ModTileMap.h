#pragma once
#include <string>

namespace bl::runtime {

/**
 * O tile de mod no mapa, como no tModLoader (MapLoader + MapIO).
 *
 * Cor e nome: cada AddMapEntry vira uma entrada propria da tabela de cores do
 * mapa, depois da ultima do jogo (a do inferno, MapHelper.hellPosition), com a
 * cor pedida; o nome entra no Lang._mapLegendCache no mesmo indice. O
 * Lang.GetMapObjectName(indice) do jogo acha o nome certo sozinho.
 *
 * O `.map`: o jogo grava os pedacos do mapa crus, e na carga le
 * `traducao[Type]` sem conferir o limite (so traduz quando as contagens do
 * cabecalho mudam). Indice de mod no `.map` seria lido fora da tabela no jogo
 * sem o mod, ou com outros mods. Entao, na hora de gravar
 * (MapHelper.InternalSaveMapCompressed), as celulas de mod viram escuro — nao
 * exploradas — e voltam logo depois; as posicoes vao para `<mapa>.map.bl`,
 * pela chave "<uid>/<nome>" (o indice muda com os mods). Ao carregar
 * (WorldMap.Load), cada celula volta se o lugar continua escuro; se o jogo sem
 * o mod explorou ali nesse meio tempo, vale o que ele viu.
 */

/**
 * Uma entrada de mapa do tipo (cor e a chave do texto do nome, ja no
 * dicionario do jogo). Devolve a opcao dela (0, 1...), ou -1. THREAD-SAFE.
 */
int addModTileMapEntry(int type, int r, int g, int b, const std::string& nameKey);

int addModWallMapEntry(int type, int r, int g, int b, const std::string& nameKey);

/**
 * Poe as entradas nas tabelas do mapa (so na thread do jogo). `rebuilt`: o
 * jogo acabou de refazer as tabelas (MapHelper.Initialize).
 */
void applyModTileMap(int total, bool rebuilt);

/**
 * A celula (x, y) do mapa do mundo aberto — o Main.Map[x, y] do tModLoader:
 * o indice de cor (Type), a luz e a tinta. false fora do mapa ou sem mundo.
 */
bool readMapTile(int x, int y, int* type, int* light, int* paint);

/** Os hooks de gravar e carregar o mapa. Uma vez, na instalacao dos tiles. */
void installModTileMap();

} // namespace bl::runtime
