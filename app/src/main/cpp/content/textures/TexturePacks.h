#pragma once
#include "core/Config.h"

namespace bl::runtime {

// Publica a selecao do launcher. Nao cria objetos Unity nesta thread.
void configureTexturePacks(const std::string& root, const std::vector<ModSpec>& enabled);
// Thread do jogo: restaura a selecao anterior e aplica os PNGs da nova.
void tickTexturePacks();

} // namespace bl::runtime
