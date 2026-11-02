#include "script/api/Console.h"

#include "core/Log.h"
#include "script/dotnet/DotnetHost.h"

#include <unistd.h>

#include <atomic>
#include <deque>
#include <mutex>

namespace bl::script {

namespace {

// A saida guarda as ultimas: com o console fechado ela vai enchendo, e quem
// abre ve o que os mods disseram antes.
constexpr size_t kMaxEntries = 400;

std::mutex g_mutex;
std::deque<std::string> g_out;
std::deque<std::string> g_csCode;
bool g_csActive = false;
constexpr char kCSharpMark = '\x02';
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

} // namespace

void consoleSubmit(std::string code) {
    {
        std::lock_guard<std::mutex> guard(g_mutex);
        g_csCode.push_back(!code.empty() && code[0] == kCSharpMark ? code.substr(1) : std::move(code));
    }
    g_pending.store(true, std::memory_order_release);
}

void consolePrint(char kind, const std::string& text) {
    push(kind, text);
}

void installConsoleLog() {
    log::onLine(&onLogLine);
}

std::vector<std::string> consoleTake() {
    std::lock_guard<std::mutex> guard(g_mutex);
    std::vector<std::string> out(g_out.begin(), g_out.end());
    g_out.clear();
    return out;
}

void tickConsole() {
    if (g_csActive) dotnet::host().consoleTick();
    if (!g_pending.exchange(false, std::memory_order_acq_rel)) return;
    {
        std::deque<std::string> cs;
        {
            std::lock_guard<std::mutex> guard(g_mutex);
            cs.swap(g_csCode);
        }
        for (const std::string& code : cs) {
            if (dotnet::host().consoleSubmit(code)) {
                g_csActive = true;
            } else {
                push('x', "C#: the .NET runtime is not running");
            }
        }
    }
}

} // namespace bl::script
