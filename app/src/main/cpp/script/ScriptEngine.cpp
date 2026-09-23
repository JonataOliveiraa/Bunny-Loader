#include "script/ScriptEngine.h"
#include "core/Log.h"

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

bool ScriptEngine::init() {
    if (ready_) return true;
    JsLock lock;

    auto* rt = JS_NewRuntime();
    if (!rt) { BL_ERROR("JS_NewRuntime falhou"); return false; }
    auto* ctx = JS_NewContext(rt);
    if (!ctx) { BL_ERROR("JS_NewContext falhou"); JS_FreeRuntime(rt); return false; }

    runtime_ = rt;
    context_ = ctx;
    installBindings(ctx);

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
