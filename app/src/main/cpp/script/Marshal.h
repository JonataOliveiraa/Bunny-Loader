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
    ArgPack() = default;
    ~ArgPack();
    ArgPack(const ArgPack&) = delete;
    ArgPack& operator=(const ArgPack&) = delete;

    /**
     * Converte `argv` conforme os tipos declarados de `m`.
     * @return false e deixa uma exceção no ctx se algo não converter.
     */
    bool build(JSContext* ctx, const MethodInfo* m, int argc, JSValueConst* argv);

    void** data() { return count_ ? slots_ : nullptr; }

private:
    /** Espaço para o valor de um argumento, alinhado para qualquer tipo. */
    void* reserve(size_t bytes);

    // Os valores por cópia precisam sobreviver até a chamada. Quase toda
    // chamada cabe aqui dentro, sem tocar no heap: 16 argumentos é o limite da
    // ABI que a ponte captura, e 256 bytes atendem mesmo um punhado de structs.
    // O que não couber cai no `grandes_`, que aí sim aloca.
    static constexpr size_t kArena = 256;
    static constexpr int kMaxArgs = 16;
    alignas(16) uint8_t arena_[kArena];
    size_t usado_ = 0;
    void* slots_[kMaxArgs];
    int count_ = 0;
    std::vector<std::unique_ptr<uint8_t[]>> grandes_;
    // Referencias CRIADAS aqui (a string de um argumento) vivem so neste
    // buffer do malloc ate a chamada — o coletor do jogo nao as ve. Seguramos
    // um gchandle de cada ate o ArgPack morrer.
    std::vector<uint32_t> handles_;
};

/** Boxed/objeto -> valor JS, conforme o tipo de retorno de `m`. */
JSValue fromReturn(JSContext* ctx, const MethodInfo* m, Il2CppObject* ret);

/** Il2CppString -> std::string (UTF-8). */
std::string stringToUtf8(Il2CppString* s);

} // namespace bl::script
#endif
