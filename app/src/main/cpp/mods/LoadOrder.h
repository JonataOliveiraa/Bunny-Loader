#pragma once
#include <cstddef>
#include <string>
#include <vector>

namespace bl::mods {

/** Um mod da lista do launcher, com o que o manifesto pede. */
struct OrderEntry {
    std::string uid;
    std::string id;                          // o "id" do manifesto
    std::vector<std::string> dependencies;   // "id" ou "uid" de outro mod
    bool usable = true;                      // false: ja caiu antes (sem arquivo de entrada)
};

struct OrderResult {
    /** Indices de `mods`, na ordem de carga: toda dependencia antes de quem a pede. */
    std::vector<size_t> order;
    /** Por indice: vazio se pode carregar; senao, o motivo (em ingles, vai ao painel). */
    std::vector<std::string> errors;
    /** Por indice: as dependencias achadas, como indices de `mods`. */
    std::vector<std::vector<size_t>> needs;
};

/**
 * Ordenacao topologica estavel, como a TopoSort do tModLoader: percorre a
 * lista na ordem do launcher e poe as dependencias de cada mod antes dele.
 * Uma lista que ja respeita as dependencias sai igual, e e isso que deixa a
 * ordem que o servidor manda na sincronizacao chegar intacta ao cliente.
 *
 * Dependencia ausente (ou um `id` de dois mods), ciclo, ou dependencia que
 * caiu: o mod fica de fora, com o motivo em `errors`, e quem depende dele
 * tambem.
 */
OrderResult sortByDependencies(const std::vector<OrderEntry>& mods);

} // namespace bl::mods
