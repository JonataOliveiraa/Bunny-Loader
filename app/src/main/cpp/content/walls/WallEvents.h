#pragma once
#include <cstdint>

namespace bl::runtime
{
using WallEventCallback = int32_t (*)(int32_t kind, int32_t x, int32_t y, int32_t type, void *args);
bool installWallEvents(int32_t type, uint32_t flags, WallEventCallback callback);
int32_t scanWallEvents(int32_t x, int32_t y, int32_t width, int32_t height, uint32_t flags, int32_t *cells,
                       int32_t capacity);
}
