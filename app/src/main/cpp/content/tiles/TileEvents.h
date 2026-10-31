#pragma once
#include <cstdint>
namespace bl::runtime {
struct TileDrawData;
using TileEventCallback = int32_t (*)(int32_t kind, int32_t x, int32_t y, int32_t type, void* args);
bool installTileEvents(int32_t type, uint32_t flags, TileEventCallback callback);
int32_t scanTileEvents(int32_t x, int32_t y, int32_t width, int32_t height, uint32_t flags, int32_t* cells, int32_t capacity);
bool hasManagedTileDrawData(int32_t type);
void managedTileDrawData(int32_t x, int32_t y, int32_t type, TileDrawData* data, bool special);
}
