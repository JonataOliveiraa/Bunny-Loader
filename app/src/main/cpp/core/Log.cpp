#include "core/Log.h"
#include <cstdarg>
#include <cstdio>
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

void write(int level, const char* fmt, ...) {
    char buffer[1024];
    va_list args;
    va_start(args, fmt);
    vsnprintf(buffer, sizeof(buffer), fmt, args);
    va_end(args);

    __android_log_write(level, TAG, buffer);

    void (*notify)(const char*) = nullptr;
    {
        std::lock_guard<std::mutex> guard(g_mutex);
        if (g_file) {
            fprintf(g_file, "%s\n", buffer);
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
    std::lock_guard<std::mutex> guard(g_mutex);
    g_onError = fn;
}

} // namespace bl::log
