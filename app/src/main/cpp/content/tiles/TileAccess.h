#pragma once
#include <cstdint>

namespace bl::runtime {

struct TileArrays {
    int width = 0, height = 0;
    uint32_t* lookup = nullptr;
    uint16_t* type = nullptr;
    int16_t* sHeader = nullptr;
    int16_t* frameX = nullptr;
    int16_t* frameY = nullptr;
    uint8_t* bHeader = nullptr;
    uint8_t* bHeader2 = nullptr;
    uint8_t* bHeader3 = nullptr;
    uint16_t* wall = nullptr;
};

struct TileTable {
    const uint8_t* data = nullptr;
    int count = 0;
};

bool tileArrays(TileArrays* out);

bool tileLightedTable(TileTable* out);

bool tileSolidTable(TileTable* out);

bool worldTileSize(int* width, int* height);

bool worldGrowsGrassUnderground();

int tileTypeAt(int x, int y);

int wallTypeAt(int x, int y);

constexpr int16_t kTileActiveBit = 0x20;

constexpr uint8_t kWallInvisibleBit = 0x20;

}
