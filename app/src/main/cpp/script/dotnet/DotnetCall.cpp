#include "script/dotnet/DotnetCall.h"
#include "il2cpp/GuardedCall.h"

#include <cstring>
#include <string>

namespace bl::dotnet {

namespace {

// 8 inteiros, 8 doubles e mais 8 inteiros: pelo AAPCS64 isso entrega
// exatamente x0-x7, d0-d7 e as 8 primeiras casas da pilha (como o runRaw da
// ponte JS, script/bridge/Abi.cpp). Casas a mais sao inofensivas: no arm64
// quem CHAMA reserva e limpa a area de argumentos.
#define BL_CS_PARAMS                                                      \
    intptr_t, intptr_t, intptr_t, intptr_t, intptr_t, intptr_t, intptr_t, \
    intptr_t, double, double, double, double, double, double, double,    \
    double, intptr_t, intptr_t, intptr_t, intptr_t, intptr_t, intptr_t,  \
    intptr_t, intptr_t

struct S16 { uint64_t a, b; };
struct H1F { float a; };
struct H2F { float a, b; };
struct H3F { float a, b, c; };
struct H4F { float a, b, c, d; };
struct H1D { double a; };
struct H2D { double a, b; };
struct H3D { double a, b, c; };
struct H4D { double a, b, c, d; };
// Struct de retorno > 16 bytes: o compilador passa em x8 o endereco deste
// espaco, e o metodo do jogo escreve so o tamanho dele.
struct Big { uint8_t b[kMaxIndirectReturn]; };

struct Pending {
    void* fn;
    CallFrame* frame;
};

/** Roda dentro da guarda: nada aqui pode ter destrutor. */
void run(void* p) {
    const Pending& c = *static_cast<const Pending*>(p);
    CallFrame& f = *c.frame;
    const intptr_t* x = f.x;
    double d[8];
    std::memcpy(d, f.d, sizeof(d));

#define BL_CS_ARGS x[0], x[1], x[2], x[3], x[4], x[5], x[6], x[7], \
                   d[0], d[1], d[2], d[3], d[4], d[5], d[6], d[7], \
                   x[8], x[9], x[10], x[11], x[12], x[13], x[14], x[15]
#define BL_CS_CALL(T)                                                      \
    do {                                                                   \
        T r = reinterpret_cast<T (*)(BL_CS_PARAMS)>(c.fn)(BL_CS_ARGS);     \
        std::memcpy(f.out, &r, sizeof(r));                                 \
    } while (0)

    switch (static_cast<RetKind>(f.ret)) {
        case RetKind::Int: BL_CS_CALL(intptr_t); break;
        case RetKind::F32: BL_CS_CALL(float); break;
        case RetKind::F64: BL_CS_CALL(double); break;
        case RetKind::S16: BL_CS_CALL(S16); break;
        case RetKind::H1F: BL_CS_CALL(H1F); break;
        case RetKind::H2F: BL_CS_CALL(H2F); break;
        case RetKind::H3F: BL_CS_CALL(H3F); break;
        case RetKind::H4F: BL_CS_CALL(H4F); break;
        case RetKind::H1D: BL_CS_CALL(H1D); break;
        case RetKind::H2D: BL_CS_CALL(H2D); break;
        case RetKind::H3D: BL_CS_CALL(H3D); break;
        case RetKind::H4D: BL_CS_CALL(H4D); break;
        case RetKind::Indirect: {
            Big r = reinterpret_cast<Big (*)(BL_CS_PARAMS)>(c.fn)(BL_CS_ARGS);
            std::memcpy(f.indirect, &r, static_cast<size_t>(f.retSize));
            break;
        }
    }
#undef BL_CS_CALL
#undef BL_CS_ARGS
}

thread_local std::string t_lastError;

} // namespace

int32_t callGame(void* fn, CallFrame* frame) {
    if (!fn || !frame) return 0;
    if (static_cast<RetKind>(frame->ret) == RetKind::Indirect &&
        (frame->retSize <= 0 || frame->retSize > kMaxIndirectReturn || !frame->indirect)) {
        t_lastError = "invalid indirect return";
        return 0;
    }
    Pending p{fn, frame};
    il2cpp::Caught caught;
    if (il2cpp::guardedCall(run, &p, &caught)) return 1;
    t_lastError = il2cpp::describeCaught(caught);
    return 0;
}

const char* lastCallError() { return t_lastError.c_str(); }

} // namespace bl::dotnet
