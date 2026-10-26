#pragma once
#include <cstdint>

namespace bl::dotnet {

/**
 * Uma chamada do C# a um metodo do jogo, com os registradores ja montados.
 *
 * Quem distribui os argumentos pelos registradores e o GERADOR das proxies
 * (app/src/main/csharp/ProxyGen), na hora de gerar: ele conhece cada tipo e
 * aplica o AAPCS64. Aqui so se carrega x0-x7, d0-d7 e 8 casas de pilha e se
 * salta. Layout espelhado em BunnyLoader/Interop/CallFrame.cs.
 */
struct CallFrame {
    intptr_t x[16];      // x0-x7 e as 8 primeiras casas da pilha
    uint64_t d[8];       // d0-d7, bits crus (um float vive nos 32 de baixo)
    void* indirect;      // retorno > 16 bytes: onde copiar (o x8 do chamador)
    int32_t ret;         // RetKind
    int32_t retSize;     // so para RetKind::Indirect
    uint64_t out[4];     // x0/x1, ou s0-s3/d0-d3 empacotados como o struct
};

/** Onde volta o retorno. Mesma numeracao de Interop/RetKind.cs. */
enum class RetKind : int32_t {
    Int = 0,      // x0 (inclui void, ponteiros, bool, enums)
    F32 = 1,      // s0
    F64 = 2,      // d0
    S16 = 3,      // x0 e x1 (struct de ate 16 bytes que nao e HFA)
    H1F = 4, H2F = 5, H3F = 6, H4F = 7,   // HFA de float: s0-s3
    H1D = 8, H2D = 9, H3D = 10, H4D = 11, // HFA de double: d0-d3
    Indirect = 12,                        // > 16 bytes: x8
};

/** O maior retorno por x8 aceito. Acima disso o gerador nao gera a chamada. */
constexpr int32_t kMaxIndirectReturn = 512;

/**
 * Chama `fn` dentro do il2cpp::guardedCall. Uma excecao do jogo nao pode
 * atravessar os frames do JIT do Mono (o desenrolador do libgcc nao os
 * conhece e o processo cai): ela para aqui e vira 0, com o texto em
 * lastCallError().
 *
 * @return 1 se voltou normalmente, 0 se o jogo lancou.
 */
int32_t callGame(void* fn, CallFrame* frame);

/** O texto da ultima excecao do jogo nesta thread ("System.X: mensagem"). */
const char* lastCallError();

} // namespace bl::dotnet
