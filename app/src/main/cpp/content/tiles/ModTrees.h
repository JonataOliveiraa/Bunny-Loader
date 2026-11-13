#pragma once

struct Il2CppObject;

namespace bl::runtime {

void setModTree(int groundType, Il2CppObject* trunk, int topStyle);

void noteTreeDrawTile(int x, int y, int type, int frameX, int frameY);

}
