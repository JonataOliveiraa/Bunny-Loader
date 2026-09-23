#include "script/Bridge.h"
#include "script/Marshal.h"
#include "script/Value.h"
#include "core/Log.h"
#include "hook/HookManager.h"
#include "il2cpp/Api.h"
#include "il2cpp/Signature.h"

#if BL_HAVE_QUICKJS
#include "quickjs.h"
#include <atomic>
#include <cstring>
#include <string>
#include <vector>

namespace bl::script {

#if defined(__aarch64__)

// ================== hook JS -> metodo do jogo (arm64) ==================
//
// O ShadowHook usa UMA funcao de substituicao por alvo, entao cada hook precisa
// da propria. Geramos N funcoes em tempo de compilacao, todas com a mesma
// assinatura de captura, cada uma chamando o dispatcher comum com o seu numero
// de slot.
//
// A CAPTURA: a assinatura e "8 inteiros seguidos de 8 doubles". Pelo AAPCS do
// arm64 os argumentos inteiros vao em x0-x7 e os de ponto flutuante em d0-d7,
// em filas SEPARADAS — declarar assim faz o compilador entregar exatamente
// esses registradores, sem asm nenhum.
//
// Antes isto era um stub em asm naked que gravava o indice num global e saltava
// para o dispatcher. O global era uma CORRIDA: duas threads entrando em hooks
// diferentes ao mesmo tempo e a segunda sobrescrevia o indice da primeira, que
// ia disparar o callback errado. Passar o slot como argumento de uma funcao C
// normal acaba com o problema e ainda deixa o compilador cuidar da ABI.
//
// O RETORNO nao cabe num tipo so: inteiro/ponteiro volta em x0, float em s0 e
// double em d0. Uma funcao C declarada devolvendo intptr_t nao tem como pousar
// valor em d0 — por isso ha TRES familias de funcao geradas, e a instalacao
// escolhe a familia pelo tipo de retorno do metodo.

// STRUCT DE RETORNO: o AAPCS64 tem tres formas, e a forma faz parte da
// assinatura da funcao — nao da para escolher em tempo de execucao. Entao ha
// uma familia de funcoes geradas por forma, e cada uma devolve um TIPO
// CARREGADOR com o mesmo formato do struct do jogo. O compilador cuida da ABI:
//
//   HFA de 1..4 floats   -> s0..s3    (Vector2 = {float,float} -> s0,s1)
//   HFA de 1..4 doubles  -> d0..d3
//   <= 8 bytes           -> x0
//   9..16 bytes          -> x0, x1
//   > 16 bytes           -> MEMORIA: o chamador passa o endereco em x8, que
//                           nao e registrador de argumento. Recusado.
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

constexpr int kIntHooks = 64;                              // slots 0..63
constexpr int kFltHooks = 16;                              // slots 64..79
constexpr int kDblHooks = 16;                              // slots 80..95
// Hook em metodo que devolve struct e minoria: no dump do jogo sao ~2100
// metodos com retorno de struct pequeno contra dezenas de milhares no total, e
// um mod hooka um punhado. Quatro por forma, e a mensagem de erro diz qual
// forma esgotou.
constexpr int kStructSlots = 4;
constexpr int kBaseStruct = kIntHooks + kFltHooks + kDblHooks;   // 96
constexpr int kStructForms = 10;
constexpr int kMaxHooks = kBaseStruct + kStructForms * kStructSlots;

/**
 * Onde o valor de retorno viaja. A ordem importa: H1F..H4F e H1D..H4D sao
 * consecutivos para o buildPlan indexar pela contagem do HFA.
 */
enum class Ret { Int, F32, F64, S8, S16, H1F, H2F, H3F, H4F, H1D, H2D, H3D, H4D };

/** Primeiro slot da familia, e quantos ela tem. */
static void slotRange(Ret r, int* from, int* count) {
    switch (r) {
        case Ret::Int: *from = 0;         *count = kIntHooks; return;
        case Ret::F32: *from = kIntHooks; *count = kFltHooks; return;
        case Ret::F64: *from = kIntHooks + kFltHooks; *count = kDblHooks; return;
        default: break;
    }
    int forma = static_cast<int>(r) - static_cast<int>(Ret::S8);
    *from = kBaseStruct + forma * kStructSlots;
    *count = kStructSlots;
}

/**
 * Onde cada parametro viaja, decidido UMA vez na instalacao.
 *
 * Antes isto era recalculado a cada invocacao, com type_get_name + free por
 * parametro, na thread do jogo, todo frame. E estava incompleto: todo
 * parametro nao-float era lido como int64 da fila inteira, o que da numero
 * gigante quando o chamador deixa lixo nos 32 bits altos, e endereco errado
 * quando o parametro e um struct (Vector2 viaja em d0/d1, nao em x).
 */
struct ParamPlan {
    TypeDesc d;
    bool floatQueue = false;   // usa d0-d7 em vez de x0-x7
    int reg = 0;               // indice inicial na fila
    int regs = 1;              // quantos registradores ocupa
    bool structByRef = false;  // o registrador guarda um PONTEIRO pro struct
    bool opaque = false;       // ref/out: repassado intacto, invisivel ao JS
};

struct HookCtx {
    JSContext* ctx = nullptr;
    const MethodInfo* method = nullptr;
    JSValue callback = JS_UNDEFINED;
    // A funcao `original` entregue ao callback. Criada uma vez na instalacao,
    // nao a cada chamada: o Item.SetDefaults dispara milhares de vezes no
    // carregamento, e cada disparo alocava um objeto-funcao novo.
    JSValue originalFn = JS_UNDEFINED;
    // Para onde original() salta. Pode mudar depois da instalacao: se outro
    // mod hookar o mesmo metodo, o HookManager encadeia e isto passa a apontar
    // para o hook dele. Por isso e lido atomicamente a cada chamada.
    void* original = nullptr;
    int paramCount = 0;
    bool isInstance = false;
    // Metodo de instancia de um STRUCT: o x0 aponta para os dados, nao para um
    // objeto. Embrulhar como GameObject leria o comeco dos dados como classe.
    Il2CppClass* selfStruct = nullptr;
    bool used = false;
    Ret ret = Ret::Int;
    TypeDesc retDesc;
    std::vector<ParamPlan> params;
};

static HookCtx g_hooks[kMaxHooks];

/** Retorno bruto: o campo que vale depende de HookCtx::ret. */
struct Outcome {
    intptr_t i = 0;
    double f = 0.0;
    // 32 bytes cobrem a maior forma que viaja em registrador: HFA de 4 doubles.
    uint8_t s[32] = {0};
};

struct Frame {
    HookCtx* c;
    intptr_t a[8];
    // BITS crus de v0-v7, nao "doubles". Um argumento `float` viaja nos 32 bits
    // BAIXOS do registrador (s0), entao ler os 64 como double da um denormal
    // (180.0f virou 5.57e-315). Guardamos o padrao de bits e interpretamos
    // conforme o tipo declarado do parametro.
    uint64_t d[8];
    bool ranOriginal = false;
    Outcome originalResult;
};
static thread_local std::vector<Frame> g_frames;

// Profundidade de reentrancia por slot, por thread. Evita que um metodo cujo
// corpo real (rodado via original()) rechama a si mesmo pela entrada patcheada
// dispare o callback JS repetidamente ate estourar a pilha do QuickJS.
static thread_local int g_depth[kMaxHooks];

// Quanto um hook espera pelo motor JS antes de desistir do mod naquela
// chamada. Ver JsLock: esperar para sempre transformaria um impasse entre
// threads num jogo congelado.
constexpr int kLockTimeoutMs = 3000;
static std::atomic<bool> g_lockWarned[kMaxHooks];

#define BL_HOOK_PARAMS                                                    \
    intptr_t a0, intptr_t a1, intptr_t a2, intptr_t a3, intptr_t a4,      \
    intptr_t a5, intptr_t a6, intptr_t a7, double f0, double f1,          \
    double f2, double f3, double f4, double f5, double f6, double f7
#define BL_HOOK_ARGS a0, a1, a2, a3, a4, a5, a6, a7, f0, f1, f2, f3, f4, f5, f6, f7

using RawI = intptr_t (*)(BL_HOOK_PARAMS);
using RawF = float (*)(BL_HOOK_PARAMS);
using RawD = double (*)(BL_HOOK_PARAMS);

/**
 * Chama o metodo real com um conjunto de registradores.
 *
 * Solta o motor JS enquanto isso: o corpo do metodo do jogo nao e tempo de JS,
 * e segurar a trava ali e o que fazia um hook em metodo quente bloquear as
 * outras threads por 3 s ate elas desistirem do mod. Fora do callback (a
 * reentrancia, o timeout) a trava nem esta nossa e o JsSuspend nao faz nada.
 */
static Outcome callOriginal(HookCtx* c, const intptr_t a[8], const uint64_t d[8]) {
    Outcome o;
    void* fn = __atomic_load_n(&c->original, __ATOMIC_ACQUIRE);
    if (!fn) return o;
    JsSuspend solta;
    double f[8];
    std::memcpy(f, d, sizeof(f));

#define BL_ARGS_OUT a[0], a[1], a[2], a[3], a[4], a[5], a[6], a[7],                     f[0], f[1], f[2], f[3], f[4], f[5], f[6], f[7]
    // O tipo de retorno do ponteiro de funcao E a ABI: chamar um metodo que
    // devolve Vector2 atraves de um ponteiro que devolve intptr_t leria x0
    // enquanto o valor esta em s0/s1.
#define BL_CALL_STRUCT(T)                                                         do {                                                                              T r = reinterpret_cast<T (*)(BL_HOOK_PARAMS)>(fn)(BL_ARGS_OUT);               std::memcpy(o.s, &r, sizeof(r));                                          } while (0)

    switch (c->ret) {
        case Ret::F32: o.f = reinterpret_cast<RawF>(fn)(BL_ARGS_OUT); break;
        case Ret::F64: o.f = reinterpret_cast<RawD>(fn)(BL_ARGS_OUT); break;
        case Ret::S8:  BL_CALL_STRUCT(S8);  break;
        case Ret::S16: BL_CALL_STRUCT(S16); break;
        case Ret::H1F: BL_CALL_STRUCT(H1F); break;
        case Ret::H2F: BL_CALL_STRUCT(H2F); break;
        case Ret::H3F: BL_CALL_STRUCT(H3F); break;
        case Ret::H4F: BL_CALL_STRUCT(H4F); break;
        case Ret::H1D: BL_CALL_STRUCT(H1D); break;
        case Ret::H2D: BL_CALL_STRUCT(H2D); break;
        case Ret::H3D: BL_CALL_STRUCT(H3D); break;
        case Ret::H4D: BL_CALL_STRUCT(H4D); break;
        case Ret::Int: o.i = reinterpret_cast<RawI>(fn)(BL_ARGS_OUT); break;
    }
#undef BL_CALL_STRUCT
#undef BL_ARGS_OUT
    return o;
}

// -------------------- registradores <-> valores JS --------------------

/** Tamanho de cada casa de um HFA (struct so de float, ou so de double). */
static size_t hfaSlot(const TypeDesc& d) {
    bool dbl = false;
    int n = hfaOf(d.cls, &dbl);
    return (n > 0 && dbl) ? 8u : 4u;
}

static JSValue paramToJs(JSContext* ctx, const Frame& f, const ParamPlan& p) {
    if (p.opaque) return JS_UNDEFINED;

    if (p.d.prim == Prim::Struct) {
        if (p.structByRef) {
            // Struct grande: o registrador tem o endereco de uma copia feita
            // pelo chamador. Copiamos de novo — semantica de valor do C#.
            return makeStructCopy(ctx, p.d.cls,
                                  reinterpret_cast<void*>(f.a[p.reg]), p.d.size);
        }
        // Struct em registrador cabe em 32 bytes: HFA de ate 4 doubles, ou
        // ate 16 bytes na fila inteira (maior que isso vai por ponteiro).
        uint8_t buf[32] = {0};
        const size_t cap = p.d.size < sizeof(buf) ? p.d.size : sizeof(buf);
        if (p.floatQueue) {
            size_t slot = hfaSlot(p.d);
            for (int i = 0; i < p.regs; ++i) {
                size_t at = static_cast<size_t>(i) * slot;
                if (at + slot > cap) break;
                std::memcpy(buf + at, &f.d[p.reg + i], slot);
            }
        } else {
            size_t n = cap < static_cast<size_t>(p.regs) * 8
                           ? cap : static_cast<size_t>(p.regs) * 8;
            std::memcpy(buf, &f.a[p.reg], n);
        }
        return makeStructCopy(ctx, p.d.cls, buf, p.d.size);
    }

    if (p.floatQueue) {
        uint64_t bits = f.d[p.reg];
        if (p.d.prim == Prim::F32) {
            uint32_t lo = static_cast<uint32_t>(bits);
            float v;
            std::memcpy(&v, &lo, sizeof(v));
            return JS_NewFloat64(ctx, v);
        }
        double v;
        std::memcpy(&v, &bits, sizeof(v));
        return JS_NewFloat64(ctx, v);
    }

    // Fila inteira: o valor esta nos bytes BAIXOS do registrador. readAt le
    // exatamente o tamanho declarado — e o que conserta enum de 1 byte e
    // int com lixo nos 32 bits altos.
    return readAt(ctx, const_cast<intptr_t*>(&f.a[p.reg]), p.d, JS_UNDEFINED);
}

/** Valor JS -> registradores, para original() com argumento editado. */
static int jsToParam(JSContext* ctx, JSValueConst v, const ParamPlan& p,
                     intptr_t a[8], uint64_t d[8]) {
    if (p.opaque) return true;  // ref/out: mantem o que o chamador mandou

    if (p.d.prim == Prim::Struct) {
        Il2CppClass* cls = nullptr;
        size_t size = 0;
        void* src = structDataOf(v, &cls, &size);
        if (!src) {
            JS_ThrowTypeError(ctx, "esperava um %s", p.d.name.c_str());
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

static JSValue returnToJs(HookCtx* c, const Outcome& o) {
    JSContext* ctx = c->ctx;
    const TypeDesc& d = c->retDesc;
    if (d.prim == Prim::Void) return JS_UNDEFINED;
    // Copia, nao vista: os bytes estao num Outcome da nossa pilha, que some
    // quando a chamada volta.
    if (d.prim == Prim::Struct) return makeStructCopy(ctx, d.cls, o.s, d.size);
    if (c->ret == Ret::F32 || c->ret == Ret::F64) return JS_NewFloat64(ctx, o.f);
    intptr_t raw = o.i;
    return readAt(ctx, &raw, d, JS_UNDEFINED);
}

/** O que o callback devolveu vira o retorno do metodo. */
static Outcome jsToReturn(HookCtx* c, JSValueConst v, const Outcome& fallback) {
    JSContext* ctx = c->ctx;
    if (JS_IsUndefined(v) || JS_IsException(v)) return fallback;
    if (c->retDesc.prim == Prim::Void) return fallback;
    Outcome o;
    if (c->retDesc.prim == Prim::Struct) {
        Il2CppClass* src = nullptr;
        size_t n = 0;
        void* p = structDataOf(v, &src, &n);
        // Devolveu outra coisa que nao um struct do tipo certo: fica o do
        // original, como acontece com qualquer retorno que nao converte.
        if (!p || (src && c->retDesc.cls && src != c->retDesc.cls)) return fallback;
        size_t cap = c->retDesc.size < sizeof(o.s) ? c->retDesc.size : sizeof(o.s);
        std::memcpy(o.s, p, n < cap ? n : cap);
        return o;
    }
    if (c->ret == Ret::F32 || c->ret == Ret::F64) {
        double x = 0;
        if (JS_ToFloat64(ctx, &x, v) < 0) {
            JS_FreeValue(ctx, JS_GetException(ctx));
            return fallback;
        }
        o.f = x;
        return o;
    }
    intptr_t raw = 0;
    if (writeAt(ctx, &raw, c->retDesc, v) < 0) {
        JS_FreeValue(ctx, JS_GetException(ctx));
        return fallback;
    }
    o.i = raw;
    return o;
}

// ------------------------------ original() ------------------------------

/**
 * original([self,] ...args) — roda o metodo real.
 *
 * Sem argumento nenhum, reexecuta com os registradores exatos que chegaram.
 * Com argumentos, cada um SUBSTITUI o seu (o que faltar fica como veio), que e
 * como se edita uma entrada antes de deixar o jogo processa-la:
 *
 *     original(self, dano * 2, knockback)
 *
 * O que NAO e reescrito continua intacto — inclusive o `const MethodInfo*` que
 * o IL2CPP passa como ultimo argumento de todo metodo, e que nao aparece na
 * lista de parametros. Por isso partimos dos registradores salvos em vez de
 * montar um conjunto novo do zero.
 */
static JSValue js_original(JSContext* ctx, JSValueConst, int argc, JSValueConst* argv,
                           int slot) {
    // Cada hook tem a SUA funcao original, marcada com o slot. Guardada numa
    // variavel e chamada depois — fora do callback, ou dentro do hook de outro
    // metodo — ela rodaria com os registradores de outra chamada.
    if (g_frames.empty() || g_frames.back().c != &g_hooks[slot]) {
        return JS_ThrowTypeError(ctx, "original() so vale dentro do proprio hook, "
                                      "durante a chamada");
    }
    Frame& f = g_frames.back();
    HookCtx* c = f.c;

    intptr_t a[8];
    uint64_t d[8];
    std::memcpy(a, f.a, sizeof(a));
    std::memcpy(d, f.d, sizeof(d));

    int at = 0;
    if (c->isInstance && at < argc) {
        if (void* s = structDataOf(argv[at], nullptr, nullptr)) {
            a[0] = reinterpret_cast<intptr_t>(s);
        } else if (Il2CppObject* o = objectFromJS(argv[at])) {
            a[0] = reinterpret_cast<intptr_t>(o);
        }
        ++at;
    }
    for (size_t i = 0; i < c->params.size() && at < argc; ++i, ++at) {
        if (jsToParam(ctx, argv[at], c->params[i], a, d) < 0) return JS_EXCEPTION;
    }

    f.ranOriginal = true;
    f.originalResult = callOriginal(c, a, d);
    return returnToJs(c, f.originalResult);
}

// ---------------------------- dispatcher ----------------------------

static Outcome dispatch(BL_HOOK_PARAMS, int slot) {
    Outcome result;
    if (slot < 0 || slot >= kMaxHooks) return result;
    HookCtx* c = &g_hooks[slot];

    const intptr_t rawA[8] = {a0, a1, a2, a3, a4, a5, a6, a7};
    const double rawF[8] = {f0, f1, f2, f3, f4, f5, f6, f7};
    uint64_t rawD[8];
    std::memcpy(rawD, rawF, sizeof(rawD));

    // Reentrancia: ja estamos dentro deste hook nesta thread (o corpo real,
    // rodando via original(), rechamou o metodo pela entrada patcheada). Nao
    // dispara o callback de novo — executa direto o original e volta.
    if (g_depth[slot] > 0) return callOriginal(c, rawA, rawD);

    JsLock lock(kLockTimeoutMs);
    if (!lock.held()) {
        if (!g_lockWarned[slot].exchange(true)) {
            BL_ERROR("hook %s: motor JS ocupado por outra thread ha %d ms; "
                     "rodando o metodo sem o mod (aviso unico por hook)",
                     il2cpp::api().method_get_name(c->method), kLockTimeoutMs);
        }
        return callOriginal(c, rawA, rawD);
    }
    ++g_depth[slot];

    JSContext* ctx = c->ctx;
    Frame f{c, {}, {}};
    std::memcpy(f.a, rawA, sizeof(f.a));
    std::memcpy(f.d, rawD, sizeof(f.d));
    g_frames.push_back(f);

    // argv: [0]=original, [1]=self (se instancia), [2..]=parametros
    JSValue argv[2 + 16];
    int argc = 0;
    argv[argc++] = JS_DupValue(ctx, c->originalFn);
    if (c->selfStruct) {
        // Vista, nao copia: original(self) precisa repassar o MESMO endereco,
        // senao um metodo que altera o struct alteraria a nossa copia. Sem
        // dono conhecido, vale so durante o callback.
        argv[argc++] = makeStructView(ctx, c->selfStruct, reinterpret_cast<void*>(rawA[0]),
                                      JS_UNDEFINED);
    } else if (c->isInstance) {
        argv[argc++] = makeNativeObject(ctx, reinterpret_cast<Il2CppObject*>(rawA[0]));
    }
    for (const ParamPlan& p : c->params) {
        if (argc >= static_cast<int>(sizeof(argv) / sizeof(argv[0]))) break;
        argv[argc++] = paramToJs(ctx, g_frames.back(), p);
    }

    // O limite de pilha do QuickJS ja foi realinhado com esta thread pelo
    // JsLock (ver ScriptEngine.h). Sem isso os mods, carregados na thread da
    // sonda, recusavam o callback na thread do jogo com "Maximum call stack
    // size exceeded".
    JSValue ret = JS_Call(ctx, c->callback, JS_UNDEFINED, argc, argv);
    if (JS_IsException(ret)) {
        JSValue e = JS_GetException(ctx);
        const char* t = JS_ToCString(ctx, e);
        BL_ERROR("hook: excecao no callback: %s", t ? t : "?");
        if (t) JS_FreeCString(ctx, t);
        JS_FreeValue(ctx, e);

        // Mod quebrado NAO pode quebrar o jogo. Sem isto, um callback que
        // estoura engole a chamada ao metodo real — e foi exatamente o que
        // aconteceu com Item.SetDefaults: todo item passou a nascer sem os
        // defaults, aparecendo so com o nome e sem funcionar.
        //
        // So no caminho de EXCECAO: um callback que roda inteiro e escolhe nao
        // chamar original() esta suprimindo o metodo de proposito.
        if (!g_frames.back().ranOriginal) {
            g_frames.back().originalResult = callOriginal(c, rawA, rawD);
        }
    }
    // O que o metodo devolve: o valor do callback tem prioridade; senao o do
    // original, se ele foi chamado.
    result = jsToReturn(c, ret, g_frames.back().originalResult);

    for (int i = 0; i < argc; ++i) JS_FreeValue(ctx, argv[i]);
    JS_FreeValue(ctx, ret);

    g_frames.pop_back();
    --g_depth[slot];
    return result;
}

// --- funcoes de substituicao geradas em tempo de compilacao ---
// Uma por slot, em tres familias, porque o tipo de RETORNO faz parte da ABI.

#define BL_LIST_64 \
 X(0)X(1)X(2)X(3)X(4)X(5)X(6)X(7)X(8)X(9)X(10)X(11)X(12)X(13)X(14)X(15) \
 X(16)X(17)X(18)X(19)X(20)X(21)X(22)X(23)X(24)X(25)X(26)X(27)X(28)X(29)X(30)X(31) \
 X(32)X(33)X(34)X(35)X(36)X(37)X(38)X(39)X(40)X(41)X(42)X(43)X(44)X(45)X(46)X(47) \
 X(48)X(49)X(50)X(51)X(52)X(53)X(54)X(55)X(56)X(57)X(58)X(59)X(60)X(61)X(62)X(63)

#define BL_LIST_16 \
 X(0)X(1)X(2)X(3)X(4)X(5)X(6)X(7)X(8)X(9)X(10)X(11)X(12)X(13)X(14)X(15)

// O parametro da macro NAO pode se chamar `i`: o pre-processador substituiria
// tambem o `.i` do acesso ao campo de Outcome, virando `.0`.
#define X(n) \
    static intptr_t bl_hook_i##n(BL_HOOK_PARAMS) { \
        return dispatch(BL_HOOK_ARGS, n).i; \
    }
BL_LIST_64
#undef X

#define X(n) \
    static float bl_hook_f##n(BL_HOOK_PARAMS) { \
        return static_cast<float>(dispatch(BL_HOOK_ARGS, kIntHooks + n).f); \
    }
BL_LIST_16
#undef X

#define X(n) \
    static double bl_hook_d##n(BL_HOOK_PARAMS) { \
        return dispatch(BL_HOOK_ARGS, kIntHooks + kFltHooks + n).f; \
    }
BL_LIST_16
#undef X

/** Os bytes do Outcome no formato do carregador. */
template <class T>
static inline T carry(const Outcome& o) {
    T r;
    std::memcpy(&r, o.s, sizeof(r));
    return r;
}

// Quatro slots por forma. O numero da forma multiplica kStructSlots, entao a
// ordem aqui tem de bater com a do enum Ret (S8, S16, H1F..H4F, H1D..H4D) — e
// a mesma que o slotRange usa para achar o intervalo.
#define BL_GEN_STRUCT(sufixo, T, forma)                                      \
    static T bl_hook_##sufixo##0(BL_HOOK_PARAMS) {                           \
        return carry<T>(dispatch(BL_HOOK_ARGS,                               \
                        kBaseStruct + (forma) * kStructSlots + 0));          \
    }                                                                        \
    static T bl_hook_##sufixo##1(BL_HOOK_PARAMS) {                           \
        return carry<T>(dispatch(BL_HOOK_ARGS,                               \
                        kBaseStruct + (forma) * kStructSlots + 1));          \
    }                                                                        \
    static T bl_hook_##sufixo##2(BL_HOOK_PARAMS) {                           \
        return carry<T>(dispatch(BL_HOOK_ARGS,                               \
                        kBaseStruct + (forma) * kStructSlots + 2));          \
    }                                                                        \
    static T bl_hook_##sufixo##3(BL_HOOK_PARAMS) {                           \
        return carry<T>(dispatch(BL_HOOK_ARGS,                               \
                        kBaseStruct + (forma) * kStructSlots + 3));          \
    }

BL_GEN_STRUCT(s8,  S8,  0)
BL_GEN_STRUCT(s16, S16, 1)
BL_GEN_STRUCT(h1f, H1F, 2)
BL_GEN_STRUCT(h2f, H2F, 3)
BL_GEN_STRUCT(h3f, H3F, 4)
BL_GEN_STRUCT(h4f, H4F, 5)
BL_GEN_STRUCT(h1d, H1D, 6)
BL_GEN_STRUCT(h2d, H2D, 7)
BL_GEN_STRUCT(h3d, H3D, 8)
BL_GEN_STRUCT(h4d, H4D, 9)
#undef BL_GEN_STRUCT

static void* const g_stubs[kMaxHooks] = {
#define X(n) reinterpret_cast<void*>(&bl_hook_i##n),
    BL_LIST_64
#undef X
#define X(n) reinterpret_cast<void*>(&bl_hook_f##n),
    BL_LIST_16
#undef X
#define X(n) reinterpret_cast<void*>(&bl_hook_d##n),
    BL_LIST_16
#undef X
#define BL_PTRS(sufixo)                            \
    reinterpret_cast<void*>(&bl_hook_##sufixo##0), \
    reinterpret_cast<void*>(&bl_hook_##sufixo##1), \
    reinterpret_cast<void*>(&bl_hook_##sufixo##2), \
    reinterpret_cast<void*>(&bl_hook_##sufixo##3),
    BL_PTRS(s8) BL_PTRS(s16)
    BL_PTRS(h1f) BL_PTRS(h2f) BL_PTRS(h3f) BL_PTRS(h4f)
    BL_PTRS(h1d) BL_PTRS(h2d) BL_PTRS(h3d) BL_PTRS(h4d)
#undef BL_PTRS
};
static_assert(sizeof(g_stubs) / sizeof(g_stubs[0]) == kMaxHooks,
              "a tabela de stubs tem de cobrir exatamente os slots");

// ---------------------------- instalacao ----------------------------

/**
 * Distribui os parametros pelas filas de registrador, seguindo o AAPCS.
 * @return mensagem de erro, ou vazio se der para reproduzir a chamada.
 */
static std::string buildPlan(HookCtx& c) {
    auto& api = il2cpp::api();
    c.retDesc = describe(api.method_get_return_type(c.method));
    if (c.isInstance && api.method_get_class && api.class_is_valuetype) {
        Il2CppClass* owner = api.method_get_class(c.method);
        if (owner && api.class_is_valuetype(owner)) c.selfStruct = owner;
    }

    if (c.retDesc.prim == Prim::Struct) {
        bool dbl = false;
        int hfa = hfaOf(c.retDesc.cls, &dbl);
        if (hfa >= 1 && hfa <= 4) {
            // O enum tem H1F..H4F e H1D..H4D consecutivos justamente para isto.
            int base = static_cast<int>(dbl ? Ret::H1D : Ret::H1F);
            c.ret = static_cast<Ret>(base + hfa - 1);
        } else if (c.retDesc.size <= 8) {
            c.ret = Ret::S8;
        } else if (c.retDesc.size <= 16) {
            c.ret = Ret::S16;
        } else {
            // Acima de 16 bytes (e sem ser HFA) o valor volta pela MEMORIA: o
            // chamador reserva o espaco e passa o endereco em x8, que nao e
            // registrador de argumento e nao chega numa funcao C sem asm.
            return "metodo devolve " + c.retDesc.name + ", de " +
                   std::to_string(c.retDesc.size) +
                   " bytes: acima de 16 o struct volta pela memoria (endereco em"
                   " x8), que a nossa captura nao alcanca";
        }
    } else {
        c.ret = c.retDesc.prim == Prim::F32 ? Ret::F32
              : c.retDesc.prim == Prim::F64 ? Ret::F64
                                            : Ret::Int;
    }

    int x = c.isInstance ? 1 : 0;  // `this` ocupa x0
    int dq = 0;
    uint32_t n = api.method_get_param_count(c.method);
    c.params.clear();
    for (uint32_t i = 0; i < n; ++i) {
        ParamPlan p;
        p.d = describe(api.method_get_param(c.method, i));
        if (p.d.byRef) {
            // `ref`/`out` chega como ponteiro. Repassamos intacto; expor o
            // endereco cru ao JS so serviria para alguem escrever nele.
            p.opaque = true;
            p.reg = x++;
        } else if (p.d.prim == Prim::F32 || p.d.prim == Prim::F64) {
            p.floatQueue = true;
            p.reg = dq++;
        } else if (p.d.prim == Prim::Struct) {
            bool dbl = false;
            int hfa = hfaOf(p.d.cls, &dbl);
            if (hfa > 0) {
                p.floatQueue = true;   // Vector2 = {float,float} -> s0,s1
                p.reg = dq;
                p.regs = hfa;
                dq += hfa;
            } else if (p.d.size > 16) {
                p.structByRef = true;  // grande: vai o endereco de uma copia
                p.reg = x++;
            } else {
                p.regs = p.d.size > 8 ? 2 : 1;
                p.reg = x;
                x += p.regs;
            }
        } else {
            p.reg = x++;
        }
        c.params.push_back(p);
    }
    // Todo metodo do IL2CPP recebe um `const MethodInfo*` a mais, no fim.
    ++x;
    if (x > 8 || dq > 8) {
        return "metodo com argumentos demais para a captura (" + std::to_string(x) +
               " inteiros, " + std::to_string(dq) +
               " de ponto flutuante; o limite e 8 de cada) — o resto viaja pela"
               " pilha, que nao capturamos";
    }
    return {};
}

bool installJsHook(JSContext* ctx, const MethodInfo* method, int paramCount,
                   bool isInstance, JSValueConst callback) {
    if (!method) return false;

    HookCtx probe;
    probe.method = method;
    probe.isInstance = isInstance;
    std::string err = buildPlan(probe);
    if (!err.empty()) {
        JS_ThrowTypeError(ctx, "hook em '%s': %s",
                          il2cpp::api().method_get_name(method), err.c_str());
        return false;
    }

    // A familia depende do tipo de retorno: cada uma tem seu proprio pool,
    // porque o tipo de retorno da funcao de substituicao faz parte da ABI.
    int from = 0, count = 0;
    slotRange(probe.ret, &from, &count);

    for (int i = from; i < from + count; ++i) {
        if (g_hooks[i].used) continue;
        HookCtx& c = g_hooks[i];
        c = probe;
        c.ctx = ctx;
        c.callback = JS_DupValue(ctx, callback);  // mantem vivo
        c.originalFn = JS_NewCFunctionMagic(ctx, js_original, "original", 0,
                                            JS_CFUNC_generic_magic, i);
        c.paramCount = paramCount;
        c.used = true;
        if (!hook::install(method, g_stubs[i], &c.original)) {
            JS_FreeValue(ctx, c.callback);
            JS_FreeValue(ctx, c.originalFn);
            c.used = false;
            JS_ThrowInternalError(ctx, "hook: o ShadowHook recusou o metodo");
            return false;
        }
        BL_INFO("hook JS no slot %d: %s", i, il2cpp::describeMethod(method).c_str());
        return true;
    }
    JS_ThrowInternalError(ctx, "hook: sem slots livres para retorno '%s' "
                               "(%d nessa forma, todos em uso)",
                          probe.retDesc.name.c_str(), count);
    return false;
}

#else // arquitetura != arm64: hook JS indisponivel (stub)

bool installJsHook(JSContext* ctx, const MethodInfo*, int, bool, JSValueConst) {
    JS_ThrowInternalError(ctx, "hook JS: so implementado em arm64");
    return false;
}

#endif // __aarch64__

} // namespace bl::script

#endif // BL_HAVE_QUICKJS
