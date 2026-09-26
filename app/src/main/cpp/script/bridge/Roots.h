#pragma once
#include "il2cpp/Types.h"
#include <cstdint>

namespace bl::script {

/**
 * Ancora dos objetos do jogo que o JS segura.
 *
 * O coletor do IL2CPP (Boehm) nao enxerga a memoria do QuickJS: um objeto que
 * so o mod segura seria recolhido. Antes, cada wrapper pedia um gchandle
 * (il2cpp_gchandle_new/free: trava e tabela do runtime a cada objeto que
 * entrava e saia do JS). Aqui a ancora e um SLOT num bloco que o coletor
 * varre como raiz (il2cpp_gc_alloc_fixed): ancorar e gravar o ponteiro num
 * slot livre, soltar e zerar o slot.
 *
 * Sem a API de memoria fixa (outra versao do runtime), cai no gchandle.
 *
 * NAO e thread-safe: so se chega aqui com o motor JS travado (JsLock), e o
 * finalizador dos wrappers roda dentro da coleta do QuickJS, que tambem.
 */
class Roots {
public:
    /** Ancora `obj`. Devolve o token para `remove` (0 = nada a soltar). */
    static uint32_t add(void* obj);
    static void remove(uint32_t token);

    /** Quantos objetos ancorados agora (diagnostico). */
    static uint32_t live();
};

} // namespace bl::script
