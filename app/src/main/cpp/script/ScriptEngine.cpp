#include "script/ScriptEngine.h"
#include "core/Log.h"
#include "mods/ModLoader.h"

#if BL_HAVE_QUICKJS
#include "quickjs.h"
#endif

#include <chrono>
#include <cstdio>
#include <mutex>
#include <string>
#include <vector>

namespace bl::script {

ScriptEngine& engine() {
    static ScriptEngine instance;
    return instance;
}

// ------------------------------- JsLock -------------------------------

namespace {
std::timed_mutex g_jsMutex;
// Quem segura este pode deixar frames do QuickJS estacionados enquanto solta
// o motor. So um por vez — ver JsSuspend.
std::mutex g_parkMutex;
thread_local int t_jsDepth = 0;

void alignStackTop() {
#if BL_HAVE_QUICKJS
    if (void* rt = engine().runtime()) JS_UpdateStackTop(static_cast<JSRuntime*>(rt));
#endif
}
} // namespace

JsLock::JsLock() {
    if (t_jsDepth == 0) {
        g_jsMutex.lock();
        alignStackTop();
    }
    ++t_jsDepth;
    held_ = true;
}

JsLock::JsLock(int timeoutMs) {
    if (t_jsDepth == 0) {
        if (!g_jsMutex.try_lock_for(std::chrono::milliseconds(timeoutMs))) return;
        alignStackTop();
    }
    ++t_jsDepth;
    held_ = true;
}

JsLock::~JsLock() {
    if (!held_) return;
    if (--t_jsDepth == 0) g_jsMutex.unlock();
}

JsSuspend::JsSuspend() {
    if (t_jsDepth == 0) return;            // nao seguramos o motor: nada a soltar
    if (!g_parkMutex.try_lock()) return;   // outra thread ja esta estacionada
    depth_ = t_jsDepth;
    t_jsDepth = 0;
    released_ = true;
    g_jsMutex.unlock();
}

JsSuspend::~JsSuspend() {
    if (!released_) return;
    // Readquire ANTES de largar o estacionamento: soltar na ordem inversa
    // deixaria outra thread estacionar enquanto ainda esperamos, e voltariamos
    // com os frames dela por cima dos nossos.
    //
    // Espera sem prazo, de proposito: nao ha o que fazer com um "desistir" aqui
    // — a nossa chamada JS esta no meio e precisa terminar de desempilhar.
    g_jsMutex.lock();
    t_jsDepth = depth_;
    alignStackTop();
    g_parkMutex.unlock();
}

#if BL_HAVE_QUICKJS

namespace {

// ---------------------------- import entre arquivos ----------------------------
//
// Um mod pode dividir o codigo em arquivos, como o ExMod (um item por arquivo
// em Content/Items/...): `import { X } from './Content/Items/X.js'`.
//
// O nome de cada modulo diz de qual mod ele e: o main.js se chama so "<uid>"
// (e o que o ModLoader passa), e um arquivo importado "<uid>/<caminho dentro
// da pasta do mod>". E por esse prefixo que callerModId e resolvePath
// (Texture.cpp) acham a pasta do mod de quem chamou.
//
// So caminho relativo, e so dentro da pasta do mod: um mod nao le arquivo de
// outro nem do aparelho por import.

/** "<uid>/<rel>" -> {uid, rel}; o main.js ("<uid>") tem rel vazio. */
void splitModuleName(const std::string& name, std::string* uid, std::string* rel) {
    const size_t slash = name.find('/');
    *uid = name.substr(0, slash);
    *rel = slash == std::string::npos ? std::string() : name.substr(slash + 1);
}

char* normalizeModule(JSContext* ctx, const char* baseName, const char* spec, void*) {
    const std::string s = spec ? spec : "";
    if (s.rfind("./", 0) != 0 && s.rfind("../", 0) != 0) {
        JS_ThrowReferenceError(ctx, "import '%s': so arquivo do proprio mod, com caminho "
                                    "relativo ('./pasta/arquivo.js')", s.c_str());
        return nullptr;
    }
    std::string uid, rel;
    splitModuleName(baseName ? baseName : "", &uid, &rel);

    // A pasta de quem importa: a do arquivo dele, ou a raiz para o main.js.
    std::vector<std::string> parts;
    auto push = [&](const std::string& path, bool dropLast) {
        size_t start = 0;
        std::vector<std::string> segs;
        while (start <= path.size()) {
            const size_t end = path.find('/', start);
            segs.push_back(path.substr(start, end == std::string::npos ? std::string::npos : end - start));
            if (end == std::string::npos) break;
            start = end + 1;
        }
        if (dropLast && !segs.empty()) segs.pop_back();
        for (const std::string& seg : segs) {
            if (seg.empty() || seg == ".") continue;
            if (seg == "..") {
                if (parts.empty()) return false;   // subiu alem da pasta do mod
                parts.pop_back();
            } else {
                parts.push_back(seg);
            }
        }
        return true;
    };
    if (!push(rel, true) || !push(s, false) || parts.empty()) {
        JS_ThrowReferenceError(ctx, "import '%s': sai da pasta do mod", s.c_str());
        return nullptr;
    }
    std::string out = uid;
    for (const std::string& p : parts) out += "/" + p;
    return js_strdup(ctx, out.c_str());
}

JSModuleDef* loadModule(JSContext* ctx, const char* name, void*) {
    std::string uid, rel;
    splitModuleName(name, &uid, &rel);
    const std::string& dir = mods::dirOf(uid);
    const std::string path = dir + "/" + rel;
    FILE* f = dir.empty() || rel.empty() ? nullptr : std::fopen(path.c_str(), "rb");
    if (!f) {
        JS_ThrowReferenceError(ctx, "import: arquivo nao existe: %s", rel.c_str());
        return nullptr;
    }
    std::string code;
    char buf[4096];
    size_t n;
    while ((n = std::fread(buf, 1, sizeof(buf), f)) > 0) code.append(buf, n);
    std::fclose(f);

    JSValue fn = JS_Eval(ctx, code.c_str(), code.size(), name,
                         JS_EVAL_TYPE_MODULE | JS_EVAL_FLAG_COMPILE_ONLY);
    if (JS_IsException(fn)) return nullptr;
    auto* m = static_cast<JSModuleDef*>(JS_VALUE_GET_PTR(fn));
    JS_FreeValue(ctx, fn);
    return m;
}

} // namespace

bool ScriptEngine::init() {
    if (ready_) return true;
    JsLock lock;

    auto* rt = JS_NewRuntime();
    if (!rt) { BL_ERROR("JS_NewRuntime falhou"); return false; }
    auto* ctx = JS_NewContext(rt);
    if (!ctx) { BL_ERROR("JS_NewContext falhou"); JS_FreeRuntime(rt); return false; }

    // O QuickJS assume 1 MB de pilha (JS_DEFAULT_STACK_SIZE) e a thread do
    // Android tem exatamente isso, entao a guarda dele so disparava DEPOIS do
    // estouro de verdade: um mod com recursao infinita matava o processo do
    // jogo em silencio, sem excecao e sem tombstone — so os frames repetidos
    // da libbunny no logcat. Com folga, o mesmo mod leva um "Maximum call
    // stack size exceeded" e o jogo segue.
    //
    // Vale tambem para as threads do jogo, cuja pilha nao e nossa para medir.
    JS_SetMaxStackSize(rt, 256 * 1024);
    JS_SetModuleLoaderFunc(rt, normalizeModule, loadModule, nullptr);

    runtime_ = rt;
    context_ = ctx;
    installBindings(ctx);
    installModClasses(ctx);

    ready_ = true;
    BL_INFO("QuickJS iniciado");
    return true;
}

void ScriptEngine::shutdown() {
    if (!ready_) return;
    JsLock lock;
    JS_FreeContext(static_cast<JSContext*>(context_));
    JS_FreeRuntime(static_cast<JSRuntime*>(runtime_));
    context_ = nullptr;
    runtime_ = nullptr;
    ready_ = false;
}

namespace {

/** Loga a excecao pendente, com a pilha JS quando houver. */
void logException(JSContext* ctx, JSValueConst err, const std::string& name) {
    const char* text = JS_ToCString(ctx, err);
    std::string msg = text ? text : "?";
    if (text) JS_FreeCString(ctx, text);
    JSValue stack = JS_GetPropertyStr(ctx, err, "stack");
    if (JS_IsString(stack)) {
        const char* st = JS_ToCString(ctx, stack);
        if (st && *st) { msg += "\n"; msg += st; }
        if (st) JS_FreeCString(ctx, st);
    }
    JS_FreeValue(ctx, stack);
    BL_ERROR("erro em %s: %s", name.c_str(), msg.c_str());
}

/**
 * Avalia o codigo de um mod como MODULO ES.
 *
 * Antes era JS_EVAL_TYPE_GLOBAL, e todos os mods dividiam o mesmo escopo: o
 * Sem Queda declarava `const Update` e o Vida Cheia, carregado depois, morria
 * com "redeclaration of 'Update'". Dois mods quaisquer com um nome em comum no
 * topo do arquivo nao conviviam. Modulo tem escopo proprio; os globais da API
 * (Terraria, bl) continuam visiveis.
 *
 * Custo: modulo e strict mode. Atribuir a variavel nao declarada vira erro —
 * o que, num mod, quase sempre era um bug mesmo.
 *
 * O resultado de um modulo e uma PROMISE (top-level await existe). Um erro no
 * mod nao vem como excecao do JS_Eval, e sim como promise rejeitada — sem
 * olhar para ela, mod quebrado pareceria carregado.
 */
bool evalModule(JSContext* ctx, const char* code, size_t len, const std::string& name) {
    JSValue result = JS_Eval(ctx, code, len, name.c_str(), JS_EVAL_TYPE_MODULE);
    if (JS_IsException(result)) {
        JSValue err = JS_GetException(ctx);
        logException(ctx, err, name);
        JS_FreeValue(ctx, err);
        return false;
    }
    // Drena os jobs: e o que assenta a promise do modulo.
    JSContext* jobCtx = nullptr;
    while (JS_ExecutePendingJob(JS_GetRuntime(ctx), &jobCtx) > 0) {}

    bool ok = true;
    switch (JS_PromiseState(ctx, result)) {
        case JS_PROMISE_REJECTED: {
            JSValue err = JS_PromiseResult(ctx, result);
            logException(ctx, err, name);
            JS_FreeValue(ctx, err);
            ok = false;
            break;
        }
        case JS_PROMISE_PENDING:
            // Top-level await esperando algo que nunca vem: o mod ficaria
            // pela metade. Nao ha loop de eventos para completar isso.
            BL_ERROR("erro em %s: o modulo ficou pendente (await no topo?)", name.c_str());
            ok = false;
            break;
        default:
            break;
    }
    JS_FreeValue(ctx, result);
    return ok;
}

} // namespace

bool ScriptEngine::evalFile(const std::string& path, const std::string& moduleName) {
    if (!ready_) return false;

    FILE* f = fopen(path.c_str(), "rb");
    if (!f) { BL_ERROR("nao abriu script: %s", path.c_str()); return false; }
    fseek(f, 0, SEEK_END);
    long size = ftell(f);
    fseek(f, 0, SEEK_SET);
    std::vector<char> buffer(static_cast<size_t>(size) + 1, 0);
    fread(buffer.data(), 1, static_cast<size_t>(size), f);
    fclose(f);

    JsLock lock;
    return evalModule(static_cast<JSContext*>(context_), buffer.data(),
                      static_cast<size_t>(size), moduleName);
}

bool ScriptEngine::eval(const std::string& code, const std::string& name) {
    if (!ready_) return false;
    JsLock lock;
    return evalModule(static_cast<JSContext*>(context_), code.c_str(), code.size(), name);
}

#else // sem QuickJS: stub para o projeto compilar antes do vendoring

bool ScriptEngine::init() {
    BL_WARN("ScriptEngine em modo STUB (QuickJS ausente). Ver third_party/README.md");
    ready_ = false;
    return true; // não bloqueia o boot: o jogo sobe sem mods
}
void ScriptEngine::shutdown() {}
bool ScriptEngine::evalFile(const std::string&, const std::string&) { return false; }
bool ScriptEngine::eval(const std::string&, const std::string&) { return false; }

#endif

} // namespace bl::script
