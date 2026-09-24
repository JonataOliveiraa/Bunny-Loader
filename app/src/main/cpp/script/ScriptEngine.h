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
 * Para soltar a trava enquanto roda o metodo do jogo, ver JsSuspend.
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

/**
 * Solta o motor JS enquanto roda algo que NAO e JS — na pratica, o metodo
 * original do jogo dentro de um hook.
 *
 * O problema: sem isto, um hook em metodo quente (Player.Update) segura o
 * motor durante o CORPO INTEIRO do metodo. Outra thread que caia noutro hook
 * espera 3 s e roda sem o mod. O tempo gasto ali dentro nao e tempo de JS.
 *
 * Por que nao e so dar unlock: o QuickJS guarda a pilha de frames no RUNTIME
 * (`rt->current_stack_frame`), nao na thread, e o desempilhar e uma atribuicao
 * absoluta (`rt->current_stack_frame = sf->prev_frame`), nao uma verificacao.
 * Uma thread que entre no meio empilha e desempilha de forma BALANCEADA, entao
 * devolve a corrente como achou — isso e seguro. O que quebra e DUAS threads
 * estacionarem frames ao mesmo tempo: a corrente deixa de ser pilha e cada uma
 * restaura por cima da outra.
 *
 * Entao a regra e uma so: no maximo uma thread por vez pode deixar frames
 * estacionados. Quem chega depois nao solta a trava e se comporta como antes —
 * nunca pior que hoje, e melhor sempre que so uma thread estiver no original().
 *
 * O jeito limpo seria salvar e restaurar `current_stack_frame`, mas isso pede
 * uma funcao a mais no QuickJS, que aqui NAO e versionado (clonado no build,
 * ver third_party/README.md): o patch se perderia no proximo clone.
 */
class JsSuspend {
public:
    JsSuspend();
    ~JsSuspend();
    JsSuspend(const JsSuspend&) = delete;
    JsSuspend& operator=(const JsSuspend&) = delete;

    /** false = a trava continua nossa (outra thread ja estava estacionada). */
    bool released() const { return released_; }

private:
    int depth_ = 0;
    bool released_ = false;
};

// Registra NativeClass / NativeObject / NativeMethod / NativeArray / tl.* no
// contexto. Implementado em Bindings.cpp.
void installBindings(void* context);

// As classes base dos mods (ModItem...), escritas em JS e avaliadas no escopo
// global depois dos bindings. Implementado em ModClasses.cpp.
void installModClasses(void* context);

} // namespace bl::script
