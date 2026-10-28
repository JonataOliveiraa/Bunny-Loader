#include "content/common/ModLanguage.h"
#include "boot/Boot.h"
#include "il2cpp/Api.h"

#include <unistd.h>

#include <atomic>
#include <map>
#include <mutex>

namespace bl::runtime {

namespace {

struct Text {
    content::CultureNames names;
    uint32_t handle = 0;
    std::string applied;
    bool inGame = false;
    bool dirty = false;
};

std::mutex g_mx;
std::map<std::string, Text> g_texts;
std::atomic<bool> g_pending{false};
std::string g_culture;
bool g_cultureSeen = false;
int g_frame = 0;

void apply(const std::string& key, Text& text, Il2CppObject* object) {
    text.applied = content::textForCulture(text.names, key);
    content::setLocalizedTextValue(object, text.applied);
    text.dirty = false;
}

}

Il2CppObject* modLanguageText(const std::string& key, const content::CultureNames* names, bool replace) {
    auto& a = il2cpp::api();
    std::lock_guard<std::mutex> l(g_mx);
    auto it = g_texts.find(key);
    if (it != g_texts.end()) {
        if (names && replace) {
            it->second.names = *names;
            it->second.dirty = true;
            g_pending.store(true, std::memory_order_release);
        }
        return a.gchandle_get_target(it->second.handle);
    }
    if (!names) return content::findLanguageText(key);
    Text text;
    text.names = *names;
    text.applied = content::textForCulture(text.names, key);
    Il2CppObject* object = content::newLocalizedText(key, text.applied);
    if (!object) return nullptr;
    text.handle = a.gchandle_new(object, false);
    const int game = gameThreadId();
    if (game != 0 && static_cast<int>(gettid()) == game) {
        content::addLanguageKey(key, object);
        text.inGame = true;
    }
    g_texts.emplace(key, std::move(text));
    g_pending.store(true, std::memory_order_release);
    return object;
}

void tickModLanguage() {
    const bool pending = g_pending.exchange(false, std::memory_order_acq_rel);
    if (!pending && ++g_frame % 15 != 0) return;
    std::lock_guard<std::mutex> l(g_mx);
    if (g_texts.empty()) return;
    auto& a = il2cpp::api();
    const std::string culture = content::activeCultureName();
    bool reapply = !g_cultureSeen || culture != g_culture;
    g_culture = culture;
    g_cultureSeen = true;
    if (!reapply) {
        for (auto& [key, text] : g_texts) {
            if (!text.inGame) continue;
            Il2CppObject* object = a.gchandle_get_target(text.handle);
            reapply = object && content::localizedTextValue(object) != text.applied;
            break;
        }
    }
    for (auto& [key, text] : g_texts) {
        Il2CppObject* object = a.gchandle_get_target(text.handle);
        if (!object) continue;
        if (!text.inGame) {
            content::addLanguageKey(key, object);
            text.inGame = true;
        }
        if (reapply || text.dirty) apply(key, text, object);
    }
}

}
