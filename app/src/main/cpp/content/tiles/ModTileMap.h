#pragma once
#include <string>

namespace bl::runtime {

int addModTileMapEntry(int type, int r, int g, int b, const std::string& nameKey);

int addModWallMapEntry(int type, int r, int g, int b, const std::string& nameKey);

void applyModTileMap(int total, bool rebuilt);

bool readMapTile(int x, int y, int* type, int* light, int* paint);

void installModTileMap();

}
