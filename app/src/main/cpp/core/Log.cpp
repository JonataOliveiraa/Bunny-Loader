#include "core/Log.h"
#include <cstdarg>
#include <cstdio>
#include <mutex>

namespace bl::log {

namespace {
constexpr const char* TAG = "BunnyLoader";
FILE* g_file = nullptr;
std::mutex g_mutex;
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

    std::lock_guard<std::mutex> guard(g_mutex);
    if (g_file) {
        fprintf(g_file, "%s\n", buffer);
        fflush(g_file);
    }
}

} // namespace bl::log
