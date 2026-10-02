#pragma once
#include <string>
#include <vector>

namespace bl::script {

// Wrapper do QuickJS. Um JSRuntime + um JSContext, vivendo na thread principal
// da Unity (a mesma que chamou il2cpp_init).
class ScriptEngine {
public:
    bool init();
    void shutdown();

    /**
     * Carrega um mod: importa cada arquivo como módulo ("<id>/<caminho>") e
     * entrega os namespaces ao carregador do JS (script/js/mod/autoload.js),
     * que exige a classe Mod no `export default` do primeiro e registra o
     * conteúdo dos outros. `files[0]` é o de entrada; os outros, os de
     * Content/ e Common/. Caminhos relativos à pasta do de entrada. Retorna
     * false e loga em caso de erro.
     */
    bool loadMod(const std::string& id, const std::vector<std::string>& files);

    // O mesmo para um mod embutido na libbunny (código em memória, sem pasta).
    bool loadBuiltinMod(const std::string& code, const std::string& id);

    bool ready() const { return ready_; }
    void* runtime() const { return runtime_; }
    void* context() const { return context_; }   // JSContext*

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
 * motor durante o CORPO INTEIRO do metodo, e um hook na thread do save ou da
 * carga do mundo, durante o save ou a carga inteira. Quem cai noutro hook
 * espera esse tempo todo (ate 3 s, e depois roda sem o mod). O tempo gasto ali
 * dentro nao e tempo de JS.
 *
 * Por que nao e so dar unlock: o QuickJS guarda a pilha de frames no RUNTIME
 * (`rt->current_stack_frame`), nao na thread, e o desempilhar e uma atribuicao
 * absoluta (`rt->current_stack_frame = sf->prev_frame`). Duas threads com
 * frames estacionados ao mesmo tempo, cada uma voltando por cima da outra,
 * deixariam a pilha apontando para frames que ja nao existem.
 *
 * Entao cada thread leva a SUA pilha: ao soltar, guarda o topo e deixa a do
 * runtime vazia; ao pegar de volta, devolve o topo (QuickJsExt.c, que
 * compila o quickjs.c com esse acesso). Quem entra do zero comeca de uma
 * pilha vazia e sai deixando-a vazia. Com isso qualquer numero de threads
 * pode estar no original() ao mesmo tempo, e o motor fica preso so enquanto
 * o JS roda de fato.
 */
class JsSuspend {
public:
    JsSuspend();
    ~JsSuspend();
    JsSuspend(const JsSuspend&) = delete;
    JsSuspend& operator=(const JsSuspend&) = delete;

    /** false = esta thread nao estava com o motor: nao havia o que soltar. */
    bool released() const { return released_; }

private:
    int depth_ = 0;
    void* frames_ = nullptr;        // o topo da pilha de frames JS desta thread
    const void* hook_ = nullptr;    // o hook em que ela estava (diagnostico)
    bool released_ = false;
};

/**
 * Quem esta com o motor JS agora, so para diagnostico: o aviso de quem esperou
 * demais diz a thread (tid; 0 = ninguem) e o hook em que ela esta (o
 * MethodInfo; nulo = fora de hook, carregando mod ou conteudo).
 */
int jsOwnerThread();
const void* jsOwnerHook();

/** Marca o hook em que quem esta com o motor entrou. Devolve o de fora. */
const void* setJsOwnerHook(const void* method);

/** Para o log: quem esta com o motor, e o nome de um hook (JsHook.cpp). */
std::string describeJsOwnerForLog();
std::string describeHookForLog(const void* method);

/**
 * As linhas do arquivo do mod em volta da primeira posicao de mod de uma pilha
 * de erro ("<uid>/Content/X.js:8:9"), com a do erro marcada e um ^ na coluna;
 * vazio se nao ha posicao de mod ou o arquivo nao abre. Para o log e o painel.
 */
std::string codeFrame(const std::string& text);

/**
 * O texto de um erro com o trecho do codigo logo depois da primeira linha (a
 * mensagem), antes das linhas "at ..." da pilha: no painel do celular e o
 * trecho que se le primeiro. Sem posicao de mod, o texto como veio.
 */
std::string withCodeFrame(const std::string& text);

// Registra NativeClass / NativeObject / NativeMethod / NativeArray / tl.* no
// contexto. Implementado em Bindings.cpp.
void installBindings(void* context);

// As classes base dos mods (ModItem...), escritas em JS e avaliadas no escopo
// global depois dos bindings. Implementado em ModClasses.cpp.
void installModClasses(void* context);

} // namespace bl::script
