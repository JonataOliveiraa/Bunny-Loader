#include "script/Abi.h"

#if BL_HAVE_QUICKJS && defined(__aarch64__)
#include "il2cpp/Api.h"
#include "script/Bridge.h"

#include <cstring>
#include <new>

namespace bl::script {

// A assinatura de captura: 8 inteiros seguidos de 8 doubles. Pelo AAPCS isso
// entrega exatamente x0-x7 e d0-d7, sem asm nenhum.
#define BL_RAW_PARAMS                                                     \
    intptr_t a0, intptr_t a1, intptr_t a2, intptr_t a3, intptr_t a4,      \
    intptr_t a5, intptr_t a6, intptr_t a7, double f0, double f1,          \
    double f2, double f3, double f4, double f5, double f6, double f7

namespace {
using RawI = intptr_t (*)(BL_RAW_PARAMS);
using RawF = float (*)(BL_RAW_PARAMS);
using RawD = double (*)(BL_RAW_PARAMS);

/** Tamanho de cada casa de um HFA (struct so de float, ou so de double). */
size_t hfaSlot(const TypeDesc& d) {
    bool dbl = false;
    int n = hfaOf(d.cls, &dbl);
    return (n > 0 && dbl) ? 8u : 4u;
}
} // namespace

Outcome callRaw(void* fn, const AbiPlan& plano, const intptr_t a[8], const uint64_t d[8],
                bool* threw, bool suspend) {
    const Ret ret = plano.ret;
    Outcome o;
    if (!fn) return o;

    // Soltar o motor custa quatro operacoes atomicas, o que num getter de
    // 100 ns pesa mais que a chamada — medido: a propriedade C# foi de 4 para
    // 11 ms por 10k quando isto era incondicional. Entao so no hook, onde o
    // corpo do metodo pode ser longo de verdade.
    alignas(alignof(JsSuspend)) unsigned char espaco[sizeof(JsSuspend)];
    JsSuspend* solta = suspend ? new (espaco) JsSuspend() : nullptr;

    double f[8];
    std::memcpy(f, d, sizeof(f));

#define BL_ARGS a[0], a[1], a[2], a[3], a[4], a[5], a[6], a[7], \
                f[0], f[1], f[2], f[3], f[4], f[5], f[6], f[7]
    // O tipo de retorno do ponteiro de funcao E a ABI: chamar um metodo que
    // devolve Vector2 por um ponteiro que devolve intptr_t leria x0 enquanto o
    // valor esta em s0/s1.
#define BL_CALL(T)                                                        \
    do {                                                                  \
        T r = reinterpret_cast<T (*)(BL_RAW_PARAMS)>(fn)(BL_ARGS);        \
        std::memcpy(o.s, &r, sizeof(r));                                  \
    } while (0)

#if defined(__cpp_exceptions)
    // A JsSuspend fica FORA do try de proposito: no caminho da excecao ela tem
    // de reaver a trava depois do catch, nao durante o desenrolar.
    try {
#endif
    switch (ret) {
        case Ret::F32: o.f = reinterpret_cast<RawF>(fn)(BL_ARGS); break;
        case Ret::F64: o.f = reinterpret_cast<RawD>(fn)(BL_ARGS); break;
        case Ret::S8:  BL_CALL(S8);  break;
        case Ret::S16: BL_CALL(S16); break;
        case Ret::H1F: BL_CALL(H1F); break;
        case Ret::H2F: BL_CALL(H2F); break;
        case Ret::H3F: BL_CALL(H3F); break;
        case Ret::H4F: BL_CALL(H4F); break;
        case Ret::H1D: BL_CALL(H1D); break;
        case Ret::H2D: BL_CALL(H2D); break;
        case Ret::H3D: BL_CALL(H3D); break;
        case Ret::H4D: BL_CALL(H4D); break;
        case Ret::Int: o.i = reinterpret_cast<RawI>(fn)(BL_ARGS); break;
    }
#if defined(__cpp_exceptions)
    } catch (...) {
        // Sem RTTI nao da para abrir o Il2CppExceptionWrapper e ler a mensagem
        // — mas o runtime_invoke tambem nunca a mostrou, so dizia qual metodo
        // lancou, e isso quem chama sabe.
        if (threw) *threw = true;
        o = Outcome{};
    }
#endif
#undef BL_CALL
#undef BL_ARGS
    if (solta) solta->~JsSuspend();
    return o;
}

JSValue paramToJs(JSContext* ctx, const intptr_t a[8], const uint64_t d[8],
                  const ParamPlan& p) {
    if (p.opaque) return JS_UNDEFINED;

    if (p.d.prim == Prim::Struct) {
        if (p.structByRef) {
            // Struct grande: o registrador tem o endereco de uma copia feita
            // pelo chamador. Copiamos de novo — semantica de valor do C#.
            void* src = reinterpret_cast<void*>(a[p.reg]);
            if (p.d.nullable()) return readAt(ctx, src, p.d, JS_UNDEFINED);
            return makeStructCopy(ctx, p.d.cls, src, p.d.size);
        }
        uint8_t buf[32] = {0};
        const size_t cap = p.d.size < sizeof(buf) ? p.d.size : sizeof(buf);
        if (p.floatQueue) {
            size_t slot = hfaSlot(p.d);
            for (int i = 0; i < p.regs; ++i) {
                size_t at = static_cast<size_t>(i) * slot;
                if (at + slot > cap) break;
                std::memcpy(buf + at, &d[p.reg + i], slot);
            }
        } else {
            size_t n = cap < static_cast<size_t>(p.regs) * 8
                           ? cap : static_cast<size_t>(p.regs) * 8;
            std::memcpy(buf, &a[p.reg], n);
        }
        // `float?` chega como null ou numero, nao como struct {hasValue, value}.
        if (p.d.nullable()) return readAt(ctx, buf, p.d, JS_UNDEFINED);
        return makeStructCopy(ctx, p.d.cls, buf, p.d.size);
    }

    if (p.floatQueue) {
        uint64_t bits = d[p.reg];
        if (p.d.prim == Prim::F32) {
            // Um float viaja nos 32 bits BAIXOS: ler os 64 como double da um
            // denormal (180.0f virava 5.57e-315).
            uint32_t lo = static_cast<uint32_t>(bits);
            float v;
            std::memcpy(&v, &lo, sizeof(v));
            return JS_NewFloat64(ctx, v);
        }
        double v;
        std::memcpy(&v, &bits, sizeof(v));
        return JS_NewFloat64(ctx, v);
    }

    // Fila inteira: o valor esta nos bytes BAIXOS. readAt le exatamente o
    // tamanho declarado — e o que conserta enum de 1 byte e int com lixo nos
    // 32 bits altos.
    return readAt(ctx, const_cast<intptr_t*>(&a[p.reg]), p.d, JS_UNDEFINED);
}

int jsToParam(JSContext* ctx, JSValueConst v, const ParamPlan& p,
              intptr_t a[8], uint64_t d[8], ArgScratch* scratch) {
    if (p.opaque) return true;  // ref/out: mantem o que o chamador mandou

    if (p.d.nullable()) {
        // Monta os bytes do Nullable (null, o T, ou outro Nullable) e so
        // entao decide o caminho: nunca e HFA (tem o bool), entao e fila
        // inteira ou endereco.
        uint8_t local[16];
        void* dst = local;
        if (p.structByRef) {
            dst = scratch ? scratch->take(p.d.size) : nullptr;
            if (!dst) {
                JS_ThrowInternalError(ctx, "sem espaco para montar o %s desta chamada",
                                      p.d.name.c_str());
                return -1;
            }
        }
        if (writeAt(ctx, dst, p.d, v) < 0) return -1;
        if (p.structByRef) {
            a[p.reg] = reinterpret_cast<intptr_t>(dst);
        } else {
            for (int i = 0; i < p.regs; ++i) a[p.reg + i] = 0;
            std::memcpy(&a[p.reg], dst, p.d.size);
        }
        return true;
    }

    if (p.d.prim == Prim::Struct) {
        Il2CppClass* cls = nullptr;
        size_t size = 0;
        void* src = structDataOf(v, &cls, &size);
        if (!src) {
            JS_ThrowTypeError(ctx, "esperava um %s", p.d.name.c_str());
            return -1;
        }
        if (cls && p.d.cls && cls != p.d.cls) {
            JS_ThrowTypeError(ctx, "esperava %s, recebeu %s", p.d.name.c_str(),
                              il2cpp::api().class_get_name(cls));
            return -1;
        }
        if (p.structByRef) {
            a[p.reg] = reinterpret_cast<intptr_t>(src);
            return true;
        }
        if (p.floatQueue) {
            size_t slot = hfaSlot(p.d);
            for (int i = 0; i < p.regs; ++i) {
                d[p.reg + i] = 0;
                std::memcpy(&d[p.reg + i], static_cast<char*>(src) + i * slot, slot);
            }
            return true;
        }
        for (int i = 0; i < p.regs; ++i) a[p.reg + i] = 0;
        std::memcpy(&a[p.reg], src, p.d.size);
        return true;
    }

    if (p.floatQueue) {
        double x = 0;
        if (JS_ToFloat64(ctx, &x, v) < 0) return -1;
        d[p.reg] = 0;
        if (p.d.prim == Prim::F32) {
            float s = static_cast<float>(x);
            std::memcpy(&d[p.reg], &s, sizeof(s));
        } else {
            std::memcpy(&d[p.reg], &x, sizeof(x));
        }
        return true;
    }

    a[p.reg] = 0;
    return writeAt(ctx, &a[p.reg], p.d, v);
}

JSValue outcomeToJs(JSContext* ctx, const AbiPlan& p, const Outcome& o) {
    const TypeDesc& d = p.retDesc;
    if (d.prim == Prim::Void) return JS_UNDEFINED;
    // Copia, nao vista: os bytes estao num Outcome de pilha que ja foi embora.
    if (d.nullable()) return readAt(ctx, const_cast<uint8_t*>(o.s), d, JS_UNDEFINED);
    if (d.prim == Prim::Struct) return makeStructCopy(ctx, d.cls, o.s, d.size);
    if (p.ret == Ret::F32 || p.ret == Ret::F64) return JS_NewFloat64(ctx, o.f);
    intptr_t raw = o.i;
    return readAt(ctx, &raw, d, JS_UNDEFINED);
}

Outcome jsToOutcome(JSContext* ctx, const AbiPlan& p, JSValueConst v,
                    const Outcome& fallback) {
    const TypeDesc& d = p.retDesc;
    if (JS_IsUndefined(v) || JS_IsException(v)) return fallback;
    if (d.prim == Prim::Void) return fallback;

    Outcome o;
    if (d.nullable()) {
        // `return null` e resposta valida para um `float?`: sem valor.
        if (d.size > sizeof(o.s) || writeAt(ctx, o.s, d, v) < 0) {
            JS_FreeValue(ctx, JS_GetException(ctx));
            return fallback;
        }
        return o;
    }
    if (d.prim == Prim::Struct) {
        Il2CppClass* src = nullptr;
        size_t n = 0;
        void* q = structDataOf(v, &src, &n);
        // Devolveu outra coisa que nao um struct do tipo certo: fica o do
        // original, como qualquer retorno que nao converte.
        if (!q || (src && d.cls && src != d.cls)) return fallback;
        size_t cap = d.size < sizeof(o.s) ? d.size : sizeof(o.s);
        std::memcpy(o.s, q, n < cap ? n : cap);
        return o;
    }
    if (p.ret == Ret::F32 || p.ret == Ret::F64) {
        // Pelo writeAt, que recusa o que nao e numero. Antes era JS_ToFloat64
        // direto: um callback que devolvesse texto entregava NaN ao jogo.
        //
        // O buffer tem de ter o tamanho do TIPO: writeAt de um Single escreve
        // 4 bytes, e apontar para um double deixaria a metade de cima como
        // estava.
        bool ok;
        if (p.ret == Ret::F32) {
            float f32 = 0;
            ok = writeAt(ctx, &f32, p.retDesc, v) > 0;
            o.f = f32;
        } else {
            double f64 = 0;
            ok = writeAt(ctx, &f64, p.retDesc, v) > 0;
            o.f = f64;
        }
        if (!ok) {
            JS_FreeValue(ctx, JS_GetException(ctx));
            return fallback;
        }
        return o;
    }
    intptr_t raw = 0;
    if (writeAt(ctx, &raw, d, v) < 0) {
        JS_FreeValue(ctx, JS_GetException(ctx));
        return fallback;
    }
    o.i = raw;
    return o;
}

std::string planAbi(const MethodInfo* m, bool isInstance, AbiPlan* out) {
    auto& api = il2cpp::api();
    AbiPlan p;
    p.retDesc = describe(api.method_get_return_type(m));

    if (p.retDesc.prim == Prim::Struct) {
        bool dbl = false;
        int hfa = hfaOf(p.retDesc.cls, &dbl);
        if (hfa >= 1 && hfa <= 4) {
            int base = static_cast<int>(dbl ? Ret::H1D : Ret::H1F);
            p.ret = static_cast<Ret>(base + hfa - 1);
        } else if (p.retDesc.size <= 8) {
            p.ret = Ret::S8;
        } else if (p.retDesc.size <= 16) {
            p.ret = Ret::S16;
        } else {
            // Acima de 16 bytes (sem ser HFA) o valor volta pela MEMORIA: o
            // chamador reserva o espaco e passa o endereco em x8, que nao e
            // registrador de argumento e nao chega numa funcao C sem asm.
            return "devolve " + p.retDesc.name + ", de " +
                   std::to_string(p.retDesc.size) +
                   " bytes: acima de 16 o struct volta pela memoria (endereco em"
                   " x8), que a nossa captura nao alcanca";
        }
    } else {
        p.ret = p.retDesc.prim == Prim::F32 ? Ret::F32
              : p.retDesc.prim == Prim::F64 ? Ret::F64
                                            : Ret::Int;
    }

    int x = isInstance ? 1 : 0;  // `this` ocupa x0
    int dq = 0;
    uint32_t n = api.method_get_param_count(m);
    for (uint32_t i = 0; i < n; ++i) {
        ParamPlan pp;
        pp.d = describe(api.method_get_param(m, i));
        if (pp.d.byRef) {
            // ref/out chega como ponteiro. Repassamos intacto; expor o
            // endereco cru ao JS so serviria para alguem escrever nele.
            pp.opaque = true;
            pp.reg = x++;
        } else if (pp.d.prim == Prim::F32 || pp.d.prim == Prim::F64) {
            pp.floatQueue = true;
            pp.reg = dq++;
        } else if (pp.d.prim == Prim::Struct) {
            bool dbl = false;
            int hfa = hfaOf(pp.d.cls, &dbl);
            if (hfa > 0) {
                pp.floatQueue = true;   // Vector2 = {float,float} -> s0,s1
                pp.reg = dq;
                pp.regs = hfa;
                dq += hfa;
            } else if (pp.d.size > 16) {
                pp.structByRef = true;  // grande: vai o endereco de uma copia
                pp.reg = x++;
            } else {
                pp.regs = pp.d.size > 8 ? 2 : 1;
                pp.reg = x;
                x += pp.regs;
            }
        } else {
            pp.reg = x++;
        }
        p.params.push_back(pp);
    }

    // Todo metodo gerado pelo IL2CPP recebe um const MethodInfo* a mais, no
    // fim — nao aparece na lista de parametros e nao da para omitir.
    p.methodInfoReg = x++;
    p.intRegs = x;
    p.fltRegs = dq;
    if (x > 8 || dq > 8) {
        return "tem argumentos demais para a captura (" + std::to_string(x) +
               " inteiros, " + std::to_string(dq) +
               " de ponto flutuante; o limite e 8 de cada) — o resto viaja pela"
               " pilha, que nao capturamos";
    }
    *out = std::move(p);
    return {};
}

} // namespace bl::script
#endif
