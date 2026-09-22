#pragma once
#include "script/ScriptEngine.h"

#if BL_HAVE_QUICKJS
#include "quickjs.h"
#include "il2cpp/Types.h"
#include <memory>
#include <string>
#include <vector>

namespace bl::script {

/**
 * Conversão JS <-> IL2CPP para chamar métodos do jogo.
 *
 * `il2cpp_runtime_invoke` recebe `void** params` com uma convenção que não é
 * óbvia: para tipo por VALOR entra o endereço do valor; para tipo por
 * REFERÊNCIA entra o próprio ponteiro do objeto. Errar isso não dá erro — dá
 * lixo lido como ponteiro.
 *
 * O retorno vem sempre como `Il2CppObject*`: boxed para valor, o objeto em si
 * para referência, nullptr para void.
 */
class ArgPack {
public:
    /**
     * Converte `argv` conforme os tipos declarados de `m`.
     * @return false e deixa uma exceção no ctx se algo não converter.
     */
    bool build(JSContext* ctx, const MethodInfo* m, int argc, JSValueConst* argv);

    void** data() { return slots_.empty() ? nullptr : slots_.data(); }

private:
    // Os valores por cópia precisam sobreviver até a chamada; guardamos aqui e
    // apontamos para dentro. `storage_` é uma deque-like: nunca realoca, senão
    // os ponteiros em slots_ ficariam pendurados.
    std::vector<std::unique_ptr<uint8_t[]>> storage_;
    std::vector<void*> slots_;
};

/** Boxed/objeto -> valor JS, conforme o tipo de retorno de `m`. */
JSValue fromReturn(JSContext* ctx, const MethodInfo* m, Il2CppObject* ret);

/** Il2CppString -> std::string (UTF-8). */
std::string stringToUtf8(Il2CppString* s);

} // namespace bl::script
#endif
