#include "script/ScriptEngine.h"
#include "core/Log.h"

#if BL_HAVE_QUICKJS
#include "quickjs.h"
#endif

#include <cstdio>
#include <string>
#include <vector>

namespace bl::script {

ScriptEngine& engine() {
    static ScriptEngine instance;
    return instance;
}

#if BL_HAVE_QUICKJS

bool ScriptEngine::init() {
    if (ready_) return true;

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
    JS_FreeContext(static_cast<JSContext*>(context_));
    JS_FreeRuntime(static_cast<JSRuntime*>(runtime_));
    context_ = nullptr;
    runtime_ = nullptr;
    ready_ = false;
}

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

    auto* ctx = static_cast<JSContext*>(context_);
    JSValue result = JS_Eval(ctx, buffer.data(), static_cast<size_t>(size),
                             moduleName.c_str(), JS_EVAL_TYPE_MODULE);
    bool ok = !JS_IsException(result);
    if (!ok) {
        JSValue err = JS_GetException(ctx);
        const char* text = JS_ToCString(ctx, err);
        BL_ERROR("erro em %s: %s", moduleName.c_str(), text ? text : "?");
        if (text) JS_FreeCString(ctx, text);
        JS_FreeValue(ctx, err);
    }
    JS_FreeValue(ctx, result);
    return ok;
}

#else // sem QuickJS: stub para o projeto compilar antes do vendoring

bool ScriptEngine::init() {
    BL_WARN("ScriptEngine em modo STUB (QuickJS ausente). Ver third_party/README.md");
    ready_ = false;
    return true; // não bloqueia o boot: o jogo sobe sem mods
}
void ScriptEngine::shutdown() {}
bool ScriptEngine::evalFile(const std::string&, const std::string&) { return false; }

#endif

} // namespace bl::script
