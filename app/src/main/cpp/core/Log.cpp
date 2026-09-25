#include "core/Log.h"
#include <cstdarg>
#include <cstdio>
#include <ctime>
#include <mutex>
#include <string>

namespace bl::log {

namespace {
constexpr const char* TAG = "BunnyLoader";
constexpr size_t kMaxErrorChars = 8 * 1024;  // o painel nao serve pra 1 MB
FILE* g_file = nullptr;
std::mutex g_mutex;

std::string g_errors;
int g_errorCount = 0;
void (*g_onError)(const char*) = nullptr;
}

void open(const char* path) {
    std::lock_guard<std::mutex> guard(g_mutex);
    if (g_file) fclose(g_file);
    g_file = path ? fopen(path, "w") : nullptr;
}

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
    // Uma pilha de erro de mod passa facil de 1 KB; o logcat corta em ~4 KB.
    char buffer[4096];
    va_list args;
    va_start(args, fmt);
    vsnprintf(buffer, sizeof(buffer), fmt, args);
    va_end(args);

    __android_log_write(level, TAG, buffer);

    void (*notify)(const char*) = nullptr;
    {
        std::lock_guard<std::mutex> guard(g_mutex);
        if (g_file) {
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
            if (g_errors.size() < kMaxErrorChars) {
                g_errors.append(buffer).append("\n");
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
