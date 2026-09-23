#pragma once
#include <cstdint>
#include <string>
#include <vector>

#include "core/Config.h"  // ModSpec

namespace bl::mods {

struct LoadedMod {
    std::string id;
    std::string dir;
    std::string entry;   // ex: "main.js"
    uint32_t errorCount = 0;
    bool enabled = true;
};

// Carrega os mods habilitados de modsDir, na ordem de dependências.
void loadAll(const std::string& modsDir, const std::vector<ModSpec>& enabled);

// Carrega os mods embutidos na libbunny (JS compilado no binário). Usado como
// fallback no celular quando não há mods externos provisionados (sem adb).
void loadBuiltins();

// Quantos mods estão registrados (externos + embutidos já carregados).
size_t loadedCount();

LoadedMod* get(uint16_t index);
void disableAtRuntime(uint16_t index);

/**
 * Pasta do mod que está sendo carregado agora, ou vazio fora da carga.
 *
 * É o que deixa `bl.loadTexture('icone.png')` achar o arquivo ao lado do
 * main.js sem o autor precisar saber o caminho absoluto do aparelho.
 */
const std::string& currentDir();

/** Pasta do entry de um mod ja carregado, pelo id. Vazio se nao conhece. */
const std::string& dirOf(const std::string& id);

/** Id (uid) do mod que esta carregando agora, ou vazio fora da carga. */
const std::string& currentId();

/** Pasta raiz do pacote (onde mora o manifest.json). Vazio se nao conhece. */
std::string rootOf(const std::string& id);

/** O "name" do manifesto, para mostrar a gente; o id se nao houver. */
std::string displayName(const std::string& id);

// Um mod que falha demais é desativado em runtime; falha NUNCA derruba o jogo.
constexpr uint32_t MaxErrors = 20;

} // namespace bl::mods
