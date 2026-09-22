#pragma once
#include <string>

namespace bl::script {

// Wrapper do QuickJS. Um JSRuntime + um JSContext, vivendo na thread principal
// da Unity (a mesma que chamou il2cpp_init).
class ScriptEngine {
public:
    bool init();
    void shutdown();

    // Avalia um arquivo .js como módulo. Retorna false e loga em caso de erro.
    bool evalFile(const std::string& path, const std::string& moduleName);

    // Avalia um trecho de código (global scope). Para testes rápidos.
    bool eval(const std::string& code, const std::string& name);

    bool ready() const { return ready_; }

private:
    bool ready_ = false;
    void* runtime_ = nullptr; // JSRuntime*
    void* context_ = nullptr; // JSContext*
};

ScriptEngine& engine();

// Registra NativeClass / NativeObject / NativeMethod / NativeArray / tl.* no
// contexto. Implementado em Bindings.cpp.
void installBindings(void* context);

} // namespace bl::script
