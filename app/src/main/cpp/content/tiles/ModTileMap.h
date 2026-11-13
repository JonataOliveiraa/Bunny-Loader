#pragma once
#include <string>

namespace bl::runtime {

int addModTileMapEntry(int type, int r, int g, int b, const std::string& nameKey);

int addModWallMapEntry(int type, int r, int g, int b, const std::string& nameKey);

void applyModTileMap(int total, bool rebuilt);

void installModTileMap();

}
