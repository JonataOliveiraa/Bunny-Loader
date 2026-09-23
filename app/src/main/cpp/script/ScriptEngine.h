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

    // Avalia um trecho de código, também como módulo (mods embutidos).
    bool eval(const std::string& code, const std::string& name);

    bool ready() const { return ready_; }
    void* runtime() const { return runtime_; }

private:
    bool ready_ = false;
    void* runtime_ = nullptr; // JSRuntime*
    void* context_ = nullptr; // JSContext*
};

ScriptEngine& engine();

/**
 * Trava do motor JS. TODA entrada no QuickJS passa por aqui.
 *
 * O QuickJS nao e thread-safe, e o jogo chama metodo hookado de mais de uma
 * thread: a geracao de mundo cria itens de bau (Item.SetDefaults) na thread
 * dela enquanto a principal roda Player.Update. Dois JS_Call simultaneos no
 * mesmo runtime corrompem o heap — crash aleatorio, "as vezes, ao criar mundo".
 *
 * Recursiva por thread: callback -> metodo do jogo -> outro metodo hookado ->
 * callback de novo, tudo na mesma thread, e legitimo.
 *
 * A trava NAO e solta durante original(): o QuickJS guarda a pilha de frames
 * no runtime, nao na thread. Outra thread entrando no meio empilharia frames
 * por cima dos nossos e o desempilhar sairia fora de ordem.
 *
 * Ao adquirir (e so entao), realinha o limite de pilha do QuickJS com a thread
 * corrente — ele o deduz do ponteiro de pilha de quando o runtime nasceu.
 */
class JsLock {
public:
    /** Espera o quanto for preciso. Para carregar mod, onde nao ha pressa. */
    JsLock();
    /**
     * Espera no maximo `timeoutMs`. Para hook: se outra thread segurar o motor
     * esse tempo todo, e quase certo um impasse (ela esperando por nos), e
     * rodar o metodo sem o mod e melhor que congelar o jogo.
     */
    explicit JsLock(int timeoutMs);
    ~JsLock();
    JsLock(const JsLock&) = delete;
    JsLock& operator=(const JsLock&) = delete;

    bool held() const { return held_; }

private:
    bool held_ = false;
};

// Registra NativeClass / NativeObject / NativeMethod / NativeArray / tl.* no
// contexto. Implementado em Bindings.cpp.
void installBindings(void* context);

} // namespace bl::script
