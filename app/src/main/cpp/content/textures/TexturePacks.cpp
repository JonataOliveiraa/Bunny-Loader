#include "content/textures/TexturePacks.h"
#include "content/textures/TexturePackNames.h"
#include "content/common/ContentAssets.h"
#include "content/common/ModContent.h"
#include "core/Log.h"
#include "il2cpp/Api.h"
#include "il2cpp/Resolver.h"
#include "hook/HookManager.h"

#include <algorithm>
#include <atomic>
#include <dirent.h>
#include <map>
#include <mutex>
#include <unordered_set>
#include <sys/stat.h>
#include <thread>

namespace bl::runtime {
namespace {
struct Pack { std::string uid, images; };
struct Candidate { std::string uid, path, name; bool failed = false; };
struct NamedSaved { uint32_t asset, value, source, replacement; int state; };
std::mutex g_mutex;
std::vector<Pack> g_pending;
std::atomic<bool> g_dirty{false};
std::vector<Il2CppObject*> g_tableAssets;
std::unordered_set<Il2CppObject*> g_tableSet;
int g_sweep = 0;
std::map<std::string, std::vector<Candidate>> g_named;
std::map<Il2CppObject*, NamedSaved> g_namedSaved;
std::thread::id g_gameThread;
thread_local bool g_bypass = false;
void (*g_load)(Il2CppObject*, const MethodInfo*) = nullptr;
bool g_hooked = false;

std::string canonical(std::string name) {
    for (auto& c : name) {
        if (c == '\\') c = '/';
        else if (c >= 'A' && c <= 'Z') c += 'a' - 'A';
    }
    return name;
}

bool directory(const std::string& path) {
    struct stat st{};
    return lstat(path.c_str(), &st) == 0 && S_ISDIR(st.st_mode);
}

void collectPngs(const std::string& images, const std::string& folder, std::vector<std::string>& files) {
    DIR* dir = opendir((images + "/" + folder).c_str());
    if (!dir) return;
    while (dirent* e = readdir(dir)) {
        if (std::string_view(e->d_name) == "." || std::string_view(e->d_name) == "..") continue;
        const std::string name = folder + e->d_name;
        struct stat st{};
        if (lstat((images + "/" + name).c_str(), &st) != 0) continue;
        if (S_ISDIR(st.st_mode)) collectPngs(images, name + "/", files);
        else if (S_ISREG(st.st_mode) && name.size() > 4 && canonical(name.substr(name.size() - 4)) == ".png") files.push_back(name);
    }
    closedir(dir);
}

std::vector<std::string> pngs(const std::string& images) {
    std::vector<std::string> files;
    if (directory(images)) collectPngs(images, {}, files);
    std::sort(files.begin(), files.end());
    return files;
}

bool applyNamed(Il2CppObject* original, bool loadOriginal = true) {
    const std::string name = content::textureAssetName(original);
    auto candidates = g_named.find(canonical(name));
    if (candidates == g_named.end()) return false;
    auto& a = il2cpp::api();
    auto saved = g_namedSaved.find(original);
    if (saved != g_namedSaved.end()) {
        return content::replaceTextureAssetValue(original, a.gchandle_get_target(saved->second.replacement), nullptr);
    }
    const bool bypass = g_bypass;
    g_bypass = true;
    int ow = 0, oh = 0;
    const bool dimensions = (loadOriginal || content::textureAssetValue(original)) && content::textureAssetSize(original, &ow, &oh);
    auto* value = content::textureAssetValue(original);
    auto* source = content::textureAssetSource(original);
    const int state = content::textureAssetState(original);
    if (state < 0) { g_bypass = bypass; return false; }
    for (auto& candidate : candidates->second) {
        if (candidate.failed) continue;
        uint32_t assetHandle = a.gchandle_new(original, false);
        uint32_t valueHandle = value ? a.gchandle_new(value, false) : 0;
        uint32_t sourceHandle = source ? a.gchandle_new(source, false) : 0;
        int w = 0, h = 0;
        auto* replacement = content::loadPackTexture(candidate.path, &w, &h);
        uint32_t replacementHandle = replacement ? a.gchandle_new(replacement, false) : 0;
        if (!assetHandle || (value && !valueHandle) || (source && !sourceHandle) || !replacementHandle ||
            !content::replaceTextureAssetValue(original, replacement, nullptr)) {
            if (replacement) content::destroyGameTexture(replacement);
            for (auto handle : {assetHandle, valueHandle, sourceHandle, replacementHandle}) if (handle) a.gchandle_free(handle);
            candidate.failed = true;
            BL_WARN("Texture pack %s: could not apply %s; keeping original/lower priority asset", candidate.uid.c_str(), candidate.name.c_str());
            continue;
        }
        if (dimensions && (ow != w || oh != h))
            BL_WARN("Texture pack %s: %s size mismatch: PNG %dx%d, original %dx%d; sprite frames may break", candidate.uid.c_str(), candidate.name.c_str(), w, h, ow, oh);
        else if (!dimensions)
            BL_DEBUG("Texture pack %s: original dimensions unavailable for %s; PNG size was not verified", candidate.uid.c_str(), candidate.name.c_str());
        g_namedSaved.emplace(original, NamedSaved{assetHandle, valueHandle, sourceHandle, replacementHandle, state});
        BL_DEBUG("Texture pack %s: applied %s -> %s", candidate.uid.c_str(), candidate.name.c_str(), name.c_str());
        g_bypass = bypass;
        return true;
    }
    g_bypass = bypass;
    return false;
}

void loadHook(Il2CppObject* asset, const MethodInfo* method) {
    if (!g_bypass && std::this_thread::get_id() == g_gameThread && applyNamed(asset, g_tableSet.count(asset) != 0)) return;
    g_load(asset, method);
}

void sweep() {
    if (g_tableAssets.empty() || ++g_sweep < 30) return;
    g_sweep = 0;
    auto& a = il2cpp::api();
    for (Il2CppObject* asset : g_tableAssets) {
        Il2CppObject* value = content::textureAssetValue(asset);
        if (!value) continue;
        auto saved = g_namedSaved.find(asset);
        if (saved == g_namedSaved.end() || a.gchandle_get_target(saved->second.replacement) != value) applyNamed(asset);
    }
}

bool restore() {
    auto& a = il2cpp::api();
    for (auto it = g_namedSaved.begin(); it != g_namedSaved.end();) {
        auto& saved = it->second;
        if (!content::replaceTextureAssetValue(a.gchandle_get_target(saved.asset), saved.value ? a.gchandle_get_target(saved.value) : nullptr,
            saved.source ? a.gchandle_get_target(saved.source) : nullptr, saved.state)) {
            BL_ERROR("Texture packs: could not restore named asset");
            ++it;
            continue;
        }
        content::destroyGameTexture(a.gchandle_get_target(saved.replacement));
        for (auto handle : {saved.asset, saved.value, saved.source, saved.replacement}) if (handle) a.gchandle_free(handle);
        it = g_namedSaved.erase(it);
    }
    return g_namedSaved.empty();
}
} // namespace

void configureTexturePacks(const std::string& root, const std::vector<ModSpec>& enabled) {
    std::lock_guard<std::mutex> lock(g_mutex);
    g_pending.clear();
    for (const auto& spec : enabled) {
        if (spec.entry != "@texture") continue;
        g_pending.push_back({spec.id, root + "/" + spec.id + "/Images"});
    }
    g_dirty.store(true, std::memory_order_release);
}

void tickTexturePacks() {
    if (!g_dirty.load(std::memory_order_acquire)) {
        sweep();
        return;
    }
    if (!contentSettled()) return;
    std::vector<Pack> packs;
    {
        std::lock_guard<std::mutex> lock(g_mutex);
        packs = g_pending;
        g_dirty.store(false, std::memory_order_release);
    }
    g_named.clear();
    g_tableAssets.clear();
    g_tableSet.clear();
    if (!restore() || packs.empty()) return;
    auto& a = il2cpp::api();
    Il2CppClass* cls = il2cpp::findClass({"Terraria.GameContent", "TextureAssets", {}});
    if (!cls || !a.gchandle_new || !a.gchandle_get_target || !a.gchandle_free) {
        BL_ERROR("Texture packs: required TextureAssets/GC APIs are unavailable");
        return;
    }
    auto* assetClass = content::textureAssetClass();
    if (!g_hooked && assetClass) {
        g_gameThread = std::this_thread::get_id();
        auto* method = a.class_get_method_from_name(assetClass, "ActionUnityLoad", 0);
        g_hooked = method && hook::install(method, loadHook, &g_load);
        if (!g_hooked) BL_WARN("Texture packs: asset load hook is unavailable; only textures already loaded can be replaced");
    }
    std::map<std::string, FieldInfo*> tables;
    for (const auto& pack : packs) {
        size_t count = 0;
        for (const auto& file : pngs(pack.images)) {
            const auto target = textures::targetFor(file);
            std::string name = "Images/" + file.substr(0, file.size() - 4);
            if (target.index >= 0) {
                auto [table, inserted] = tables.try_emplace(target.table, nullptr);
                if (inserted) table->second = il2cpp::findField(cls, target.table.c_str());
                FieldInfo* field = table->second;
                Il2CppArray* array = nullptr;
                if (field) a.field_static_get_value(field, &array);
                if (!array || static_cast<uintptr_t>(target.index) >= array->length) {
                    BL_WARN("Texture pack %s: unsupported index for %s", pack.uid.c_str(), file.c_str());
                    continue;
                }
                Il2CppObject* original = static_cast<Il2CppObject**>(arrayData(array))[target.index];
                const std::string assetName = original ? content::textureAssetName(original) : std::string();
                if (assetName.empty()) {
                    BL_WARN("Texture pack %s: no original asset for %s", pack.uid.c_str(), file.c_str());
                    continue;
                }
                name = assetName;
                if (g_tableSet.insert(original).second) g_tableAssets.push_back(original);
            }
            g_named[canonical(name)].push_back({pack.uid, pack.images + "/" + file, file});
            ++count;
        }
        BL_INFO("Texture pack %s: %zu texture(s)", pack.uid.c_str(), count);
    }
    for (auto* asset : content::textureAssets()) {
        if (content::textureAssetValue(asset)) applyNamed(asset);
    }
    const size_t now = g_namedSaved.size();
    BL_INFO("Texture packs: %zu path(s), %zu applied now, the rest when the game loads them", g_named.size(), now);
}
} // namespace bl::runtime
