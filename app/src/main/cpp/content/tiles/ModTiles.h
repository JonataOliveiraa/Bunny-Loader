#pragma once
#include <atomic>
#include <cstdint>
#include <string>
#include <utility>
#include <vector>

#include "il2cpp/Types.h"

namespace bl::runtime {

constexpr int kVanillaTileCount = 754;

struct ModTileDef {
    std::string mod;
    std::string name;
    std::string texture;
};

int registerModTile(ModTileDef def);

bool isModTile(int type);

int tileTypeCount();

void ensureTileDrawHook();

void tickModTiles();

bool modTilesSettled();

using TilesInstalledHook = void (*)(int first, int last);
void setTilesInstalledHook(TilesInstalledHook hook);
void setManagedTilesInstalledHook(TilesInstalledHook hook);

int modTileTypeByName(const std::string& mod, const std::string& name);

std::string modTileKey(int type);

struct ModObjectShape {
    int cols = 1, rows = 1, coordWidth = 16, padding = 2, fullWidth = 18, fullHeight = 18;
    std::vector<int> heights;
};

bool modObjectShape(int type, ModObjectShape* out);

int modTileTypeByKey(const std::string& key);

struct ModTileInfo {
    int type;
    std::string mod, name, texture;
};

void setModTileAnimation(int type, int frameHeight);
void readManagedTileDrawData(void* renderer, int x, int y, int offset, int type, void* data, int* halfBrick);

struct TileDrawData {
    int32_t frameX = 0, frameY = 0;
    int32_t width = 16, height = 16, top = 0;
    int32_t addFrX = 0, addFrY = 0;
    int32_t effects = 0;
};

using DrawDataHook = bool (*)(int x, int y, int type, TileDrawData* d, bool special);
void setDrawDataHook(DrawDataHook hook, const std::atomic<uint8_t>* marks, const std::atomic<uint8_t>* uncached);

std::vector<ModTileInfo> modTiles();

}
