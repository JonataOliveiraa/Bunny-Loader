#pragma once
#include <cstdint>
#include <string>
#include <vector>

namespace bl::mods {

struct LoadedMod {
    std::string id;
    std::string dir;
    std::string entry;   // ex: "main.js"
    uint32_t errorCount = 0;
    bool enabled = true;
};

// Carrega os mods habilitados de modsDir, na ordem de dependências.
void loadAll(const std::string& modsDir, const std::vector<std::string>& enabled);

LoadedMod* get(uint16_t index);
void disableAtRuntime(uint16_t index);

// Um mod que falha demais é desativado em runtime; falha NUNCA derruba o jogo.
constexpr uint32_t MaxErrors = 20;

} // namespace bl::mods
