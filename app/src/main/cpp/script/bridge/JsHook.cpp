#include "script/bridge/Abi.h"
#include "script/bridge/Bridge.h"
#include "script/bridge/Marshal.h"
#include "script/bridge/Ref.h"
#include "content/tiles/TileAccess.h"
#include "script/bridge/Value.h"
#include "core/Log.h"
#include "hook/HookManager.h"
#include "il2cpp/Api.h"
#include "il2cpp/Resolver.h"
#include "il2cpp/Signature.h"

#if BL_HAVE_QUICKJS
#include "quickjs.h"
#include <atomic>
#include <cstring>
#include <string>
#include <utility>
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

// Quantos slots por forma de retorno. Nao custam nada na chamada (a funcao do
// slot ja sabe o proprio numero e vai direto em g_hooks[slot]); custam ~108
// bytes de codigo e ~240 de tabela cada. 64 de int/void acabaram com o Example
// Mod inteiro, os testes e os tiles juntos; o de hoje fica longe do uso real.
constexpr int kIntHooks = 1024;                            // slots 0..1023
constexpr int kFltHooks = 64;                              // slots 1024..1087
constexpr int kDblHooks = 64;                              // slots 1088..1151
// Por forma de struct (Vector2, Color, Rectangle...). Comecou em quatro e a
// propria bateria esgotou a do Vector2; a mensagem de erro diz qual acabou.
constexpr int kStructSlots = 32;
constexpr int kBaseStruct = kIntHooks + kFltHooks + kDblHooks;   // 1152
constexpr int kStructForms = 10;
constexpr int kMaxHooks = kBaseStruct + kStructForms * kStructSlots;

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
    // Como o metodo recebe e devolve, calculado uma vez na instalacao. O mesmo
    // plano que a chamada direta usa, do mesmo lugar (Abi.cpp) — enquanto eram
    // duas copias, uma delas estava sempre errada sobre algum tipo.
    AbiPlan abi;
    // Filtro nativo (HookFilter): o x onde vem o objeto, e o offset do campo.
    int filterReg = -1;
    int32_t filterOffset = -1;
    int32_t filterMin = 0;
    // HookFilter::whileIn: o slot do hook de fora; -1 = sem esse filtro.
    int gateSlot = -1;
    // Filtro pelo tipo do tile: o x do `Tile` (offset), ou os x de i e j.
    int tileReg = -1, tileAtIReg = -1, tileAtJReg = -1;
};

static HookCtx g_hooks[kMaxHooks];

struct Frame {
    HookCtx* c;
    intptr_t a[kIntSlots];   // x0-x7 e as casas da pilha
    // BITS crus de v0-v7, nao "doubles". Um argumento `float` viaja nos 32 bits
    // BAIXOS do registrador (s0), entao ler os 64 como double da um denormal
    // (180.0f virou 5.57e-315). Guardamos o padrao de bits e interpretamos
    // conforme o tipo declarado do parametro.
    uint64_t d[8];
    bool ranOriginal = false;
    Outcome originalResult;
};
// NUNCA segure `Frame&` atravessando uma chamada ao jogo: o original pode
// disparar outro hook nesta thread, o push_back realoca o vector e a
// referencia passa a apontar para memoria liberada. Use o INDICE.
//
// Foi o crash ao criar e ao carregar mundo: dois hooks JS encadeados no
// Item.SetDefaults (HelloMod + itens de mod) aninham na primeira chamada de
// cada thread nova, e a gravacao do resultado caia em memoria ja devolvida —
// que o IL2CPP reusava nas tabelas de genericos (docs/PONTE-OTIMIZACAO.md).
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

// s0-s7: as 8 primeiras casas da PILHA de argumentos (o 9o inteiro em
// diante). Um metodo com menos argumentos deixa ali o que o chamador tinha:
// so lemos, e so repassamos ao original, que ignora o que nao declarou.
#define BL_HOOK_PARAMS                                                    \
    intptr_t a0, intptr_t a1, intptr_t a2, intptr_t a3, intptr_t a4,      \
    intptr_t a5, intptr_t a6, intptr_t a7, double f0, double f1,          \
    double f2, double f3, double f4, double f5, double f6, double f7,     \
    intptr_t s0, intptr_t s1, intptr_t s2, intptr_t s3, intptr_t s4,      \
    intptr_t s5, intptr_t s6, intptr_t s7
#define BL_HOOK_ARGS a0, a1, a2, a3, a4, a5, a6, a7, f0, f1, f2, f3, f4, f5, f6, f7, \
                     s0, s1, s2, s3, s4, s5, s6, s7

/**
 * Chama o metodo real com um conjunto de registradores.
 *
 * `original` pode ter mudado depois da instalacao: se outro mod hookar o mesmo
 * metodo, o HookManager encadeia e isto passa a apontar para o hook dele. Por
 * isso e lido atomicamente a cada chamada.
 */
static Outcome callOriginal(HookCtx* c, const intptr_t a[kIntSlots], const uint64_t d[8]) {
    void* fn = __atomic_load_n(&c->original, __ATOMIC_ACQUIRE);
    bool threw = false;
    Outcome o = callRaw(fn, c->abi, a, d, &threw, /*suspend=*/true);
    if (threw) {
        // Nao ha para quem devolver a excecao: quem chamou foi o JOGO, nao o
        // JS. Engolir e ruim, mas melhor que derrubar o processo — e o log
        // aparece no painel de erro dentro do jogo.
        BL_ERROR("hook %s: o metodo original lancou excecao no jogo",
                 il2cpp::api().method_get_name(c->method));
    }
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
    const size_t frame = g_frames.size() - 1;
    HookCtx* c = g_frames[frame].c;

    intptr_t a[kIntSlots];
    uint64_t d[8];
    std::memcpy(a, g_frames[frame].a, sizeof(a));
    std::memcpy(d, g_frames[frame].d, sizeof(d));

    int at = 0;
    if (c->isInstance && at < argc) {
        if (void* s = structDataOf(argv[at], nullptr, nullptr)) {
            a[0] = reinterpret_cast<intptr_t>(s);
        } else if (Il2CppObject* o = objectFromJS(argv[at])) {
            a[0] = reinterpret_cast<intptr_t>(o);
        }
        ++at;
    }
    ArgScratch scratch;   // vive ate o original voltar
    for (size_t i = 0; i < c->abi.params.size() && at < argc; ++i, ++at) {
        if (jsToParam(ctx, argv[at], c->abi.params[i], a, d, &scratch) < 0) return JS_EXCEPTION;
    }

    g_frames[frame].ranOriginal = true;
    const Outcome result = callOriginal(c, a, d);   // pode empilhar outros frames
    g_frames[frame].originalResult = result;
    return outcomeToJs(c->ctx, c->abi, result);
}

// ---------------------------- dispatcher ----------------------------

static Outcome dispatch(BL_HOOK_PARAMS, int slot) {
    Outcome result;
    if (slot < 0 || slot >= kMaxHooks) return result;
    HookCtx* c = &g_hooks[slot];

    const intptr_t rawA[kIntSlots] = {a0, a1, a2, a3, a4, a5, a6, a7, s0, s1, s2, s3, s4, s5, s6, s7};
    const double rawF[8] = {f0, f1, f2, f3, f4, f5, f6, f7};
    uint64_t rawD[8];
    std::memcpy(rawD, rawF, sizeof(rawD));

    // O filtro vem antes de tudo: quem nao passa nao paga trava nem JS.
    // g_depth do hook de fora > 0 = esta thread esta dentro do callback dele.
    if (c->gateSlot >= 0 && g_depth[c->gateSlot] == 0) return callOriginal(c, rawA, rawD);
    if (c->tileReg >= 0 || c->tileAtIReg >= 0) {
        const int t = c->tileReg >= 0
            ? runtime::tileTypeAtOffset(static_cast<int32_t>(rawA[c->tileReg]))
            : runtime::tileTypeAt(static_cast<int32_t>(rawA[c->tileAtIReg]),
                                  static_cast<int32_t>(rawA[c->tileAtJReg]));
        if (t < c->filterMin) return callOriginal(c, rawA, rawD);
    }
    if (c->filterReg >= 0) {
        auto* o = reinterpret_cast<const uint8_t*>(rawA[c->filterReg]);
        int32_t v = 0;
        if (o) std::memcpy(&v, o + c->filterOffset, sizeof(v));
        if (!o || v < c->filterMin) return callOriginal(c, rawA, rawD);
    }

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
    const int firstParam = argc;
    for (const ParamPlan& p : c->abi.params) {
        if (argc >= static_cast<int>(sizeof(argv) / sizeof(argv[0]))) break;
        argv[argc++] = paramToJs(ctx, f.a, f.d, p);
    }

    // O limite de pilha do QuickJS ja foi realinhado com esta thread pelo
    // JsLock (ver ScriptEngine.h). Sem isso os mods, carregados na thread da
    // sonda, recusavam o callback na thread do jogo com "Maximum call stack
    // size exceeded".
    JSValue ret = JS_Call(ctx, c->callback, JS_UNDEFINED, argc, argv);
    if (JS_IsException(ret)) {
        JSValue e = JS_GetException(ctx);
        const char* t = JS_ToCString(ctx, e);
        // O nome do metodo e a pilha JS: sem eles, "nao aceita NaN" nao diz
        // de qual mod nem de qual linha veio.
        JSValue st = JS_IsError(e) ? JS_GetPropertyStr(ctx, e, "stack") : JS_UNDEFINED;
        const char* s = JS_IsString(st) ? JS_ToCString(ctx, st) : nullptr;
        BL_ERROR("hook %s.%s: excecao no callback: %s%s%s",
                 il2cpp::api().class_get_name(il2cpp::api().method_get_class(c->method)),
                 il2cpp::api().method_get_name(c->method), t ? t : "?", s ? "\n" : "", s ? s : "");
        if (s) JS_FreeCString(ctx, s);
        JS_FreeValue(ctx, st);
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
            const Outcome o = callOriginal(c, rawA, rawD);
            g_frames.back().originalResult = o;
        }
    }
    // O que o metodo devolve: o valor do callback tem prioridade; senao o do
    // original, se ele foi chamado.
    result = jsToOutcome(ctx, c->abi, ret, g_frames.back().originalResult);

    // Os ref/out apontam para a pilha de quem chamou: o Ref que o mod guardou
    // fica com o ultimo valor, sem o endereco.
    for (int i = firstParam; i < argc; ++i) {
        if (c->abi.params[static_cast<size_t>(i - firstParam)].opaque) unbindRef(ctx, argv[i]);
    }
    for (int i = 0; i < argc; ++i) JS_FreeValue(ctx, argv[i]);
    JS_FreeValue(ctx, ret);

    g_frames.pop_back();
    --g_depth[slot];
    return result;
}

// --- funcoes de substituicao geradas em tempo de compilacao ---
// Uma por slot, instanciadas por template: stubInt<37> e a funcao do slot 37.
// O tipo de RETORNO faz parte da ABI (x0, s0, d0, ou os registradores do
// struct), entao ha uma familia por forma de retorno.

template <int Slot>
static intptr_t stubInt(BL_HOOK_PARAMS) { return dispatch(BL_HOOK_ARGS, Slot).i; }

template <int Slot>
static float stubFlt(BL_HOOK_PARAMS) { return static_cast<float>(dispatch(BL_HOOK_ARGS, Slot).f); }

template <int Slot>
static double stubDbl(BL_HOOK_PARAMS) { return dispatch(BL_HOOK_ARGS, Slot).f; }

/** Os bytes do Outcome no formato do carregador. */
template <class T>
static inline T carry(const Outcome& o) {
    T r;
    std::memcpy(&r, o.s, sizeof(r));
    return r;
}

template <class T, int Slot>
static T stubStruct(BL_HOOK_PARAMS) { return carry<T>(dispatch(BL_HOOK_ARGS, Slot)); }

// A tabela slot -> funcao, montada uma vez. A ordem das formas de struct tem
// de bater com a do enum Ret (S8, S16, H1F..H4F, H1D..H4D), que e a que o
// slotRange usa para achar o intervalo.
static void* g_stubs[kMaxHooks];

template <int From, size_t... I>
static void putInt(std::index_sequence<I...>) {
    ((g_stubs[From + I] = reinterpret_cast<void*>(&stubInt<From + static_cast<int>(I)>)), ...);
}
template <int From, size_t... I>
static void putFlt(std::index_sequence<I...>) {
    ((g_stubs[From + I] = reinterpret_cast<void*>(&stubFlt<From + static_cast<int>(I)>)), ...);
}
template <int From, size_t... I>
static void putDbl(std::index_sequence<I...>) {
    ((g_stubs[From + I] = reinterpret_cast<void*>(&stubDbl<From + static_cast<int>(I)>)), ...);
}
template <class T, int Forma, size_t... I>
static void putStruct(std::index_sequence<I...>) {
    constexpr int from = kBaseStruct + Forma * kStructSlots;
    ((g_stubs[from + I] = reinterpret_cast<void*>(&stubStruct<T, from + static_cast<int>(I)>)), ...);
}

static bool fillStubs() {
    putInt<0>(std::make_index_sequence<kIntHooks>{});
    putFlt<kIntHooks>(std::make_index_sequence<kFltHooks>{});
    putDbl<kIntHooks + kFltHooks>(std::make_index_sequence<kDblHooks>{});
    using Seq = std::make_index_sequence<kStructSlots>;
    putStruct<S8, 0>(Seq{});
    putStruct<S16, 1>(Seq{});
    putStruct<H1F, 2>(Seq{});
    putStruct<H2F, 3>(Seq{});
    putStruct<H3F, 4>(Seq{});
    putStruct<H4F, 5>(Seq{});
    putStruct<H1D, 6>(Seq{});
    putStruct<H2D, 7>(Seq{});
    putStruct<H3D, 8>(Seq{});
    putStruct<H4D, 9>(Seq{});
    for (void* p : g_stubs) {
        if (!p) return false;
    }
    return true;
}
static_assert(static_cast<int>(Ret::H4D) - static_cast<int>(Ret::S8) + 1 == kStructForms,
              "uma forma de struct por Ret de struct");

// ---------------------------- instalacao ----------------------------

/**
 * O plano da ABI, mais o que so o hook precisa saber.
 * @return mensagem de erro, ou vazio se der para reproduzir a chamada.
 */
static std::string buildPlan(HookCtx& c) {
    auto& api = il2cpp::api();
    // Metodo de instancia de um STRUCT: o x0 aponta para os DADOS, nao para um
    // objeto. Embrulhar como GameObject leria o comeco dos dados como classe.
    if (c.isInstance && api.method_get_class && api.class_is_valuetype) {
        Il2CppClass* owner = api.method_get_class(c.method);
        if (owner && api.class_is_valuetype(owner)) c.selfStruct = owner;
    }
    return planAbi(c.method, c.isInstance, &c.abi);
}

/** O x e o offset do filtro. Mensagem de erro, ou vazio. */
static std::string resolveFilter(HookCtx& c, const HookFilter& f) {
    auto& api = il2cpp::api();
    Il2CppClass* cls = nullptr;
    if (f.on == -1) {
        if (!c.isInstance || c.selfStruct) return "filtro 'self' so vale em metodo de instancia de classe";
        c.filterReg = 0;
        cls = api.method_get_class(c.method);
    } else {
        if (f.on < 0 || static_cast<size_t>(f.on) >= c.abi.params.size()) return "filtro: parametro fora da faixa";
        const ParamPlan& p = c.abi.params[static_cast<size_t>(f.on)];
        if (p.floatQueue || p.structByRef || p.opaque || p.d.byValue || p.reg >= 8) {
            return "filtro: o parametro tem de ser um objeto";
        }
        c.filterReg = p.reg;
        cls = p.d.cls;
    }
    c.filterOffset = cls ? il2cpp::fieldOffset(cls, f.field) : -1;
    if (c.filterOffset < 0) return "filtro: campo '" + f.field + "' nao existe";
    c.filterMin = f.minType;
    return {};
}

bool installJsHook(JSContext* ctx, const MethodInfo* method, int paramCount,
                   bool isInstance, JSValueConst callback, const HookFilter* filter) {
    if (!method) return false;

    HookCtx probe;
    probe.method = method;
    probe.isInstance = isInstance;
    std::string err = buildPlan(probe);
    if (err.empty() && filter && filter->on != -2) err = resolveFilter(probe, *filter);
    if (err.empty() && filter && (filter->tileParam >= 0 || filter->tileAtI >= 0)) {
        auto intReg = [&](int index, int* reg) -> bool {
            if (index < 0 || static_cast<size_t>(index) >= probe.abi.params.size()) return false;
            const ParamPlan& p = probe.abi.params[static_cast<size_t>(index)];
            if (p.floatQueue || p.structByRef || p.opaque || p.d.size > 8) return false;
            *reg = p.reg;
            return true;
        };
        const bool ok = filter->tileParam >= 0
            ? intReg(filter->tileParam, &probe.tileReg)
            : intReg(filter->tileAtI, &probe.tileAtIReg) && intReg(filter->tileAtJ, &probe.tileAtJReg);
        if (!ok) err = "filtro de tile: parametro fora da faixa ou que nao e Tile/int";
        probe.filterMin = filter->minType;
    }
    if (err.empty() && filter && filter->whileIn) {
        for (int i = 0; i < kMaxHooks; ++i) {
            if (g_hooks[i].used && g_hooks[i].method == filter->whileIn) { probe.gateSlot = i; break; }
        }
        if (probe.gateSlot < 0) {
            err = std::string("whileIn: '") + il2cpp::api().method_get_name(filter->whileIn) +
                  "' precisa ter um hook JS antes";
        }
    }
    if (!err.empty()) {
        JS_ThrowTypeError(ctx, "hook em '%s': %s",
                          il2cpp::api().method_get_name(method), err.c_str());
        return false;
    }

    static const bool stubsReady = fillStubs();
    if (!stubsReady) {
        JS_ThrowInternalError(ctx, "hook: tabela de funcoes de slot incompleta");
        return false;
    }
    // A familia depende do tipo de retorno: cada uma tem seu proprio pool,
    // porque o tipo de retorno da funcao de substituicao faz parte da ABI.
    int from = 0, count = 0;
    slotRange(probe.abi.ret, &from, &count);

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
        BL_DEBUG("hook JS no slot %d: %s", i, il2cpp::describeMethod(method).c_str());
        return true;
    }
    JS_ThrowInternalError(ctx, "hook: sem slots livres para retorno '%s' "
                               "(%d nessa forma, todos em uso)",
                          probe.abi.retDesc.name.c_str(), count);
    return false;
}

#else // arquitetura != arm64: hook JS indisponivel (stub)

bool installJsHook(JSContext* ctx, const MethodInfo*, int, bool, JSValueConst, const HookFilter*) {
    JS_ThrowInternalError(ctx, "hook JS: so implementado em arm64");
    return false;
}

#endif // __aarch64__

} // namespace bl::script

#endif // BL_HAVE_QUICKJS
