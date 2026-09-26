#pragma once
#include "script/bridge/ScriptEngine.h"

#if BL_HAVE_QUICKJS
#include "quickjs.h"
#include "il2cpp/Types.h"
#include "script/bridge/Value.h"

#include <cstdint>
#include <string>
#include <vector>

namespace bl::script {

#if defined(__aarch64__)

/**
 * A ABI do arm64 (AAPCS64), num lugar so.
 *
 * Dois caminhos precisam dela e precisam concordar: o HOOK, que RECEBE os
 * registradores de uma chamada do jogo, e a CHAMADA DIRETA, que os MONTA para
 * entrar no metodo. Enquanto cada um tinha a sua copia, o hook sabia que um
 * Vector2 viaja em s0/s1 e o invoke nao — e um deles estava sempre errado.
 *
 * As duas filas sao SEPARADAS: inteiros e ponteiros em x0-x7, ponto flutuante
 * em d0-d7. Um `float` ocupa os 32 bits BAIXOS (s0), nao o registrador todo.
 */

// Carregadores de RETORNO. O tipo de retorno de uma funcao C faz parte da ABI:
// e ele que decide se o valor sai em x0, em s0 ou num par. Para cada forma ha
// um struct com o mesmo formato, e o compilador cuida do resto.
struct S8  { uint64_t a; };
struct S16 { uint64_t a, b; };
struct H1F { float a; };
struct H2F { float a, b; };
struct H3F { float a, b, c; };
struct H4F { float a, b, c, d; };
struct H1D { double a; };
struct H2D { double a, b; };
struct H3D { double a, b, c; };
struct H4D { double a, b, c, d; };

/**
 * Onde o valor de retorno viaja.
 *
 * A ordem importa: H1F..H4F e H1D..H4D sao consecutivos para indexar pela
 * contagem do HFA, e S8 e o primeiro dos de struct.
 */
enum class Ret { Int, F32, F64, S8, S16, H1F, H2F, H3F, H4F, H1D, H2D, H3D, H4D };

/**
 * A fila de inteiros: x0-x7 e mais 8 casas da PILHA. Pelo AAPCS64 (Android),
 * o 9o inteiro em diante vai para a pilha, uma casa de 8 bytes cada, na ordem
 * dos parametros — numa funcao C isso e so "mais parametros". O Player.Hurt
 * (self + 8 + MethodInfo) precisa de 10.
 */
constexpr int kIntSlots = 16;

/** Onde um parametro viaja. Decidido UMA vez, nao a cada chamada. */
struct ParamPlan {
    TypeDesc d;
    bool floatQueue = false;   // usa d0-d7 em vez de x0-x7
    int reg = 0;               // indice inicial na fila (inteiros: >= 8 e pilha)
    int regs = 1;              // quantos registradores ocupa
    bool structByRef = false;  // o registrador guarda um PONTEIRO pro struct
    bool opaque = false;       // ref/out: o ponteiro vai intacto; o JS ve um Ref (Ref.h)
};

/** Retorno bruto: o campo que vale depende do Ret. */
struct Outcome {
    intptr_t i = 0;
    double f = 0.0;
    // 32 bytes cobrem a maior forma que viaja em registrador: HFA de 4 doubles.
    uint8_t s[32] = {0};
};

/** Como o metodo recebe e devolve. Vazio = da para reproduzir a chamada. */
struct AbiPlan {
    Ret ret = Ret::Int;
    TypeDesc retDesc;
    std::vector<ParamPlan> params;
    int intRegs = 0;    // quantos x foram usados, JA contando o MethodInfo*
    int fltRegs = 0;    // quantos d foram usados
    int methodInfoReg = 0;  // o x que leva o `const MethodInfo*` do IL2CPP
};

/**
 * Distribui parametros e retorno pelos registradores.
 * @return mensagem de erro, ou vazio se couber na nossa captura.
 */
std::string planAbi(const MethodInfo* m, bool isInstance, AbiPlan* out);

/**
 * Chama `fn` com os registradores montados, pela forma de retorno `ret`.
 *
 * `threw` (opcional) vira true se o metodo do jogo lancou. Uma excecao gerida
 * pelo IL2CPP e uma excecao C++ de verdade (Il2CppExceptionWrapper): deixar
 * passar por um frame compilado com -fno-exceptions chama std::terminate e
 * derruba o jogo. Por isso Abi.cpp — e SO ele — compila com -fexceptions e
 * para a excecao aqui, que e o mesmo que o runtime_invoke faz.
 *
 * `suspend` solta o motor JS durante a chamada (ver JsSuspend). Vale a pena
 * num HOOK, onde o corpo do metodo pode ser DoDraw inteiro e outra thread
 * (a geracao de mundo, no SetDefaults) ficaria esperando a trava. NAO vale
 * numa chamada que o proprio mod fez: soltar e reaver a trava custa mais que
 * um getter. Medido: ~100 ns por chamada (docs/PONTE-OTIMIZACAO.md).
 */
Outcome callRaw(void* fn, const AbiPlan& p, const intptr_t a[kIntSlots], const uint64_t d[8],
                bool* threw = nullptr, bool suspend = false);

/** Registrador(es) -> valor JS, conforme o plano do parametro. */
JSValue paramToJs(JSContext* ctx, const intptr_t a[kIntSlots], const uint64_t d[8],
                  const ParamPlan& p);

/**
 * Espaco para struct que viaja por ENDERECO e que nos mesmos montamos — um
 * `Rectangle?` vindo de `null` ou de um Rectangle nao existe em lugar nenhum
 * antes da chamada. Mora na pilha de quem chama e vive ate o metodo voltar.
 */
struct ArgScratch {
    alignas(16) uint8_t buf[512];
    size_t used = 0;
    void* take(size_t n) {
        const size_t at = (used + 15) & ~size_t{15};
        if (at + n > sizeof(buf)) return nullptr;
        used = at + n;
        return buf + at;
    }
};

/** Valor JS -> registrador(es). @return -1 com excecao posta. */
int jsToParam(JSContext* ctx, JSValueConst v, const ParamPlan& p,
              intptr_t a[kIntSlots], uint64_t d[8], ArgScratch* scratch = nullptr);

/** Retorno bruto -> valor JS. */
JSValue outcomeToJs(JSContext* ctx, const AbiPlan& p, const Outcome& o);

/** Valor JS -> retorno bruto. Devolve `fallback` se nao converter. */
Outcome jsToOutcome(JSContext* ctx, const AbiPlan& p, JSValueConst v,
                    const Outcome& fallback);

#endif // __aarch64__

} // namespace bl::script
#endif
