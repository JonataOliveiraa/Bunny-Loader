#include "script/Bridge.h"
#include "script/Marshal.h"
#include "core/Log.h"
#include "hook/HookManager.h"
#include "il2cpp/Api.h"

#if BL_HAVE_QUICKJS
#include "quickjs.h"
#include <cstring>
#include <string>
#include <vector>

namespace bl::script {

#if defined(__aarch64__)

// ================== hook JS -> metodo do jogo (arm64) ==================
//
// Como cada hook precisa do proprio contexto mas o ShadowHook usa uma unica
// funcao de substituicao por alvo, geramos em tempo de COMPILACAO N stubs. Cada
// stub grava seu indice num global e salta pro dispatcher comum, preservando
// x0-x7 (os argumentos). Nada de codegen em runtime — e ARM compilado, que o
// houdini ja traduz (como o resto da libbunny).
//
// FLOATS: o dispatcher e declarado com 8 inteiros SEGUIDOS de 8 doubles. Pelo
// AAPCS do arm64 os 8 primeiros argumentos inteiros vao em x0-x7 e os 8
// primeiros de ponto flutuante em d0-d7, em filas SEPARADAS — declarar assim
// faz o compilador ler exatamente esses registradores, sem asm nenhum. Antes
// so x0-x7 era capturado, e um hook em `Player.Update(float dt)` recebia lixo.
//
// A distribuicao tambem e por fila: ao montar os argumentos do JS, parametro
// inteiro consome o proximo x, parametro float consome o proximo d.
//
// Limitacoes que ficam: original() reexecuta com os args ORIGINAIS (nao honra
// edicao de argumento antes da chamada), e o retorno so viaja por registrador
// inteiro (metodo que devolve float ainda nao da para alterar).

constexpr int kMaxHooks = 64;

struct HookCtx {
    JSContext* ctx = nullptr;
    const MethodInfo* method = nullptr;  // tipos dos params e do retorno
    JSValue callback = JS_UNDEFINED;
    void* original = nullptr;
    int paramCount = 0;
    bool isInstance = false;
    bool used = false;
};

static HookCtx g_hooks[kMaxHooks];

// Escrito pelo stub imediatamente antes de saltar ao dispatcher.
extern "C" volatile int bl_hook_slot;
volatile int bl_hook_slot = 0;

// Frame por invocacao (para original() reexecutar com os args salvos).
// `ranOriginal` existe para o caso de o callback JS falhar: aí chamamos o
// metodo real no lugar dele, em vez de engolir a chamada.
struct Frame {
    HookCtx* c;
    intptr_t a[8];
    // BITS crus de v0-v7, nao "doubles". Um argumento `float` viaja nos 32 bits
    // BAIXOS do registrador (s0), entao ler os 64 como double da um denormal
    // (180.0f virou 5.57e-315). Guardamos o padrao de bits e interpretamos
    // conforme o tipo declarado do parametro.
    uint64_t d[8];
    bool ranOriginal = false;
    intptr_t originalResult = 0;
};
static thread_local std::vector<Frame> g_frames;

// Profundidade de reentrancia por slot, por thread. Evita que um metodo cujo
// corpo real (rodado via original()) rechama a si mesmo pela entrada patcheada
// dispare o callback JS repetidamente ate estourar a pilha do QuickJS.
static thread_local int g_depth[kMaxHooks];

using RawFn = intptr_t (*)(intptr_t, intptr_t, intptr_t, intptr_t,
                           intptr_t, intptr_t, intptr_t, intptr_t,
                           double, double, double, double,
                           double, double, double, double);

/** Nome do tipo de retorno do metodo, ou vazio. */
static std::string returnTypeName(const HookCtx* c) {
    auto& api = il2cpp::api();
    if (!c->method || !api.method_get_return_type || !api.type_get_name) return {};
    char* tn = api.type_get_name(api.method_get_return_type(c->method));
    if (!tn) return {};
    std::string t(tn);
    api.il2cpp_free(tn);
    return t;
}

/**
 * Retorno bruto (x0) -> valor JS, para `const r = original(...)`.
 *
 * Float/double voltam em d0, que esta funcao nao ve: metodo que devolve ponto
 * flutuante entrega undefined. Conhecido, e o mesmo motivo de nao dar para
 * ALTERAR esse retorno (ver o cabecalho do arquivo).
 */
static JSValue returnToJs(HookCtx* c, intptr_t raw) {
    JSContext* ctx = c->ctx;
    std::string t = returnTypeName(c);
    if (t.empty() || t == "System.Void") return JS_UNDEFINED;
    if (t == "System.Boolean") return JS_NewBool(ctx, (raw & 0xff) != 0);
    if (t == "System.Single" || t == "System.Double") return JS_UNDEFINED;
    if (t == "System.Byte")   return JS_NewInt32(ctx, static_cast<uint8_t>(raw));
    if (t == "System.SByte")  return JS_NewInt32(ctx, static_cast<int8_t>(raw));
    if (t == "System.Int16")  return JS_NewInt32(ctx, static_cast<int16_t>(raw));
    if (t == "System.UInt16" || t == "System.Char") {
        return JS_NewInt32(ctx, static_cast<uint16_t>(raw));
    }
    if (t == "System.Int32" || t == "System.UInt32") {
        return JS_NewInt32(ctx, static_cast<int32_t>(raw));
    }
    if (t == "System.Int64" || t == "System.UInt64") return JS_NewInt64(ctx, raw);
    if (!raw) return JS_NULL;
    if (t == "System.String") {
        return JS_NewString(ctx, stringToUtf8(reinterpret_cast<Il2CppString*>(raw)).c_str());
    }
    return makeNativeObject(ctx, reinterpret_cast<Il2CppObject*>(raw));
}

/** Valor JS -> retorno bruto, quando o callback decide o que o metodo devolve. */
static intptr_t jsToRaw(JSContext* ctx, JSValueConst v, intptr_t fallback) {
    if (JS_IsUndefined(v) || JS_IsException(v)) return fallback;
    if (JS_IsNull(v)) return 0;
    if (JS_IsBool(v)) return JS_ToBool(ctx, v) ? 1 : 0;
    if (JS_IsNumber(v)) {
        int64_t x = 0;
        if (JS_ToInt64(ctx, &x, v) < 0) return fallback;
        return static_cast<intptr_t>(x);
    }
    if (Il2CppObject* o = objectFromJS(v)) return reinterpret_cast<intptr_t>(o);
    return fallback;
}

// original(): reexecuta o metodo real com os registradores salvos do frame topo.
static JSValue js_original(JSContext*, JSValueConst, int, JSValueConst*) {
    if (g_frames.empty()) return JS_UNDEFINED;
    Frame& f = g_frames.back();
    f.ranOriginal = true;
    auto fn = reinterpret_cast<RawFn>(f.c->original);
    double fd[8];
    std::memcpy(fd, f.d, sizeof(fd));
    f.originalResult = fn(f.a[0], f.a[1], f.a[2], f.a[3], f.a[4], f.a[5], f.a[6], f.a[7],
                          fd[0], fd[1], fd[2], fd[3], fd[4], fd[5], fd[6], fd[7]);
    // O retorno do metodo real chega ao JS: `const r = original(...)`.
    return returnToJs(f.c, f.originalResult);
}

// Dispatcher comum. Recebe os args do metodo em x0-x7 (assinatura de 8 inteiros).
extern "C" intptr_t bl_hook_repl(intptr_t a0, intptr_t a1, intptr_t a2, intptr_t a3,
                                 intptr_t a4, intptr_t a5, intptr_t a6, intptr_t a7,
                                 double f0, double f1, double f2, double f3,
                                 double f4, double f5, double f6, double f7) {
    int slot = bl_hook_slot;
    if (slot < 0 || slot >= kMaxHooks) return 0;
    HookCtx* c = &g_hooks[slot];

    // Reentrancia: ja estamos dentro deste hook nesta thread (o corpo real,
    // rodando via original(), rechamou o metodo pela entrada patcheada). Nao
    // dispara o callback de novo — executa direto o original e volta.
    if (g_depth[slot] > 0) {
        auto fn = reinterpret_cast<RawFn>(c->original);
        return fn ? fn(a0, a1, a2, a3, a4, a5, a6, a7,
                       f0, f1, f2, f3, f4, f5, f6, f7) : 0;
    }
    ++g_depth[slot];

    JSContext* ctx = c->ctx;

    Frame f{c, {a0, a1, a2, a3, a4, a5, a6, a7}, {}};
    const double fin[8] = {f0, f1, f2, f3, f4, f5, f6, f7};
    std::memcpy(f.d, fin, sizeof(f.d));
    g_frames.push_back(f);

    // argv: [0]=original, [1]=self (se instancia), [2..]=params inteiros
    JSValue argv[2 + 8];
    int argc = 0;
    argv[argc++] = JS_NewCFunction(ctx, js_original, "original", 0);

    // Filas separadas: inteiro consome x, float consome d.
    int xi = 0, di = 0;
    if (c->isInstance) {
        argv[argc++] = makeNativeObject(ctx, reinterpret_cast<Il2CppObject*>(f.a[xi++]));
    }
    auto& api = il2cpp::api();
    for (int i = 0; i < c->paramCount && argc < 2 + 8; ++i) {
        bool isFloat = false, isDouble = false;
        if (c->method && api.method_get_param && api.type_get_name) {
            char* tn = api.type_get_name(api.method_get_param(c->method, i));
            if (tn) {
                isFloat = std::strcmp(tn, "System.Single") == 0;
                isDouble = std::strcmp(tn, "System.Double") == 0;
                api.il2cpp_free(tn);
            }
        }
        if ((isFloat || isDouble) && di < 8) {
            uint64_t bits = f.d[di++];
            double val;
            if (isFloat) {
                uint32_t lo = static_cast<uint32_t>(bits);
                float sv;
                std::memcpy(&sv, &lo, sizeof(sv));
                val = sv;
            } else {
                std::memcpy(&val, &bits, sizeof(val));
            }
            argv[argc++] = JS_NewFloat64(ctx, val);
        } else if (xi < 8) {
            argv[argc++] = JS_NewInt64(ctx, f.a[xi++]);
        } else {
            argv[argc++] = JS_UNDEFINED;
        }
    }

    // O QuickJS deduz o limite de pilha do ponteiro de pilha de quando o
    // runtime nasce. Os mods sao carregados na thread da sonda, mas os hooks
    // disparam na thread do JOGO — e ali a checagem concluia que a pilha tinha
    // acabado, rejeitando o callback na primeira chamada:
    //
    //     hook: excecao no callback: Maximum call stack size exceeded
    //
    // Isto realinha o limite com a thread corrente. Nao ha concorrencia a
    // proteger: depois da carga, JS so roda por hooks, na thread do jogo.
    JS_UpdateStackTop(JS_GetRuntime(ctx));

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
        // Só no caminho de EXCECAO: um callback que roda inteiro e escolhe nao
        // chamar original() esta suprimindo o metodo de proposito.
        if (!g_frames.back().ranOriginal && c->original) {
            auto fn = reinterpret_cast<RawFn>(c->original);
            fn(a0, a1, a2, a3, a4, a5, a6, a7, f0, f1, f2, f3, f4, f5, f6, f7);
        }
    }
    // O que o metodo devolve: o valor do callback tem prioridade; senao o do
    // original, se ele foi chamado. Antes o dispatcher devolvia 0 SEMPRE, o que
    // zerava o retorno de todo metodo hookado que nao fosse void.
    intptr_t result = jsToRaw(ctx, ret, g_frames.back().originalResult);

    for (int i = 0; i < argc; ++i) JS_FreeValue(ctx, argv[i]);
    JS_FreeValue(ctx, ret);

    g_frames.pop_back();
    --g_depth[slot];
    return result;
}

// --- stubs gerados em tempo de compilacao ---
// Cada stub: grava seu indice em bl_hook_slot e salta pro dispatcher, sem
// tocar em x0-x7/d0-d7 (usa x16/x17, que sao scratch).
#define BL_STUB(i)                                              \
    __attribute__((naked)) static void bl_stub_##i() {         \
        __asm__ volatile(                                      \
            "adrp x16, bl_hook_slot\n"                         \
            "add  x16, x16, :lo12:bl_hook_slot\n"             \
            "mov  w17, #" #i "\n"                              \
            "str  w17, [x16]\n"                                \
            "b    bl_hook_repl\n");                            \
    }

#define BL_STUBS_16(base)                                                      \
    BL_STUB(base##0) BL_STUB(base##1) BL_STUB(base##2) BL_STUB(base##3)        \
    BL_STUB(base##4) BL_STUB(base##5) BL_STUB(base##6) BL_STUB(base##7)        \
    BL_STUB(base##8) BL_STUB(base##9)

// Gera 0..63 (via numeros literais para o #i virar decimal correto).
#define BL_LIST \
 X(0)X(1)X(2)X(3)X(4)X(5)X(6)X(7)X(8)X(9)X(10)X(11)X(12)X(13)X(14)X(15) \
 X(16)X(17)X(18)X(19)X(20)X(21)X(22)X(23)X(24)X(25)X(26)X(27)X(28)X(29)X(30)X(31) \
 X(32)X(33)X(34)X(35)X(36)X(37)X(38)X(39)X(40)X(41)X(42)X(43)X(44)X(45)X(46)X(47) \
 X(48)X(49)X(50)X(51)X(52)X(53)X(54)X(55)X(56)X(57)X(58)X(59)X(60)X(61)X(62)X(63)

#define X(i) BL_STUB(i)
BL_LIST
#undef X

static void* const g_stubs[kMaxHooks] = {
#define X(i) reinterpret_cast<void*>(&bl_stub_##i),
    BL_LIST
#undef X
};

bool installJsHook(JSContext* ctx, const MethodInfo* method, int paramCount,
                   bool isInstance, JSValueConst callback) {
    if (!method) return false;
    for (int i = 0; i < kMaxHooks; ++i) {
        if (g_hooks[i].used) continue;
        HookCtx& c = g_hooks[i];
        c.ctx = ctx;
        c.method = method;
        c.callback = JS_DupValue(ctx, callback);  // mantem vivo
        c.paramCount = paramCount;
        c.isInstance = isInstance;
        c.used = true;
        if (!hook::install(method, g_stubs[i], &c.original)) {
            JS_FreeValue(ctx, c.callback);
            c.used = false;
            return false;
        }
        BL_INFO("hook JS instalado no slot %d (params=%d, instancia=%d)",
                i, paramCount, isInstance);
        return true;
    }
    BL_ERROR("hook: sem slots livres (max %d)", kMaxHooks);
    return false;
}

#else // arquitetura != arm64: hook JS indisponivel (stub)

bool installJsHook(JSContext*, const MethodInfo*, int, bool, JSValueConst) {
    BL_ERROR("hook JS: so implementado em arm64");
    return false;
}

#endif // __aarch64__

} // namespace bl::script

#endif // BL_HAVE_QUICKJS
