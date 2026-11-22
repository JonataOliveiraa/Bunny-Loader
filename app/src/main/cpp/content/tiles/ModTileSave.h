#pragma once

#include <cstdint>
#include <functional>
#include <utility>
#include <vector>

namespace bl::runtime {

void installModTileSave();

int32_t withKeptChestTiles(const std::vector<std::pair<int, int>>& chests, const std::function<int32_t()>& run);

}
