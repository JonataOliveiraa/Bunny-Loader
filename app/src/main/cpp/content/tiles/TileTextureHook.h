#pragma once
#include <cstdint>

namespace bl::runtime {
using TileTextureCallback = void* (*)(int32_t x, int32_t y, int32_t type);
bool installTileTextureHook(int32_t firstType, int32_t secondType, TileTextureCallback callback);
}
