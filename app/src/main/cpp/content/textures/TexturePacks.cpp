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
#include <set>
#include <sys/stat.h>
#include <thread>

namespace bl::runtime {
namespace {
struct Pack { std::string uid, images; };
struct Saved { FieldInfo* field; int index; uint32_t original; uint32_t replacement; };
struct Candidate { std::string uid, path, name; bool failed = false; };
struct NamedSaved { uint32_t asset, value, source, replacement; int state; };
std::mutex g_mutex;
std::vector<Pack> g_pending;
std::atomic<bool> g_dirty{false};
std::vector<Saved> g_saved; // So a thread do jogo acessa; handles seguram os assets no GC.
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
        return content::replaceTextureAssetValue(original, content::textureAssetValue(a.gchandle_get_target(saved->second.replacement)), nullptr);
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
        auto* replacement = content::loadTextureAsset(candidate.path, nullptr, 0, name, &w, &h);
        uint32_t replacementHandle = replacement ? a.gchandle_new(replacement, false) : 0;
        if (!assetHandle || (value && !valueHandle) || (source && !sourceHandle) || !replacementHandle ||
            !content::replaceTextureAssetValue(original, content::textureAssetValue(replacement), nullptr)) {
            if (replacement) content::destroyTextureAsset(replacement);
            for (auto handle : {assetHandle, valueHandle, sourceHandle, replacementHandle}) if (handle) a.gchandle_free(handle);
            candidate.failed = true;
            BL_WARN("Texture pack %s: could not apply %s; keeping original/lower priority asset", candidate.uid.c_str(), candidate.name.c_str());
            continue;
        }
        if (dimensions && (ow != w || oh != h))
            BL_WARN("Texture pack %s: %s size mismatch: PNG %dx%d, original %dx%d; sprite frames may break", candidate.uid.c_str(), candidate.name.c_str(), w, h, ow, oh);
        else if (!dimensions)
            BL_WARN("Texture pack %s: original dimensions unavailable for %s; PNG size was not verified", candidate.uid.c_str(), candidate.name.c_str());
        g_namedSaved.emplace(original, NamedSaved{assetHandle, valueHandle, sourceHandle, replacementHandle, state});
        BL_DEBUG("Texture pack %s: applied %s -> %s", candidate.uid.c_str(), candidate.name.c_str(), name.c_str());
        g_bypass = bypass;
        return true;
    }
    g_bypass = bypass;
    return false;
}

void loadHook(Il2CppObject* asset, const MethodInfo* method) {
    if (!g_bypass && std::this_thread::get_id() == g_gameThread && applyNamed(asset, false)) return;
    g_load(asset, method);
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
        content::destroyTextureAsset(a.gchandle_get_target(saved.replacement));
        for (auto handle : {saved.asset, saved.value, saved.source, saved.replacement}) if (handle) a.gchandle_free(handle);
        it = g_namedSaved.erase(it);
    }
    for (auto it = g_saved.begin(); it != g_saved.end();) {
        if (!content::setTableElement(it->field, it->index, a.gchandle_get_target(it->original))) {
            BL_ERROR("Texture packs: could not restore %s[%d]", a.field_get_name(it->field), it->index);
            ++it; // Retem os handles: a tabela ainda pode apontar para a substituicao.
            continue;
        }
        content::destroyTextureAsset(a.gchandle_get_target(it->replacement));
        a.gchandle_free(it->original);
        a.gchandle_free(it->replacement);
        it = g_saved.erase(it);
    }
    return g_saved.empty() && g_namedSaved.empty();
}
} // namespace

void configureTexturePacks(const std::string& root, const std::vector<ModSpec>& enabled) {
    std::lock_guard<std::mutex> lock(g_mutex);
    g_pending.clear();
    for (const auto& spec : enabled) {
        if (spec.entry != "@texture") continue;
        const std::string base = root + "/" + spec.id;
        const std::string content = directory(base + "/content") ? "/content" : "/Content";
        g_pending.push_back({spec.id, base + content + "/Images"});
    }
    g_dirty.store(true, std::memory_order_release);
}

void tickTexturePacks() {
    if (!g_dirty.load(std::memory_order_acquire) || !contentSettled()) return;
    std::vector<Pack> packs;
    {
        std::lock_guard<std::mutex> lock(g_mutex);
        packs = g_pending;
        g_dirty.store(false, std::memory_order_release);
    }
    g_named.clear();
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
        if (!g_hooked) BL_WARN("Texture packs: named asset load hook is unavailable; only existing assets can be replaced");
    }
    std::set<std::pair<std::string, int>> applied;
    std::map<std::string, FieldInfo*> tables;
    for (const auto& pack : packs) {
        size_t count = 0;
        for (const auto& file : pngs(pack.images)) {
            const auto target = textures::targetFor(file);
            if (target.index < 0) {
                const std::string name = "Images/" + file.substr(0, file.size() - 4);
                g_named[canonical(name)].push_back({pack.uid, pack.images + "/" + file, file});
                continue;
            }
            const auto key = std::make_pair(target.table, target.index);
            if (applied.count(key)) continue; // Primeiro na lista (o de cima) ganha.
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
            if (!original) {
                BL_WARN("Texture pack %s: no original asset for %s", pack.uid.c_str(), file.c_str());
                continue;
            }
            uint32_t saved = a.gchandle_new(original, false);
            if (!saved) continue;
            int ow = 0, oh = 0, w = 0, h = 0;
            const bool dimensions = content::textureAssetSize(original, &ow, &oh);
            Il2CppObject* asset = content::loadTextureAsset(pack.images + "/" + file, nullptr, 0,
                "Images/" + file.substr(0, file.size() - 4), &w, &h);
            uint32_t replacement = asset ? a.gchandle_new(asset, false) : 0;
            if (!asset || !replacement || !content::setTableElement(field, target.index, asset)) {
                if (asset) content::destroyTextureAsset(asset);
                if (replacement) a.gchandle_free(replacement);
                a.gchandle_free(saved);
                BL_WARN("Texture pack %s: could not apply %s; keeping original/lower priority asset",
                    pack.uid.c_str(), file.c_str());
                continue;
            }
            if (dimensions && (ow != w || oh != h)) {
                BL_WARN("Texture pack %s: %s size mismatch: PNG %dx%d, original %dx%d; sprite frames may break",
                    pack.uid.c_str(), file.c_str(), w, h, ow, oh);
            } else if (!dimensions) {
                BL_WARN("Texture pack %s: original dimensions unavailable for %s; PNG size was not verified",
                    pack.uid.c_str(), file.c_str());
            }
            g_saved.push_back({field, target.index, saved, replacement});
            applied.insert(key);
            ++count;
            BL_DEBUG("Texture pack %s: applied %s -> TextureAssets.%s[%d]", pack.uid.c_str(), file.c_str(), target.table.c_str(), target.index);
        }
        BL_INFO("Texture pack %s: applied %zu texture(s)", pack.uid.c_str(), count);
    }
    for (auto* asset : content::textureAssets()) applyNamed(asset);
    BL_INFO("Texture packs: applied %zu named asset(s), indexed %zu named path(s) for future loads", g_namedSaved.size(), g_named.size());
}
} // namespace bl::runtime
