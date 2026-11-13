#include "content/textures/TexturePacks.h"
#include "content/textures/TexturePackNames.h"
#include "content/common/ContentAssets.h"
#include "core/Log.h"
#include "il2cpp/Api.h"
#include "il2cpp/Resolver.h"
#include "hook/HookManager.h"

#include <algorithm>
#include <atomic>
#include <chrono>
#include <cstring>
#include <dirent.h>
#include <map>
#include <mutex>
#include <unordered_set>
#include <sys/prctl.h>
#include <sys/stat.h>

namespace bl::runtime {
namespace {
struct Pack {
    std::string uid, images;
    bool operator==(const Pack& o) const { return uid == o.uid && images == o.images; }
};
struct Candidate { std::string uid, path, name; bool failed = false; };
struct NamedSaved { uint32_t asset, value, source, replacement; int state; };
std::mutex g_mutex;
std::vector<Pack> g_pending;
std::vector<Pack> g_active;
std::atomic<bool> g_dirty{false};
bool g_finished = false;
std::vector<Il2CppObject*> g_tableAssets;
std::unordered_set<Il2CppObject*> g_tableSet;
int g_sweep = 0;
std::vector<Il2CppObject*> g_queue;
size_t g_queueAt = 0;
size_t g_hookApplied = 0;
std::chrono::steady_clock::time_point g_queueStart;
constexpr auto kFrameBudget = std::chrono::milliseconds(5);
constexpr auto kSplashBudget = std::chrono::milliseconds(40);
FieldInfo* g_asyncLoaded = nullptr;
FieldInfo* g_showSplash = nullptr;
std::map<std::string, std::vector<Candidate>> g_named;
std::map<Il2CppObject*, NamedSaved> g_namedSaved;
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

std::string pathName(const std::string& file) { return "Images/" + file.substr(0, file.size() - 4); }

bool onUnityMain() {
    char name[16] = {};
    prctl(PR_GET_NAME, name, 0, 0, 0);
    return std::strcmp(name, "UnityMain") == 0;
}

bool itemTableReady() {
    static FieldInfo* field = nullptr;
    if (!field) {
        Il2CppClass* cls = il2cpp::findClass({"Terraria.GameContent", "TextureAssets", {}});
        field = cls ? il2cpp::findField(cls, "Item") : nullptr;
        if (!field) return false;
    }
    Il2CppArray* items = nullptr;
    il2cpp::api().field_static_get_value(field, &items);
    return items && items->length > 1 && static_cast<Il2CppObject**>(arrayData(items))[1];
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
    if (!g_bypass && !g_named.empty() && onUnityMain() && itemTableReady() && applyNamed(asset)) {
        ++g_hookApplied;
        return;
    }
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

bool staticFlag(FieldInfo*& field, const char* name) {
    if (!field) {
        Il2CppClass* main = il2cpp::findClass({"Terraria", "Main", {}});
        field = main ? il2cpp::findField(main, name) : nullptr;
        if (!field) return false;
    }
    bool value = false;
    il2cpp::api().field_static_get_value(field, &value);
    return value;
}

void drainQueue() {
    const auto start = std::chrono::steady_clock::now();
    const auto budget = staticFlag(g_showSplash, "showSplash") ? kSplashBudget : kFrameBudget;
    while (g_queueAt < g_queue.size()) {
        Il2CppObject* asset = g_queue[g_queueAt++];
        if (content::textureAssetValue(asset)) applyNamed(asset);
        if (std::chrono::steady_clock::now() - start >= budget) return;
    }
    const auto ms = std::chrono::duration_cast<std::chrono::milliseconds>(std::chrono::steady_clock::now() - g_queueStart).count();
    BL_INFO("Texture packs: %zu replaced (%zu while the game loaded them, %zu after, in %lld ms)", g_namedSaved.size(), g_hookApplied,
            g_queue.size(), static_cast<long long>(ms));
    g_queue.clear();
    g_queue.shrink_to_fit();
    g_queueAt = 0;
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

std::vector<Pack> packsOf(const std::string& root, const std::vector<ModSpec>& enabled) {
    std::vector<Pack> packs;
    for (const auto& spec : enabled) {
        if (spec.entry == "@texture") packs.push_back({spec.id, root + "/" + spec.id + "/Images"});
    }
    return packs;
}

void index(const std::vector<Pack>& packs) {
    g_named.clear();
    for (const auto& pack : packs) {
        size_t count = 0;
        for (const auto& file : pngs(pack.images)) {
            g_named[canonical(pathName(file))].push_back({pack.uid, pack.images + "/" + file, file});
            ++count;
        }
        BL_INFO("Texture pack %s: %zu texture(s)", pack.uid.c_str(), count);
    }
}

void installLoadHook() {
    if (g_hooked) return;
    auto& a = il2cpp::api();
    Il2CppClass* textures = il2cpp::findClass({"Terraria.GameContent", "TextureAssets", {}});
    FieldInfo* field = textures ? il2cpp::findField(textures, "Item") : nullptr;
    Il2CppClass* arrayClass = field ? a.class_from_il2cpp_type(a.field_get_type(field)) : nullptr;
    Il2CppClass* assetClass = arrayClass ? a.class_get_element_class(arrayClass) : nullptr;
    const MethodInfo* method = assetClass ? a.class_get_method_from_name(assetClass, "ActionUnityLoad", 0) : nullptr;
    g_hooked = method && hook::install(method, loadHook, &g_load);
    if (!g_hooked) BL_WARN("Texture packs: asset load hook is unavailable; only textures already loaded can be replaced");
}

void finish() {
    auto& a = il2cpp::api();
    Il2CppClass* cls = il2cpp::findClass({"Terraria.GameContent", "TextureAssets", {}});
    std::map<std::string, FieldInfo*> tables;
    std::map<std::string, std::vector<Candidate>> aliases;
    for (const auto& [key, candidates] : g_named) {
        const std::string& file = candidates.front().name;
        const auto target = textures::targetFor(file);
        if (target.index < 0 || !cls) continue;
        auto [table, inserted] = tables.try_emplace(target.table, nullptr);
        if (inserted) table->second = il2cpp::findField(cls, target.table.c_str());
        Il2CppArray* array = nullptr;
        if (table->second) a.field_static_get_value(table->second, &array);
        if (!array || static_cast<uintptr_t>(target.index) >= array->length) {
            BL_WARN("Texture pack %s: unsupported index for %s", candidates.front().uid.c_str(), file.c_str());
            continue;
        }
        Il2CppObject* original = static_cast<Il2CppObject**>(arrayData(array))[target.index];
        const std::string assetName = original ? canonical(content::textureAssetName(original)) : std::string();
        if (assetName.empty()) continue;
        if (assetName != key && !g_named.count(assetName)) aliases[assetName] = candidates;
        if (g_tableSet.insert(original).second) g_tableAssets.push_back(original);
    }
    for (auto& [key, candidates] : aliases) g_named.emplace(key, std::move(candidates));
    std::unordered_set<Il2CppObject*> queued;
    for (auto* asset : content::textureAssets()) {
        if (!content::textureAssetValue(asset) || g_namedSaved.count(asset) || !queued.insert(asset).second) continue;
        if (g_named.count(canonical(content::textureAssetName(asset)))) g_queue.push_back(asset);
    }
    g_queueAt = 0;
    g_queueStart = std::chrono::steady_clock::now();
    g_finished = true;
    drainQueue();
}
}

void prepareTexturePacks(const std::string& root, const std::vector<ModSpec>& enabled) {
    std::lock_guard<std::mutex> lock(g_mutex);
    g_active = packsOf(root, enabled);
    g_pending = g_active;
    if (g_active.empty()) return;
    index(g_active);
    installLoadHook();
}

void configureTexturePacks(const std::string& root, const std::vector<ModSpec>& enabled) {
    std::lock_guard<std::mutex> lock(g_mutex);
    std::vector<Pack> packs = packsOf(root, enabled);
    if (packs == g_active && (g_hooked || packs.empty())) return;
    g_pending = std::move(packs);
    g_dirty.store(true, std::memory_order_release);
}

bool texturePacksBusy() {
    return g_dirty.load(std::memory_order_acquire) || !g_queue.empty() || (!g_finished && !g_active.empty());
}

void tickTexturePacks() {
    if (g_dirty.load(std::memory_order_acquire)) {
        if (!staticFlag(g_asyncLoaded, "_isAsyncLoadComplete") || !itemTableReady()) return;
        std::vector<Pack> packs;
        {
            std::lock_guard<std::mutex> lock(g_mutex);
            packs = g_pending;
            g_dirty.store(false, std::memory_order_release);
        }
        g_tableAssets.clear();
        g_tableSet.clear();
        g_queue.clear();
        g_queueAt = 0;
        g_finished = false;
        g_named.clear();
        if (!restore()) return;
        g_active = packs;
        if (packs.empty()) return;
        index(packs);
        installLoadHook();
    }
    if (g_active.empty()) return;
    if (!g_finished) {
        if (staticFlag(g_asyncLoaded, "_isAsyncLoadComplete") && itemTableReady()) finish();
        return;
    }
    if (!g_queue.empty()) drainQueue();
    else sweep();
}
}
