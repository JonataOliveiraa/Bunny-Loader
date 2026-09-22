#include "script/Bridge.h"
#include "core/Log.h"
#include "hook/HookManager.h"
#include "il2cpp/Api.h"

#if BL_HAVE_QUICKJS
#include "quickjs.h"
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
// Limitacoes v1: captura só os 8 registradores inteiros (x0-x7); argumentos
// float/double e retorno nao-void ainda nao. original() reexecuta o metodo com
// os args ORIGINAIS (nao honra modificacao de args antes do original). Cobre o
// caso comum (ex.: Minishark: original(self,type) e depois mexe em campos).

constexpr int kMaxHooks = 64;

struct HookCtx {
    JSContext* ctx = nullptr;
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
struct Frame { HookCtx* c; intptr_t a[8]; };
static thread_local std::vector<Frame> g_frames;

using RawFn8 = intptr_t (*)(intptr_t, intptr_t, intptr_t, intptr_t,
                            intptr_t, intptr_t, intptr_t, intptr_t);

// original(): reexecuta o metodo real com os registradores salvos do frame topo.
static JSValue js_original(JSContext*, JSValueConst, int, JSValueConst*) {
    if (g_frames.empty()) return JS_UNDEFINED;
    Frame& f = g_frames.back();
    auto fn = reinterpret_cast<RawFn8>(f.c->original);
    fn(f.a[0], f.a[1], f.a[2], f.a[3], f.a[4], f.a[5], f.a[6], f.a[7]);
    return JS_UNDEFINED;
}

// Dispatcher comum. Recebe os args do metodo em x0-x7 (assinatura de 8 inteiros).
extern "C" intptr_t bl_hook_repl(intptr_t a0, intptr_t a1, intptr_t a2, intptr_t a3,
                                 intptr_t a4, intptr_t a5, intptr_t a6, intptr_t a7) {
    int slot = bl_hook_slot;
    if (slot < 0 || slot >= kMaxHooks) return 0;
    HookCtx* c = &g_hooks[slot];
    JSContext* ctx = c->ctx;

    Frame f{c, {a0, a1, a2, a3, a4, a5, a6, a7}};
    g_frames.push_back(f);

    // argv: [0]=original, [1]=self (se instancia), [2..]=params inteiros
    JSValue argv[2 + 8];
    int argc = 0;
    argv[argc++] = JS_NewCFunction(ctx, js_original, "original", 0);

    int base = 0;  // indice em `a[]` onde comecam os params
    if (c->isInstance) {
        argv[argc++] = makeNativeObject(ctx, reinterpret_cast<Il2CppObject*>(a0));
        base = 1;
    }
    for (int i = 0; i < c->paramCount && argc < 2 + 8; ++i) {
        argv[argc++] = JS_NewInt64(ctx, f.a[base + i]);
    }

    JSValue ret = JS_Call(ctx, c->callback, JS_UNDEFINED, argc, argv);
    if (JS_IsException(ret)) {
        JSValue e = JS_GetException(ctx);
        const char* t = JS_ToCString(ctx, e);
        BL_ERROR("hook: excecao no callback: %s", t ? t : "?");
        if (t) JS_FreeCString(ctx, t);
        JS_FreeValue(ctx, e);
    }
    for (int i = 0; i < argc; ++i) JS_FreeValue(ctx, argv[i]);
    JS_FreeValue(ctx, ret);

    g_frames.pop_back();
    return 0;
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
