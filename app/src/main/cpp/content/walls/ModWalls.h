#pragma once
#include <cstddef>
#include <string>
#include <vector>

#include "il2cpp/Types.h"

namespace bl::runtime {

constexpr int kVanillaWallCount = 367;

struct ModWallDef {
    std::string mod;
    std::string name;
    std::string texture;
    const unsigned char* textureData = nullptr;   // textura embutida (a parede nao carregada)
    size_t textureSize = 0;
};

int registerModWall(ModWallDef def);

bool isModWall(int type);

int wallTypeCount();

void tickModWalls();

bool modWallsSettled();

using WallsInstalledHook = void (*)(int first, int last);
void setWallsInstalledHook(WallsInstalledHook hook);

int modWallTypeByName(const std::string& mod, const std::string& name);

std::string modWallKey(int type);

int modWallTypeByKey(const std::string& key);

struct ModWallInfo {
    int type;
    std::string mod, name, texture;
};

std::vector<ModWallInfo> modWalls();

/**
 * A parede "nao carregada" do Bunny Loader (a UnloadedWall do tModLoader).
 * Sem o mod de uma parede salva, ela fica no lugar: segura o que esta preso
 * na parede (quadro, tocha) e a original volta quando o mod voltar. Quebrada,
 * a original daquela casa se perde. Registrada depois dos mods, sempre.
 */
void registerUnloadedWall();

/** O tipo da parede nao carregada, ou -1 (ainda nao registrada ou paredes desligadas). */
int unloadedWallType();

} // namespace bl::runtime
