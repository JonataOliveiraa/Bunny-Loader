#include "core/Log.h"
#include <algorithm>
#include <atomic>
#include <cstdarg>
#include <cstdio>
#include <cstring>
#include <ctime>
#include <mutex>
#include <string>

namespace bl::log {

namespace {
constexpr const char* TAG = "BunnyLoader";
constexpr size_t kMaxErrorChars = 8 * 1024;  // o painel nao serve pra 1 MB
// O logd corta a linha em ~4 KB; acima disso ela vai ao logcat em pedacos.
constexpr size_t kLogcatChunk = 4000;
FILE* g_file = nullptr;
std::mutex g_mutex;
std::atomic<bool> g_verbose{false};

std::string g_errors;
int g_errorCount = 0;
void (*g_onError)(const char*) = nullptr;

/** Ao logcat, em pedacos que nao cortam um caractere UTF-8 ao meio. */
void toLogcat(int level, const char* text, size_t len) {
    if (len <= kLogcatChunk) {
        __android_log_write(level, TAG, text);
        return;
    }
    std::string piece;
    for (size_t at = 0; at < len;) {
        size_t n = len - at < kLogcatChunk ? len - at : kLogcatChunk;
        while (n > 1 && at + n < len && (static_cast<unsigned char>(text[at + n]) & 0xC0) == 0x80) --n;
        piece.assign(text + at, n);
        __android_log_write(level, TAG, piece.c_str());
        at += n;
    }
}
}

void open(const char* path) {
    std::lock_guard<std::mutex> guard(g_mutex);
    if (g_file) fclose(g_file);
    g_file = path ? fopen(path, "w") : nullptr;
}

void setVerbose(bool on) { g_verbose.store(on, std::memory_order_relaxed); }

/** A letra do nivel, como no logcat: I, W, E... */
static char levelLetter(int level) {
    switch (level) {
        case ANDROID_LOG_VERBOSE: return 'V';
        case ANDROID_LOG_DEBUG: return 'D';
        case ANDROID_LOG_WARN: return 'W';
        case ANDROID_LOG_ERROR: return 'E';
        case ANDROID_LOG_FATAL: return 'F';
        default: return 'I';
    }
}

void write(int level, const char* fmt, ...) {
    // Quase toda linha cabe aqui; a que nao cabe (um bl.log de objeto grande,
    // uma pilha de erro de mod) ia cortada em 4 KB. Agora vai inteira.
    char stackBuffer[4096];
    std::string heapBuffer;
    va_list args;
    va_start(args, fmt);
    va_list again;
    va_copy(again, args);
    const int needed = vsnprintf(stackBuffer, sizeof(stackBuffer), fmt, args);
    va_end(args);
    if (needed < 0) stackBuffer[0] = '\0';   // formato invalido: linha vazia, nao lixo
    const char* buffer = stackBuffer;
    size_t length = needed > 0 ? static_cast<size_t>(needed) : 0;
    if (length >= sizeof(stackBuffer)) {
        heapBuffer.resize(length + 1);
        vsnprintf(&heapBuffer[0], heapBuffer.size(), fmt, again);
        heapBuffer.resize(length);
        buffer = heapBuffer.c_str();
    }
    va_end(again);

    toLogcat(level, buffer, length);

    void (*notify)(const char*) = nullptr;
    {
        std::lock_guard<std::mutex> guard(g_mutex);
        if (g_file && (level >= ANDROID_LOG_INFO || g_verbose.load(std::memory_order_relaxed))) {
            // No arquivo, com hora e nivel: e ele que o modder abre, sem logcat.
            timespec ts{};
            clock_gettime(CLOCK_REALTIME, &ts);
            tm t{};
            localtime_r(&ts.tv_sec, &t);
            fprintf(g_file, "%02d:%02d:%02d.%03ld %c %s\n", t.tm_hour, t.tm_min, t.tm_sec,
                    ts.tv_nsec / 1000000, levelLetter(level), buffer);
            fflush(g_file);
        }
        if (level >= ANDROID_LOG_ERROR) {
            ++g_errorCount;
            // Guarda os PRIMEIROS: o erro que importa e o que comeca a cascata,
            // e um hook que falha a cada frame afogaria o resto.
            // A linha agora vem inteira: uma so nao pode passar do teto.
            if (g_errors.size() < kMaxErrorChars) {
                g_errors.append(buffer, std::min(length, kMaxErrorChars - g_errors.size())).append("\n");
            }
            notify = g_onError;
        }
    }
    // Fora do lock: quem escuta vai mexer em UI e pode logar de volta.
    if (notify) notify(buffer);
}

std::string errorsSoFar() {
    std::lock_guard<std::mutex> guard(g_mutex);
    return g_errors;
}

int errorCount() {
    std::lock_guard<std::mutex> guard(g_mutex);
    return g_errorCount;
}

void onError(void (*fn)(const char*)) {
    bool atrasados = false;
    {
        std::lock_guard<std::mutex> guard(g_mutex);
        g_onError = fn;
        atrasados = fn && g_errorCount > 0;
    }
    // O painel so nasce depois que a Activity do jogo existe, e a sonda carrega
    // os mods ANTES disso. Sem este aviso, o erro mais importante do boot — um
    // mod que nao carregou — era justamente o unico que nunca chegava a tela.
    // O ouvinte ignora a linha e mostra log::errorsSoFar(), entao um toque
    // basta para trazer tudo o que ficou para tras.
    if (atrasados) fn("(erros anteriores ao painel)");
}

} // namespace bl::log
