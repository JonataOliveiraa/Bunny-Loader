#include "content/common/GameTexts.h"
#include "content/common/ContentAssets.h"
#include "content/common/ModLanguage.h"
#include "core/Config.h"
#include "core/Log.h"

#include <mutex>
#include <unordered_map>
#include <vector>

namespace bl::runtime {

namespace {

std::mutex g_mx;
std::unordered_map<std::string, content::CultureNames> g_texts;

std::vector<std::string> split(const std::string& text, char separator) {
    std::vector<std::string> parts;
    size_t start = 0;
    for (size_t at = text.find(separator); at != std::string::npos; at = text.find(separator, start)) {
        parts.push_back(text.substr(start, at - start));
        start = at + 1;
    }
    parts.push_back(text.substr(start));
    return parts;
}

}

void registerGameTexts() {
    const std::string& packed = config().gameTexts;
    if (packed.empty()) {
        BL_WARN("game texts: none from the launcher");
        return;
    }
    size_t count = 0;
    for (const std::string& entry : split(packed, '\x1C')) {
        const size_t at = entry.find('\x1D');
        if (at == std::string::npos) continue;
        const std::string key = entry.substr(0, at);
        content::CultureNames names;
        for (const std::string& pair : split(entry.substr(at + 1), '\x1E')) {
            const size_t cut = pair.find('\x1F');
            if (cut != std::string::npos) names.emplace_back(pair.substr(0, cut), pair.substr(cut + 1));
        }
        if (key.empty() || names.empty()) continue;
        {
            std::lock_guard<std::mutex> l(g_mx);
            g_texts[key] = names;
        }
        modLanguageText(key.find('.') == std::string::npos ? "BunnyLoader." + key : key, &names, true);
        ++count;
    }
    BL_DEBUG("game texts: %zu keys", count);
}

std::string gameText(const std::string& key) {
    content::CultureNames names;
    {
        std::lock_guard<std::mutex> l(g_mx);
        auto it = g_texts.find(key);
        if (it == g_texts.end()) return key;
        names = it->second;
    }
    return content::textForCulture(names, key);
}

}
