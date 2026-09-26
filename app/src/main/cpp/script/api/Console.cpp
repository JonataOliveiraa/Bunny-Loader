#include "script/api/Console.h"

#include "core/Log.h"
#include "il2cpp/Api.h"
#include "il2cpp/Resolver.h"

#include <unistd.h>

#include <atomic>
#include <chrono>
#include <cstdio>
#include <deque>
#include <mutex>

#if BL_HAVE_QUICKJS
#include "script/bridge/Bridge.h"
#endif

namespace bl::script {

namespace {

// A saida guarda as ultimas: com o console fechado ela vai enchendo, e quem
// abre ve o que os mods disseram antes.
constexpr size_t kMaxEntries = 400;
constexpr size_t kMaxChat = 60;

struct ChatLine {
    std::string text;
    uint8_t r, g, b;
};

std::mutex g_mutex;
std::deque<std::string> g_out;
std::deque<std::string> g_code;
std::deque<ChatLine> g_chat;
std::atomic<bool> g_pending{false};

void push(char kind, const std::string& text) {
    std::lock_guard<std::mutex> guard(g_mutex);
    g_out.push_back(std::string(1, kind) + text);
    while (g_out.size() > kMaxEntries) g_out.pop_front();
}

// Tudo o que vai ao log de sessao (o resumo do nucleo, o bl.log dos mods, os
// avisos e erros) aparece tambem no console, ao vivo.
void onLogLine(int level, const char* line) {
    push(level >= ANDROID_LOG_ERROR ? 'e' : level >= ANDROID_LOG_WARN ? 'w' : 'l', line);
}

void queueChat(const std::string& text, uint8_t r, uint8_t g, uint8_t b) {
    {
        std::lock_guard<std::mutex> guard(g_mutex);
        g_chat.push_back({text, r, g, b});
        while (g_chat.size() > kMaxChat) g_chat.pop_front();
        g_out.push_back("p" + text);
        while (g_out.size() > kMaxEntries) g_out.pop_front();
    }
    g_pending.store(true, std::memory_order_release);
}

// ------------------------------ chat ------------------------------

/** Main.NewText(string, byte, byte, byte), na thread do jogo. */
void drainChat() {
    std::deque<ChatLine> lines;
    {
        std::lock_guard<std::mutex> guard(g_mutex);
        lines.swap(g_chat);
    }
    if (lines.empty()) return;
    auto& a = il2cpp::api();
    static const MethodInfo* newText = [] {
        Il2CppClass* main = il2cpp::findClass({"Terraria", "Main", {}});
        const MethodInfo* m = main ? il2cpp::api().class_get_method_from_name(main, "NewText", 4) : nullptr;
        if (!m) BL_ERROR("console: Main.NewText(string, byte, byte, byte) nao encontrado");
        return m;
    }();
    if (!newText) return;
    for (const ChatLine& line : lines) {
        // Uma linha de chat por linha do texto: o chat do jogo nao quebra no \n.
        size_t at = 0;
        while (at <= line.text.size()) {
            size_t end = line.text.find('\n', at);
            if (end == std::string::npos) end = line.text.size();
            std::string piece = line.text.substr(at, end - at);
            Il2CppString* s = a.string_new(piece.c_str());
            uint8_t r = line.r, g = line.g, b = line.b;
            void* args[4] = {s, &r, &g, &b};
            Il2CppObject* exc = nullptr;
            a.runtime_invoke(newText, nullptr, args, &exc);
            if (exc) { BL_ERROR("console: Main.NewText lancou excecao"); return; }
            at = end + 1;
        }
    }
}

#if BL_HAVE_QUICKJS

// ------------------------------ o codigo do console ------------------------------

// Um laco infinito digitado no console nao pode congelar o jogo: o QuickJS
// pergunta ao interrupt handler de tempos em tempos (a cada ~10 mil operacoes),
// e com o prazo vencido a execucao para com um erro. So a thread do console:
// um original() chamado pelo codigo solta o motor, e outra thread que entre
// nele nesse meio tempo nao tem nada com o prazo.
constexpr auto kConsoleBudget = std::chrono::seconds(8);
std::chrono::steady_clock::time_point g_deadline;
int g_consoleTid = 0;
bool g_interrupted = false;

int interruptConsole(JSRuntime*, void*) {
    if (static_cast<int>(gettid()) != g_consoleTid) return 0;
    if (std::chrono::steady_clock::now() < g_deadline) return 0;
    g_interrupted = true;
    return 1;
}

/** O resultado como o console mostra: texto entre aspas, funcao pelo nome. */
std::string repr(JSContext* ctx, JSValueConst v) {
    if (JS_IsUndefined(v)) return "undefined";
    if (JS_IsString(v)) {
        JSValue json = JS_JSONStringify(ctx, v, JS_UNDEFINED, JS_UNDEFINED);
        std::string out;
        if (const char* s = JS_ToCString(ctx, json)) { out = s; JS_FreeCString(ctx, s); }
        JS_FreeValue(ctx, json);
        return out;
    }
    if (JS_IsFunction(ctx, v)) {
        JSValue name = JS_GetPropertyStr(ctx, v, "name");
        std::string out = "ƒ ";
        if (const char* s = JS_ToCString(ctx, name)) { out += *s ? s : "(anônima)"; JS_FreeCString(ctx, s); }
        JS_FreeValue(ctx, name);
        return out + "()";
    }
    return valueToLogText(ctx, v);
}

std::string errorText(JSContext* ctx, JSValueConst e) {
    if (g_interrupted) {
        return "parou: o código passou de 8 s rodando (um laço sem fim?). O jogo seguiu.";
    }
    std::string out = valueToLogText(ctx, e);
    if (JS_IsError(e)) {
        JSValue stack = JS_GetPropertyStr(ctx, e, "stack");
        if (JS_IsString(stack)) {
            if (const char* s = JS_ToCString(ctx, stack)) {
                if (*s) out += std::string("\n") + s;
                JS_FreeCString(ctx, s);
            }
        }
        JS_FreeValue(ctx, stack);
    }
    return out;
}

void runConsole(const std::string& code) {
    if (!engine().ready()) {
        push('x', "o motor JS ainda não subiu");
        return;
    }
    JsLock lock;
    auto* ctx = static_cast<JSContext*>(engine().context());
    JSRuntime* rt = JS_GetRuntime(ctx);
    // Num bloco: `let` e `const` valem so nesta execucao, e o mesmo trecho roda
    // de novo sem "redeclaration". O valor da ultima expressao sai do bloco do
    // mesmo jeito. Para guardar algo entre execucoes: `var`, ou globalThis.x.
    //
    // O bloco de fora traz os atalhos de quem testa algo no jogo (Main, ID e o
    // jogador local); o de dentro e o codigo, que pode redeclara-los. Tudo na
    // primeira linha: a linha de um erro e a mesma do editor.
    const std::string wrapped =
        "{ const Main = Terraria.Main, ID = Terraria.ID, player = Main.player[Main.myPlayer]; {" +
        code + "\n} }";
    g_interrupted = false;
    g_consoleTid = static_cast<int>(gettid());
    g_deadline = std::chrono::steady_clock::now() + kConsoleBudget;
    JS_SetInterruptHandler(rt, interruptConsole, nullptr);
    JSValue v = JS_Eval(ctx, wrapped.c_str(), wrapped.size(), "console", JS_EVAL_TYPE_GLOBAL);
    if (JS_IsException(v)) {
        JSValue e = JS_GetException(ctx);
        push('x', errorText(ctx, e));
        JS_FreeValue(ctx, e);
    } else {
        push('r', repr(ctx, v));
    }
    JS_FreeValue(ctx, v);
    // As promessas que o codigo deixou (um .then(print)) andam agora.
    JSContext* jobCtx = nullptr;
    while (JS_ExecutePendingJob(rt, &jobCtx) > 0) {
    }
    JS_SetInterruptHandler(rt, nullptr, nullptr);
}

// ------------------------------ print e bl.chat ------------------------------

std::string joinValues(JSContext* ctx, int argc, JSValueConst* argv, int from) {
    std::string line;
    for (int i = from; i < argc; ++i) {
        if (i > from) line += ' ';
        line += valueToLogText(ctx, argv[i]);
    }
    return line;
}

JSValue js_print(JSContext* ctx, JSValueConst, int argc, JSValueConst* argv) {
    queueChat(joinValues(ctx, argc, argv, 0), 255, 255, 255);
    return JS_UNDEFINED;
}

uint8_t channel(JSContext* ctx, JSValueConst obj, const char* name) {
    JSValue v = JS_GetPropertyStr(ctx, obj, name);
    int32_t n = 255;
    if (JS_IsNumber(v)) JS_ToInt32(ctx, &n, v);
    JS_FreeValue(ctx, v);
    return static_cast<uint8_t>(n < 0 ? 0 : n > 255 ? 255 : n);
}

/** Color do jogo, { R, G, B }, [r, g, b] ou '#RRGGBB'. */
bool readColor(JSContext* ctx, JSValueConst c, uint8_t* r, uint8_t* g, uint8_t* b) {
    if (JS_IsString(c)) {
        const char* s = JS_ToCString(ctx, c);
        if (!s) return false;
        const char* hex = s[0] == '#' ? s + 1 : s;
        unsigned v = 0;
        const bool ok = std::sscanf(hex, "%6x", &v) == 1;
        JS_FreeCString(ctx, s);
        if (!ok) return false;
        *r = (v >> 16) & 255; *g = (v >> 8) & 255; *b = v & 255;
        return true;
    }
    if (JS_IsArray(c)) {
        const char* keys[3] = {"0", "1", "2"};
        *r = channel(ctx, c, keys[0]); *g = channel(ctx, c, keys[1]); *b = channel(ctx, c, keys[2]);
        return true;
    }
    if (JS_IsObject(c)) {
        *r = channel(ctx, c, "R"); *g = channel(ctx, c, "G"); *b = channel(ctx, c, "B");
        return true;
    }
    return false;
}

JSValue js_chat(JSContext* ctx, JSValueConst, int argc, JSValueConst* argv) {
    if (argc < 1) return JS_ThrowTypeError(ctx, "bl.chat(texto, cor?)");
    uint8_t r = 255, g = 255, b = 255;
    if (argc > 1 && !JS_IsUndefined(argv[1]) && !readColor(ctx, argv[1], &r, &g, &b)) {
        return JS_ThrowTypeError(ctx, "bl.chat: a cor e um Color, { R, G, B }, [r, g, b] ou '#RRGGBB'");
    }
    queueChat(valueToLogText(ctx, argv[0]), r, g, b);
    return JS_UNDEFINED;
}

#endif // BL_HAVE_QUICKJS

} // namespace

void consoleSubmit(std::string code) {
    {
        std::lock_guard<std::mutex> guard(g_mutex);
        g_code.push_back(std::move(code));
    }
    g_pending.store(true, std::memory_order_release);
}

std::vector<std::string> consoleTake() {
    std::lock_guard<std::mutex> guard(g_mutex);
    std::vector<std::string> out(g_out.begin(), g_out.end());
    g_out.clear();
    return out;
}

void tickConsole() {
    if (!g_pending.exchange(false, std::memory_order_acq_rel)) return;
    drainChat();
#if BL_HAVE_QUICKJS
    std::deque<std::string> codes;
    {
        std::lock_guard<std::mutex> guard(g_mutex);
        codes.swap(g_code);
    }
    for (const std::string& code : codes) runConsole(code);
    // O que o codigo mandou ao chat vai no mesmo quadro.
    drainChat();
#endif
}

#if BL_HAVE_QUICKJS
void installConsoleApi(JSContext* ctx, JSValueConst global, JSValueConst bl) {
    JS_SetPropertyStr(ctx, global, "print", JS_NewCFunction(ctx, js_print, "print", 1));
    JS_SetPropertyStr(ctx, bl, "chat", JS_NewCFunction(ctx, js_chat, "chat", 2));
    log::onLine(&onLogLine);
}
#endif

} // namespace bl::script
