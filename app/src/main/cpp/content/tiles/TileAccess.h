#pragma once
#include <cstdint>

namespace bl::runtime {

/**
 * Os tiles do mundo, lidos direto da memoria do jogo.
 *
 * No celular o mundo nao e um Tile[,]: o Terraria.TileData guarda os tiles
 * INTERNADOS. Cada posicao (largura * y + x — o `_tileOffset` do struct Tile)
 * aponta, por TileLookup, para uma "definicao" compartilhada por todos os
 * tiles iguais; tipo, cabecalhos e quadro moram em arrays por definicao
 * (TileType, TileSHeader...). O `Tile.get_type` do jogo e exatamente
 * TileType[TileLookup[offset]].
 *
 * Os ponteiros mudam quando o mundo e (re)alocado: sao relidos a cada uso.
 */
struct TileArrays {
    int width = 0, height = 0;
    uint32_t* lookup = nullptr;     // posicao -> definicao
    uint16_t* type = nullptr;       // por definicao
    int16_t* sHeader = nullptr;     // bit 5 = ativo
    int16_t* frameX = nullptr;
    int16_t* frameY = nullptr;
    uint8_t* bHeader = nullptr;
    uint8_t* bHeader2 = nullptr;
    uint8_t* bHeader3 = nullptr;
};

/** Os arrays do mundo carregado; false se nao ha mundo. */
bool tileArrays(TileArrays* out);

/** Tipo do tile no offset do struct Tile, ou -1 (sem mundo, fora, inativo). */
int tileTypeAtOffset(int32_t offset);

/** Tipo do tile ativo em (x, y), ou -1. */
int tileTypeAt(int x, int y);

constexpr int16_t kTileActiveBit = 0x20;

} // namespace bl::runtime
