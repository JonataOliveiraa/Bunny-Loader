#include "script/bridge/Abi.h"
#include "script/bridge/Bridge.h"
#include "script/bridge/Marshal.h"
#include "script/bridge/Ref.h"
#include "script/bridge/ScriptEngine.h"
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
#include <cstdio>
#include <cstring>
#include <map>
#include <memory>
#include <mutex>
#include <string>
#include <time.h>
#include <utility>
#include <vector>

namespace bl::script {

// As tabelas de marcas (HookFilter::marks). O despachante le sem trava: a
// tabela nunca muda de lugar nem some, e cada entrada e atomica.
std::atomic<uint8_t>* hookMarks(const std::string& name) {
    static std::mutex lock;
    static std::map<std::string, std::unique_ptr<std::atomic<uint8_t>[]>> tables;
    std::lock_guard<std::mutex> guard(lock);
    auto& t = tables[name];
    if (!t) {
        t.reset(new std::atomic<uint8_t>[kMarkTypes]);
        for (int i = 0; i < kMarkTypes; ++i) t[i].store(0, std::memory_order_relaxed);
    }
    return t.get();
}

std::atomic<uint8_t>* hookFlag(const std::string& name) {
    static std::mutex lock;
    static std::map<std::string, std::unique_ptr<std::atomic<uint8_t>>> flags;
    std::lock_guard<std::mutex> guard(lock);
    auto& f = flags[name];
    if (!f) f.reset(new std::atomic<uint8_t>(0));
    return f.get();
}

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
    // `field: 'inner.type'`: o offset da referencia a seguir antes (-1 = direto).
    int32_t filterDeref = -1;
    int32_t filterIndexOffset = -1;
    int32_t filterMin = 0;
    Prim filterPrim = Prim::I32;   // o campo pode ser byte/short (Item.prefix)
    // HookFilter::whileIn: o slot do hook de fora; -1 = sem esse filtro.
    int gateSlot = -1;
    int scopeSlot = -1;
    // Filtro pelo tipo do tile: o x do `Tile` (offset), ou os x de i e j.
    int tileReg = -1, tileAtIReg = -1, tileAtJReg = -1;
    bool wallMode = false;
    // HookFilter::argParam: o x do parametro que ja e o tipo.
    int argReg = -1, argBytes = 4;
    // HookFilter::marks: o tipo lido tambem tem de estar marcado aqui.
    const std::atomic<uint8_t>* marks = nullptr;
    // HookFilter::flag: desligada, o metodo roda sem o JS.
    const std::atomic<uint8_t>* flag = nullptr;
    // HookFilter::ifBusy: com o motor JS noutra thread, nao espera.
    IfBusy ifBusy = IfBusy::Wait;
};

/** O campo do filtro na largura dele: Item.prefix e byte, e os 3 seguintes sao outros campos. */
static int32_t readFilterField(const uint8_t* p, Prim prim) {
    switch (prim) {
    case Prim::Bool: case Prim::U8: return *p;
    case Prim::I8: return static_cast<int8_t>(*p);
    case Prim::I16: { int16_t v; std::memcpy(&v, p, sizeof(v)); return v; }
    case Prim::U16: case Prim::Char: { uint16_t v; std::memcpy(&v, p, sizeof(v)); return v; }
    default: { int32_t v; std::memcpy(&v, p, sizeof(v)); return v; }
    }
}

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
    // Quanto do callback foi o metodo do jogo (original()): fora do tempo do JS.
    int64_t originalNs = 0;
};
// NUNCA segure `Frame&` atravessando uma chamada ao jogo: o original pode
// disparar outro hook nesta thread, o push_back realoca o vector e a
// referencia passa a apontar para memoria liberada. Use o INDICE.
//
// Foi o crash ao criar e ao carregar mundo: dois hooks JS encadeados no
// Item.SetDefaults (HelloMod + itens de mod) aninham na primeira chamada de
// cada thread nova, e a gravacao do resultado caia em memoria ja devolvida —
// que o IL2CPP reusava nas tabelas de genericos (docs/historico/PONTE-OTIMIZACAO.md).
static thread_local std::vector<Frame> g_frames;

// Profundidade de reentrancia por slot, por thread. Evita que um metodo cujo
// corpo real (rodado via original()) rechama a si mesmo pela entrada patcheada
// dispare o callback JS repetidamente ate estourar a pilha do QuickJS.
static thread_local int g_depth[kMaxHooks];
static thread_local int g_scopeDepth[kMaxHooks];

// Quanto um hook espera pelo motor JS antes de desistir do mod naquela
// chamada. Ver JsLock: esperar para sempre transformaria um impasse entre
// threads num jogo congelado.
constexpr int kLockTimeoutMs = 3000;
static std::atomic<bool> g_lockWarned[kMaxHooks];

// Contagem por hook, para o bl.hookStats (medir o que cada hook custa): as
// chamadas que chegam ao despacho e as que chegam ao JS. Soma sem troca
// atomica (ler e gravar relaxado): o numero e estatistica, e um incremento
// perdido entre threads nao importa; uma RMW em todo SpriteBatch.Draw, sim.
static std::atomic<uint64_t> g_calls[kMaxHooks];
static std::atomic<uint64_t> g_jsCalls[kMaxHooks];
// O tempo do callback sem o do original(): o que o mod custa, em ns.
static std::atomic<uint64_t> g_jsNs[kMaxHooks];

static inline int64_t nowNs() {
    timespec t;
    clock_gettime(CLOCK_MONOTONIC, &t);
    return static_cast<int64_t>(t.tv_sec) * 1000000000 + t.tv_nsec;
}

static inline void bump(std::atomic<uint64_t>& n) {
    n.store(n.load(std::memory_order_relaxed) + 1, std::memory_order_relaxed);
}

/** `Classe.Metodo`, para o log. */
static std::string hookName(const MethodInfo* m) {
    auto& a = il2cpp::api();
    if (!m) return "?";
    Il2CppClass* cls = a.method_get_class ? a.method_get_class(m) : nullptr;
    const char* c = cls ? a.class_get_name(cls) : nullptr;
    return std::string(c ? c : "?") + "." + a.method_get_name(m);
}

/** O nome da thread `tid` (o comm do Linux), ou "?". */
static std::string threadName(int tid) {
    char path[64], name[32] = "?";
    std::snprintf(path, sizeof(path), "/proc/self/task/%d/comm", tid);
    if (FILE* f = std::fopen(path, "r")) {
        if (std::fgets(name, sizeof(name), f)) name[std::strcspn(name, "\n")] = '\0';
        std::fclose(f);
    }
    return name;
}

/** Quem esta com o motor: "thread 15840 (Thread-7), no hook Player.InternalSavePlayerFile". */
static std::string describeJsOwner() {
    const int tid = jsOwnerThread();
    if (!tid) return "ninguem agora";
    const auto* m = static_cast<const MethodInfo*>(jsOwnerHook());
    return "thread " + std::to_string(tid) + " (" + threadName(tid) + "), " +
           (m ? "no hook " + hookName(m) : std::string("fora de hook (carga de mod ou de conteudo)"));
}

std::string describeJsOwnerForLog() { return describeJsOwner(); }
std::string describeHookForLog(const void* method) {
    return method ? hookName(static_cast<const MethodInfo*>(method)) : std::string("(fora de hook)");
}

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
        // O proprio `self` que o jogo passou volta como veio, sem ler nada
        // dele: um metodo que nao usa o `this` pode ser chamado com lixo no
        // registrador (o GUINPCDialogue.Draw chama o Option1Clicked com 0x1),
        // e perguntar a classe desse "objeto" derrubava o jogo.
        Il2CppObject* same = objectFromJS(argv[at]);
        if (same && reinterpret_cast<intptr_t>(same) == a[0]) {
            // a[0] ja e ele
        } else if (void* s = structDataOf(argv[at], nullptr, nullptr)) {
            a[0] = reinterpret_cast<intptr_t>(s);
        } else if (Il2CppObject* o = objectFromJS(argv[at])) {
            a[0] = reinterpret_cast<intptr_t>(o);
        }
        ++at;
    }
    ArgScratch scratch;   // vive ate o original voltar
    for (size_t i = 0; i < c->abi.params.size() && at < argc; ++i, ++at) {
        const ParamPlan& param = c->abi.params[i];
        // As with self above, IL2CPP may leave an unused reference argument
        // uninitialized (e.g. IEntitySource in Item.NewItem). Passing the exact
        // incoming pointer back must not dereference it for assignability.
        // A replacement object still goes through the normal type checks.
        if (!param.opaque && !param.floatQueue && param.d.prim == Prim::Object) {
            Il2CppObject* incoming = objectFromJS(argv[at]);
            if (incoming && reinterpret_cast<intptr_t>(incoming) == a[param.reg]) continue;
        }
        if (jsToParam(ctx, argv[at], param, a, d, &scratch) < 0) return JS_EXCEPTION;
    }

    g_frames[frame].ranOriginal = true;
    const int64_t t0 = nowNs();
    const Outcome result = callOriginal(c, a, d);   // pode empilhar outros frames
    g_frames[frame].originalNs += nowNs() - t0;
    g_frames[frame].originalResult = result;
    return outcomeToJs(c->ctx, c->abi, result);
}

// ---------------------------- dispatcher ----------------------------

static Outcome dispatch(BL_HOOK_PARAMS, int slot) {
    Outcome result;
    if (slot < 0 || slot >= kMaxHooks) return result;
    HookCtx* c = &g_hooks[slot];
    bump(g_calls[slot]);

    const intptr_t rawA[kIntSlots] = {a0, a1, a2, a3, a4, a5, a6, a7, s0, s1, s2, s3, s4, s5, s6, s7};
    const double rawF[8] = {f0, f1, f2, f3, f4, f5, f6, f7};
    uint64_t rawD[8];
    std::memcpy(rawD, rawF, sizeof(rawD));

    // O filtro vem antes de tudo: quem nao passa nao paga trava nem JS.
    if (c->flag && !c->flag->load(std::memory_order_relaxed)) return callOriginal(c, rawA, rawD);
    if (c->gateSlot >= 0 && g_scopeDepth[c->gateSlot] == 0) return callOriginal(c, rawA, rawD);
    int seen = -1;   // o tipo que um dos filtros leu (para as marcas)
    if (c->tileReg >= 0 || c->tileAtIReg >= 0) {
        const int t = c->wallMode
            ? (c->tileReg >= 0 ? runtime::wallTypeAtOffset(static_cast<int32_t>(rawA[c->tileReg]))
                               : runtime::wallTypeAt(static_cast<int32_t>(rawA[c->tileAtIReg]),
                                                     static_cast<int32_t>(rawA[c->tileAtJReg])))
            : c->tileReg >= 0
            ? runtime::tileTypeAtOffset(static_cast<int32_t>(rawA[c->tileReg]))
            : runtime::tileTypeAt(static_cast<int32_t>(rawA[c->tileAtIReg]),
                                  static_cast<int32_t>(rawA[c->tileAtJReg]));
        if (t < c->filterMin) return callOriginal(c, rawA, rawD);
        seen = t;
    }
    if (c->argReg >= 0) {
        const intptr_t raw = rawA[c->argReg];
        const int32_t t = c->argBytes == 2 ? static_cast<int32_t>(static_cast<uint16_t>(raw))
                        : c->argBytes == 1 ? static_cast<int32_t>(static_cast<uint8_t>(raw))
                                           : static_cast<int32_t>(raw);
        if (t < c->filterMin) return callOriginal(c, rawA, rawD);
        seen = t;
    }
    if (c->filterReg >= 0) {
        auto* o = reinterpret_cast<const uint8_t*>(rawA[c->filterReg]);
        int32_t index = -1;
        if (o && c->filterIndexOffset >= 0) std::memcpy(&index, o + c->filterIndexOffset, sizeof(index));
        if (o && c->filterDeref >= 0) {
            const uint8_t* inner = nullptr;
            std::memcpy(&inner, o + c->filterDeref, sizeof(inner));
            o = inner;
        }
        if (o && c->filterIndexOffset >= 0) {
            const auto* array = reinterpret_cast<const Il2CppArray*>(o);
            const uint8_t* item = nullptr;
            if (index >= 0 && static_cast<uintptr_t>(index) < array->length) {
                std::memcpy(&item, o + sizeof(Il2CppArray) + static_cast<size_t>(index) * sizeof(void*), sizeof(item));
            }
            o = item;
        }
        int32_t v = 0;
        if (o) v = readFilterField(o + c->filterOffset, c->filterPrim);
        if (!o || v < c->filterMin) return callOriginal(c, rawA, rawD);
        seen = v;
    }
    if (c->marks && (seen < 0 || seen >= kMarkTypes || !c->marks[seen].load(std::memory_order_relaxed))) {
        return callOriginal(c, rawA, rawD);
    }

    // Reentrancia: ja estamos dentro deste hook nesta thread (o corpo real,
    // rodando via original(), rechamou o metodo pela entrada patcheada). Nao
    // dispara o callback de novo — executa direto o original e volta.
    if (g_depth[slot] > 0) return callOriginal(c, rawA, rawD);

    // ifBusy: o hook de todo quadro que pode ficar um quadro sem o mod nao
    // espera nada (a musica: esperar 3 s congelava a tela de carregamento).
    JsLock lock(c->ifBusy == IfBusy::Wait ? kLockTimeoutMs : 0);
    if (!lock.held()) {
        if (c->ifBusy == IfBusy::Skip) return result;
        if (c->ifBusy == IfBusy::Wait && !g_lockWarned[slot].exchange(true)) {
            BL_ERROR("hook %s: motor JS ocupado ha %d ms por %s; rodando o metodo sem o mod "
                     "(aviso unico por hook)", hookName(c->method).c_str(), kLockTimeoutMs,
                     describeJsOwner().c_str());
        }
        return callOriginal(c, rawA, rawD);
    }
    const void* outerHook = setJsOwnerHook(c->method);
    ++g_depth[slot];
    ++g_scopeDepth[c->scopeSlot];
    bump(g_jsCalls[slot]);
    const int64_t jsStart = nowNs();

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
        // Metodo que nao usa o `this` pode chegar com lixo no lugar dele (ver
        // js_original). Um endereco baixo demais para ser objeto vira null.
        argv[argc++] = rawA[0] >= 0x10000
            ? makeNativeObject(ctx, reinterpret_cast<Il2CppObject*>(rawA[0])) : JS_NULL;
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
        const std::string detail = withCodeFrame(std::string(t ? t : "?") + (s ? "\n" : "") + (s ? s : ""));
        BL_ERROR("hook %s.%s: exception in callback: %s",
                 il2cpp::api().class_get_name(il2cpp::api().method_get_class(c->method)),
                 il2cpp::api().method_get_name(c->method), detail.c_str());
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

    const int64_t spent = nowNs() - jsStart - g_frames.back().originalNs;
    if (spent > 0) {
        g_jsNs[slot].store(g_jsNs[slot].load(std::memory_order_relaxed) + static_cast<uint64_t>(spent),
                           std::memory_order_relaxed);
    }
    g_frames.pop_back();
    --g_depth[slot];
    --g_scopeDepth[c->scopeSlot];
    setJsOwnerHook(outerHook);
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
    // `ref.campo`: um nivel de referencia (a WorldItem guarda o Item em `inner`).
    std::string name = f.field;
    const size_t closeBracket = name.find(']');
    const size_t dot = name.find('.', closeBracket == std::string::npos ? 0 : closeBracket + 1);
    if (dot != std::string::npos) {
        std::string ref = name.substr(0, dot);
        const size_t bracket = ref.find('[');
        if (bracket != std::string::npos) {
            if (ref.back() != ']' || bracket == 0) return "filtro: indice de array invalido";
            std::string index = ref.substr(bracket + 1, ref.size() - bracket - 2);
            Il2CppClass* indexClass = cls;
            int32_t indexBase = 0;
            const size_t indexDot = index.find('.');
            if (indexDot != std::string::npos) {
                FieldInfo* state = cls ? il2cpp::findField(cls, index.substr(0, indexDot)) : nullptr;
                const Il2CppType* stateType = state ? api.field_get_type(state) : nullptr;
                if (!stateType || !describe(stateType).byValue) return "filtro: estado do indice deve ser struct";
                indexClass = api.class_from_il2cpp_type(stateType);
                indexBase = static_cast<int32_t>(api.field_get_offset(state));
                index = index.substr(indexDot + 1);
            }
            FieldInfo* indexField = indexClass ? il2cpp::findField(indexClass, index) : nullptr;
            if (!indexField || describe(api.field_get_type(indexField)).prim != Prim::I32) return "filtro: indice de array deve ser um campo int";
            const size_t indexOffset = api.field_get_offset(indexField);
            c.filterIndexOffset = indexBase + static_cast<int32_t>(indexDot == std::string::npos ? indexOffset : structFieldOffset(indexClass, indexOffset));
            ref.resize(bracket);
        }
        FieldInfo* rf = cls ? il2cpp::findField(cls, ref) : nullptr;
        if (!rf) return "filtro: campo '" + ref + "' nao existe";
        const Il2CppType* rt = api.field_get_type(rf);
        if (describe(rt).byValue) return "filtro: o campo '" + ref + "' tem de ser uma referencia";
        c.filterDeref = static_cast<int32_t>(api.field_get_offset(rf));
        cls = api.class_from_il2cpp_type(rt);
        if (c.filterIndexOffset >= 0) {
            cls = cls && api.class_get_element_class ? api.class_get_element_class(cls) : nullptr;
            if (!cls || (api.class_is_valuetype && api.class_is_valuetype(cls))) return "filtro: array deve conter referencias";
        }
        name = name.substr(dot + 1);
    }
    FieldInfo* field = cls ? il2cpp::findField(cls, name) : nullptr;
    if (!field) return "filtro: campo '" + f.field + "' nao existe";
    c.filterOffset = static_cast<int32_t>(api.field_get_offset(field));
    c.filterMin = f.minType;
    switch (const Prim prim = describe(api.field_get_type(field)).prim) {
    case Prim::Bool: case Prim::I8: case Prim::U8: case Prim::I16: case Prim::U16: case Prim::Char:
    case Prim::I32: case Prim::U32:
        c.filterPrim = prim;
        break;
    default:
        return "filtro: o campo '" + f.field + "' tem de ser inteiro";
    }
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
        probe.wallMode = filter->wallMode;
    }
    if (err.empty() && filter && filter->argParam >= 0) {
        const size_t i = static_cast<size_t>(filter->argParam);
        const ParamPlan* p = i < probe.abi.params.size() ? &probe.abi.params[i] : nullptr;
        const bool integer = p && (p->d.prim == Prim::I8 || p->d.prim == Prim::U8 || p->d.prim == Prim::I16 ||
                                    p->d.prim == Prim::U16 || p->d.prim == Prim::I32 || p->d.prim == Prim::U32);
        if (!integer || p->floatQueue || p->opaque) {
            err = "filtro 'arg': o parametro tem de ser um inteiro (int, ushort...)";
        } else {
            probe.argReg = p->reg;
            probe.argBytes = static_cast<int>(p->d.size);
            probe.filterMin = filter->minType;
        }
    }
    if (err.empty() && filter && !filter->marks.empty()) {
        if (probe.tileReg < 0 && probe.tileAtIReg < 0 && probe.argReg < 0 && probe.filterReg < 0) {
            err = "filtro 'marks' precisa de outro que leia o tipo (tile, tileAt, arg ou minType)";
        } else {
            probe.marks = hookMarks(filter->marks);
        }
    }
    if (filter && !filter->flag.empty()) probe.flag = hookFlag(filter->flag);
    if (filter) probe.ifBusy = filter->ifBusy;
    if (err.empty() && probe.ifBusy == IfBusy::Skip && probe.abi.retDesc.prim != Prim::Void) {
        err = "ifBusy 'skip' so vale em metodo void (sem o JS, nao ha o que devolver)";
    }
    if (err.empty() && filter && filter->whileIn) {
        for (int i = 0; i < kMaxHooks; ++i) {
            if (g_hooks[i].used && g_hooks[i].method == filter->whileIn) { probe.gateSlot = g_hooks[i].scopeSlot; break; }
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
        c.scopeSlot = i;
        for (int j = 0; j < kMaxHooks; ++j) {
            if (g_hooks[j].used && g_hooks[j].method == method) { c.scopeSlot = g_hooks[j].scopeSlot; break; }
        }
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

std::vector<HookStat> hookStats() {
    std::vector<HookStat> out;
    for (int i = 0; i < kMaxHooks; ++i) {
        if (!g_hooks[i].used) continue;
        HookStat s;
        // Com os tipos: as sobrecargas do SpriteBatch.Draw tem o mesmo nome.
        s.name = hookName(g_hooks[i].method) + "(";
        for (size_t k = 0; k < g_hooks[i].abi.params.size(); ++k) {
            if (k) s.name += ", ";
            s.name += g_hooks[i].abi.params[k].d.name;
        }
        s.name += ")";
        s.calls = g_calls[i].load(std::memory_order_relaxed);
        s.js = g_jsCalls[i].load(std::memory_order_relaxed);
        s.jsNs = g_jsNs[i].load(std::memory_order_relaxed);
        out.push_back(std::move(s));
    }
    return out;
}

#else // arquitetura != arm64: hook JS indisponivel (stub)

std::vector<HookStat> hookStats() { return {}; }

bool installJsHook(JSContext* ctx, const MethodInfo*, int, bool, JSValueConst, const HookFilter*) {
    JS_ThrowInternalError(ctx, "hook JS: so implementado em arm64");
    return false;
}

#endif // __aarch64__

} // namespace bl::script

#endif // BL_HAVE_QUICKJS
