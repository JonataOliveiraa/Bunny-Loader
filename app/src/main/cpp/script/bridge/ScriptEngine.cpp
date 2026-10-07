#include "script/bridge/ScriptEngine.h"
#include "core/Log.h"
#include "mods/ModLoader.h"
#include "script/api/SystemHooks.h"

#if BL_HAVE_QUICKJS
#include "quickjs.h"
#include "script/bridge/QuickJsExt.h"
#endif

#include <algorithm>
#include <atomic>
#include <cctype>
#include <chrono>
#include <cstdio>
#include <mutex>
#include <string>
#include <vector>

#include <unistd.h>

namespace bl::script {

ScriptEngine& engine() {
    static ScriptEngine instance;
    return instance;
}

// ------------------------------- JsLock -------------------------------

namespace {
std::timed_mutex g_jsMutex;

// O que a thread sabe do motor, num bloco so: com o minSdk 24 o thread_local
// e emulado (uma chamada a __emutls_get_address por variavel, por funcao), e
// cada chamada a mais pesava no hook com original() no bench do MuMu.
struct JsThread {
    int depth = 0;           // entradas aninhadas no motor
    int tid = 0;             // o gettid(), lido uma vez
    int parked = 0;          // original() em andamento (JsSuspend) nesta thread
    uintptr_t baseTop = 0;   // de onde o limite de pilha dela e contado
};
thread_local JsThread t_js;

// Quem esta com o motor agora (0/nulo = ninguem) e o hook em que ele esta: so
// para o aviso de quem esperou demais. O hook e global, nao por thread: quem
// solta o motor leva o seu (JsSuspend) e o devolve ao voltar.
std::atomic<int> g_ownerTid{0};
std::atomic<const void*> g_ownerHook{nullptr};

// O rt->current_stack_frame do QuickJS, pego no init (QuickJsExt.c): guardar
// e devolver a pilha de uma thread e uma leitura e uma escrita, sem chamada.
void** g_frameSlot = nullptr;

void markOwner(JsThread& t) {
    if (!t.tid) t.tid = gettid();
    g_ownerTid.store(t.tid, std::memory_order_relaxed);
}

void clearOwner() {
    g_ownerTid.store(0, std::memory_order_relaxed);
    g_ownerHook.store(nullptr, std::memory_order_relaxed);
}

void alignStackTop() {
#if BL_HAVE_QUICKJS
    if (void* rt = engine().runtime()) JS_UpdateStackTop(static_cast<JSRuntime*>(rt));
#endif
}

// O limite de pilha do motor e contado de onde a thread ENTROU nele, uma vez
// por thread: o hook aninhado que chega de dentro de um original() (o motor foi
// solto, a entrada e "do zero") e a volta do JsSuspend usam o mesmo topo.
// Realinhar pelo ponteiro de pilha de AGORA, mais fundo, dava 256 KB novos a
// cada original() aninhado, e uma cadeia de hooks podia estourar a pilha nativa
// sem o "Maximum call stack size exceeded" (o hookslots foi de 53 para 120).
uintptr_t stackTopNow() {
#if BL_HAVE_QUICKJS
    if (void* rt = engine().runtime()) return bl_js_stack_top(static_cast<JSRuntime*>(rt));
#endif
    return 0;
}

void restoreStackTop(uintptr_t top) {
#if BL_HAVE_QUICKJS
    if (void* rt = engine().runtime()) bl_js_set_stack_top(static_cast<JSRuntime*>(rt), top);
#else
    (void)top;
#endif
}

void alignStackFor(JsThread& t) {
    if (t.parked == 0) {
        alignStackTop();
        t.baseTop = stackTopNow();
    } else {
        restoreStackTop(t.baseTop);
    }
}
} // namespace

JsLock::JsLock() {
    JsThread& t = t_js;
    if (t.depth == 0) {
        g_jsMutex.lock();
        markOwner(t);
        alignStackFor(t);
    }
    ++t.depth;
    held_ = true;
}

JsLock::JsLock(int timeoutMs) {
    JsThread& t = t_js;
    if (t.depth == 0) {
        if (!g_jsMutex.try_lock_for(std::chrono::milliseconds(timeoutMs))) return;
        markOwner(t);
        alignStackFor(t);
    }
    ++t.depth;
    held_ = true;
}

JsLock::~JsLock() {
    if (!held_) return;
    if (--t_js.depth == 0) {
        // Todo JS desta entrada ja desempilhou; sobrar frame aqui e defeito
        // nosso, e o proximo a entrar herdaria uma pilha que nao e dele.
        if (g_frameSlot && *g_frameSlot) {
            static std::atomic<bool> warned{false};
            if (!warned.exchange(true)) BL_ERROR("motor JS: frames sobrando ao soltar o motor; pilha zerada");
            *g_frameSlot = nullptr;
        }
        clearOwner();
        g_jsMutex.unlock();
    }
}

JsSuspend::JsSuspend() {
    JsThread& t = t_js;
    if (t.depth == 0) return;   // nao seguramos o motor: nada a soltar
    depth_ = t.depth;
    t.depth = 0;
    if (g_frameSlot) {
        frames_ = *g_frameSlot;
        *g_frameSlot = nullptr;
    }
    ++t.parked;
    hook_ = g_ownerHook.load(std::memory_order_relaxed);
    released_ = true;
    clearOwner();
    g_jsMutex.unlock();
}

JsSuspend::~JsSuspend() {
    if (!released_) return;
    // Espera sem prazo, de proposito: nao ha o que fazer com um "desistir" aqui
    // — a nossa chamada JS esta no meio e precisa terminar de desempilhar. E
    // espera pouco: quem esta com o motor so o segura enquanto roda JS. Passou
    // de 5 s, e quase certo um impasse: o log diz quem esta com o motor.
    if (!g_jsMutex.try_lock_for(std::chrono::seconds(5))) {
        BL_ERROR("motor JS: a thread %d espera o motor ha 5 s para voltar do original de %s; com o motor: %s",
                 static_cast<int>(gettid()), describeHookForLog(hook_).c_str(), describeJsOwnerForLog().c_str());
        g_jsMutex.lock();
        BL_ERROR("motor JS: a thread %d voltou a ter o motor", static_cast<int>(gettid()));
    }
    if (g_frameSlot) *g_frameSlot = frames_;
    JsThread& t = t_js;
    markOwner(t);
    g_ownerHook.store(hook_, std::memory_order_relaxed);
    t.depth = depth_;
    restoreStackTop(t.baseTop);
    --t.parked;
}

int jsOwnerThread() { return g_ownerTid.load(std::memory_order_relaxed); }

const void* jsOwnerHook() { return g_ownerHook.load(std::memory_order_relaxed); }

const void* setJsOwnerHook(const void* method) {
    // So quem esta com o motor escreve: ler e gravar basta, sem troca atomica.
    const void* outer = g_ownerHook.load(std::memory_order_relaxed);
    g_ownerHook.store(method, std::memory_order_relaxed);
    return outer;
}

#if BL_HAVE_QUICKJS

namespace {

// ---------------------------- import entre arquivos ----------------------------
//
// Um mod divide o codigo em arquivos, como no tModLoader (um item por arquivo
// em Content/Items/...): `import { X } from './Content/Items/X.js'`.
//
// O nome de cada modulo diz de qual mod ele e: "<uid>/<caminho dentro da pasta
// do mod>", o de entrada inclusive ("<uid>/main.js"). E por esse prefixo que
// callerModId e resolvePath (Texture.cpp) acham a pasta do mod de quem chamou.
//
// So caminho relativo, e so dentro da pasta do mod: um mod nao le arquivo de
// outro nem do aparelho por import.

/** "<uid>/<rel>" -> {uid, rel}; um mod embutido ("<id>") tem rel vazio. */
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

void installModLoaderHook(JSContext* ctx);
void releaseModLoader(JSContext* ctx);

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
    g_frameSlot = bl_js_stack_frame_slot(rt);
    context_ = ctx;
    installBindings(ctx);
    installModLoaderHook(ctx);
    installModClasses(ctx);

    ready_ = true;
    BL_DEBUG("QuickJS iniciado");
    return true;
}

void ScriptEngine::shutdown() {
    if (!ready_) return;
    JsLock lock;
    g_frameSlot = nullptr;   // e do runtime que vai embora
    auto* ctx = static_cast<JSContext*>(context_);
    JSValue global = JS_GetGlobalObject(ctx);
    JSValue api = JS_GetPropertyStr(ctx, global, "bl");
    JSValue unload = JS_GetPropertyStr(ctx, api, "__unloadMods");
    if (JS_IsFunction(ctx, unload)) {
        JSValue result = JS_Call(ctx, unload, api, 0, nullptr);
        if (JS_IsException(result)) {
            JSValue error = JS_GetException(ctx);
            const char* message = JS_ToCString(ctx, error);
            BL_ERROR("OnModUnload: %s", message ? message : "erro no callback");
            if (message) JS_FreeCString(ctx, message);
            JS_FreeValue(ctx, error);
        }
        JS_FreeValue(ctx, result);
    }
    JS_FreeValue(ctx, unload);
    JS_FreeValue(ctx, api);
    JS_FreeValue(ctx, global);
    releaseSystemBindings(context_);
    releaseModLoader(static_cast<JSContext*>(context_));
    releaseNamespaceCache(context_);
    JS_FreeContext(static_cast<JSContext*>(context_));
    JS_FreeRuntime(static_cast<JSRuntime*>(runtime_));
    context_ = nullptr;
    runtime_ = nullptr;
    ready_ = false;
}

// ------------------------------ trecho do codigo ------------------------------
//
// O erro de um mod no painel diz o arquivo e a linha ("at ModifyTooltips
// (<uid>/Content/Items/X.js:8:9)"), mas quem joga no celular nao tem o arquivo
// aberto ao lado. O trecho mostra as linhas em volta, com a do erro marcada e
// um ^ na coluna:
//
//     code (Content/Items/X.js:8):
//         6 | ModifyTooltips(item, tooltips) {
//     --> 8 |     NAO_DEFINIDO;
//           |     ^
//
// A primeira posicao da pilha num arquivo de mod e a que importa: as de cima
// sao o carregador (bunny:ModClasses.js), que chamou o metodo.

namespace {

/** A primeira "<uid>/<caminho>.js:<linha>:<coluna>" do texto, fora do bunny:. */
bool findModPosition(const std::string& text, std::string* module, int* line, int* col) {
    size_t from = 0;
    while (true) {
        const size_t js = text.find(".js:", from);
        if (js == std::string::npos) return false;
        from = js + 4;
        size_t start = js;
        while (start > 0) {
            const char c = text[start - 1];
            if (c == '(' || c == ' ' || c == '\n' || c == '\t') break;
            --start;
        }
        const std::string name = text.substr(start, js + 3 - start);
        if (name.rfind("bunny:", 0) == 0 || name.find('/') == std::string::npos) continue;

        size_t p = js + 4;
        int l = 0, c = 0;
        if (p >= text.size() || !std::isdigit(static_cast<unsigned char>(text[p]))) continue;
        while (p < text.size() && std::isdigit(static_cast<unsigned char>(text[p]))) l = l * 10 + (text[p++] - '0');
        if (p < text.size() && text[p] == ':') {
            ++p;
            while (p < text.size() && std::isdigit(static_cast<unsigned char>(text[p]))) c = c * 10 + (text[p++] - '0');
        }
        if (l <= 0) continue;
        *module = name;
        *line = l;
        *col = c;
        return true;
    }
}

/** A linha com as tabulacoes trocadas por 4 espacos (o ^ precisa de colunas fixas). */
std::string expandTabs(const std::string& s) {
    std::string out;
    for (char ch : s) {
        if (ch == '\t') out += "    ";
        else if (ch != '\r') out += ch;
    }
    return out;
}

/** Quantos caracteres (nao bytes) ha em s[0, bytes). */
size_t charsIn(const std::string& s, size_t bytes) {
    size_t n = 0;
    for (size_t i = 0; i < bytes && i < s.size(); ++i) {
        if ((static_cast<unsigned char>(s[i]) & 0xC0) != 0x80) ++n;
    }
    return n;
}

/** O byte do caractere `chars` (contando de 0) de s. */
size_t byteOfChar(const std::string& s, size_t chars) {
    size_t n = 0;
    for (size_t i = 0; i < s.size(); ++i) {
        if ((static_cast<unsigned char>(s[i]) & 0xC0) == 0x80) continue;
        if (n == chars) return i;
        ++n;
    }
    return s.size();
}

} // namespace

std::string withCodeFrame(const std::string& text) {
    const std::string code = codeFrame(text);
    if (code.empty()) return text;
    const size_t eol = text.find('\n');
    if (eol == std::string::npos) return text + "\n" + code;
    return text.substr(0, eol + 1) + code + text.substr(eol + 1);
}

std::string codeFrame(const std::string& text) {
    std::string module;
    int line = 0, col = 0;
    if (!findModPosition(text, &module, &line, &col)) return {};

    const size_t slash = module.find('/');
    const std::string uid = module.substr(0, slash), rel = module.substr(slash + 1);
    const std::string& dir = mods::dirOf(uid);
    if (dir.empty() || rel.empty()) return {};
    FILE* f = std::fopen((dir + "/" + rel).c_str(), "rb");
    if (!f) return {};

    // Duas linhas antes da do erro e uma depois.
    const int first = line > 2 ? line - 2 : 1, last = line + 1;
    std::vector<std::string> lines;
    std::string cur;
    int n = 1;
    for (int ch; (ch = std::fgetc(f)) != EOF && n <= last;) {
        if (ch == '\n') {
            if (n >= first) lines.push_back(expandTabs(cur));
            cur.clear();
            ++n;
        } else {
            cur += static_cast<char>(ch);
        }
    }
    if (n >= first && n <= last && !cur.empty()) lines.push_back(expandTabs(cur));
    std::fclose(f);
    if (static_cast<int>(lines.size()) < line - first + 1) return {};

    // A coluna do QuickJS conta a partir de 1, em caracteres da linha original
    // (com as tabulacoes ainda como um caractere).
    std::string original;
    {
        FILE* g = std::fopen((dir + "/" + rel).c_str(), "rb");
        int k = 1;
        for (int ch; g && (ch = std::fgetc(g)) != EOF && k <= line;) {
            if (ch == '\n') ++k;
            else if (k == line) original += static_cast<char>(ch);
        }
        if (g) std::fclose(g);
    }
    size_t caret = 0;   // em caracteres, na linha expandida
    if (col > 0) {
        const size_t upto = byteOfChar(original, static_cast<size_t>(col - 1));
        caret = charsIn(expandTabs(original.substr(0, upto)), std::string::npos);
    }

    // Sem a indentacao em comum: no celular a largura e pouca.
    size_t indent = std::string::npos;
    for (const std::string& l : lines) {
        const size_t first_char = l.find_first_not_of(' ');
        if (first_char != std::string::npos) indent = std::min(indent, first_char);
    }
    if (indent == std::string::npos) indent = 0;
    caret = caret >= indent ? caret - indent : 0;

    const int width = static_cast<int>(std::to_string(last).size());
    std::string out = "code (" + rel + ":" + std::to_string(line) + "):\n";
    for (size_t i = 0; i < lines.size(); ++i) {
        const int number = first + static_cast<int>(i);
        std::string body = lines[i].size() > indent ? lines[i].substr(indent) : std::string();
        if (charsIn(body, std::string::npos) > 90) body = body.substr(0, byteOfChar(body, 90)) + "...";
        std::string num = std::to_string(number);
        num.insert(0, static_cast<size_t>(width) - num.size(), ' ');
        out += (number == line ? "--> " : "    ") + num + " | " + body + "\n";
        if (number == line && col > 0 && caret <= 90) {
            out += "    " + std::string(static_cast<size_t>(width), ' ') + " | " + std::string(caret, ' ') + "^\n";
        }
    }
    return out;
}

namespace {

/** Loga a excecao pendente, com a pilha JS (e o trecho do codigo) quando houver. */
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
    msg = withCodeFrame(msg);
    BL_ERROR("error in %s: %s", name.c_str(), msg.c_str());
}

void logPendingException(JSContext* ctx, const std::string& name) {
    JSValue err = JS_GetException(ctx);
    logException(ctx, err, name);
    JS_FreeValue(ctx, err);
}

/**
 * O resultado de um modulo e uma PROMISE (top-level await existe): um erro no
 * mod vem como promise rejeitada, nao como excecao. Drena os jobs e devolve o
 * valor, ou JS_EXCEPTION (ja logado). Consome a promise.
 */
JSValue settle(JSContext* ctx, JSValue promise, const std::string& name) {
    if (JS_IsException(promise)) {
        logPendingException(ctx, name);
        return JS_EXCEPTION;
    }
    JSContext* jobCtx = nullptr;
    while (JS_ExecutePendingJob(JS_GetRuntime(ctx), &jobCtx) > 0) {}

    JSValue out = JS_EXCEPTION;
    switch (JS_PromiseState(ctx, promise)) {
        case JS_PROMISE_FULFILLED:
            out = JS_PromiseResult(ctx, promise);
            break;
        case JS_PROMISE_REJECTED: {
            JSValue err = JS_PromiseResult(ctx, promise);
            logException(ctx, err, name);
            JS_FreeValue(ctx, err);
            break;
        }
        case JS_PROMISE_PENDING:
            // Top-level await esperando algo que nunca vem: nao ha loop de
            // eventos para completar isso, e o mod ficaria pela metade.
            BL_ERROR("erro em %s: o modulo ficou pendente (await no topo?)", name.c_str());
            break;
        default:
            out = JS_UNDEFINED;
            break;
    }
    JS_FreeValue(ctx, promise);
    return out;
}

/**
 * O namespace de um arquivo do mod ("<uid>/<rel>"), importado pelo mesmo
 * carregador do `import`: um arquivo que o de entrada tambem importa e o mesmo
 * modulo, avaliado uma vez so.
 */
JSValue importModFile(JSContext* ctx, const std::string& id, const std::string& rel) {
    return settle(ctx, JS_LoadModule(ctx, id.c_str(), ("./" + rel).c_str()), id + "/" + rel);
}

// O carregador dos mods (ContentAutoload, em script/js/mod/autoload.js), que o
// JS entrega uma vez por bl.__setModLoader.
JSValue g_modLoader = JS_UNDEFINED;

JSValue js_setModLoader(JSContext* ctx, JSValueConst, int argc, JSValueConst* argv) {
    if (argc < 1 || !JS_IsFunction(ctx, argv[0])) return JS_ThrowTypeError(ctx, "bl.__setModLoader(funcao)");
    JS_FreeValue(ctx, g_modLoader);
    g_modLoader = JS_DupValue(ctx, argv[0]);
    return JS_UNDEFINED;
}

void releaseModLoader(JSContext* ctx) {
    JS_FreeValue(ctx, g_modLoader);
    g_modLoader = JS_UNDEFINED;
}

void installModLoaderHook(JSContext* ctx) {
    JSValue global = JS_GetGlobalObject(ctx);
    JSValue bl = JS_GetPropertyStr(ctx, global, "bl");
    JS_SetPropertyStr(ctx, bl, "__setModLoader", JS_NewCFunction(ctx, js_setModLoader, "__setModLoader", 1));
    JS_FreeValue(ctx, bl);
    JS_FreeValue(ctx, global);
}

/** files: [[caminho, namespace], ...] -> a classe Mod, o conteudo e o Load. */
bool callModLoader(JSContext* ctx, JSValue files, const std::string& name) {
    if (!JS_IsFunction(ctx, g_modLoader)) {
        BL_ERROR("erro em %s: as classes dos mods nao carregaram (sem carregador)", name.c_str());
        return false;
    }
    JSValue r = JS_Call(ctx, g_modLoader, JS_UNDEFINED, 1, &files);
    if (JS_IsException(r)) {
        logPendingException(ctx, name);
        return false;
    }
    JS_FreeValue(ctx, r);
    JSContext* jobCtx = nullptr;
    while (JS_ExecutePendingJob(JS_GetRuntime(ctx), &jobCtx) > 0) {}
    return true;
}

JSValue pair(JSContext* ctx, const std::string& path, JSValue ns) {
    JSValue p = JS_NewArray(ctx);
    JS_SetPropertyUint32(ctx, p, 0, JS_NewString(ctx, path.c_str()));
    JS_SetPropertyUint32(ctx, p, 1, ns);
    return p;
}

} // namespace

bool ScriptEngine::loadMod(const std::string& id, const std::vector<std::string>& files) {
    if (!ready_ || files.empty()) return false;
    JsLock lock;
    auto* ctx = static_cast<JSContext*>(context_);
    JSValue list = JS_NewArray(ctx);
    bool ok = true;
    for (uint32_t i = 0; i < files.size(); ++i) {
        JSValue ns = importModFile(ctx, id, files[i]);
        if (JS_IsException(ns)) { ok = false; break; }
        JS_SetPropertyUint32(ctx, list, i, pair(ctx, files[i], ns));
    }
    if (ok) ok = callModLoader(ctx, list, id + "/" + files[0]);
    JS_FreeValue(ctx, list);
    return ok;
}

bool ScriptEngine::loadBuiltinMod(const std::string& code, const std::string& id) {
    if (!ready_) return false;
    JsLock lock;
    auto* ctx = static_cast<JSContext*>(context_);
    JSValue fn = JS_Eval(ctx, code.c_str(), code.size(), id.c_str(),
                         JS_EVAL_TYPE_MODULE | JS_EVAL_FLAG_COMPILE_ONLY);
    if (JS_IsException(fn)) {
        logPendingException(ctx, id);
        return false;
    }
    auto* m = static_cast<JSModuleDef*>(JS_VALUE_GET_PTR(fn));
    if (JS_ResolveModule(ctx, fn) < 0) {
        JS_FreeValue(ctx, fn);
        logPendingException(ctx, id);
        return false;
    }
    JSValue done = settle(ctx, JS_EvalFunction(ctx, fn), id);
    if (JS_IsException(done)) return false;
    JS_FreeValue(ctx, done);
    JSValue list = JS_NewArray(ctx);
    JS_SetPropertyUint32(ctx, list, 0, pair(ctx, "main.js", JS_GetModuleNamespace(ctx, m)));
    const bool ok = callModLoader(ctx, list, id);
    JS_FreeValue(ctx, list);
    return ok;
}

#else // sem QuickJS: stub para o projeto compilar antes do vendoring

bool ScriptEngine::init() {
    BL_WARN("ScriptEngine em modo STUB (QuickJS ausente). Ver third_party/README.md");
    ready_ = false;
    return true; // não bloqueia o boot: o jogo sobe sem mods
}
void ScriptEngine::shutdown() {}
bool ScriptEngine::loadMod(const std::string&, const std::vector<std::string>&) { return false; }
bool ScriptEngine::loadBuiltinMod(const std::string&, const std::string&) { return false; }

#endif

} // namespace bl::script
