#include "content/walls/ModWalls.h"
#include "core/Log.h"
#include "il2cpp/Api.h"
#include "il2cpp/Resolver.h"
#include "content/common/ContentAssets.h"
#include "content/common/GameRefs.h"
#include "content/common/TypeTables.h"
#include "content/tiles/ModTileMap.h"
#include "content/tiles/ModTiles.h"

#include <atomic>
#include <cstring>
#include <mutex>

namespace bl::runtime {

namespace {

struct Entry {
    ModWallDef def;
    uint32_t asset = 0;
};

std::mutex g_mx;
std::vector<Entry> g_regs;
std::atomic<WallsInstalledHook> g_installedHook{nullptr};
std::atomic<int> g_total{0};
std::atomic<int> g_installed{0};
bool g_failed = false;

TypeTables g_tables("paredes de mod", kVanillaWallCount, {
    {"Terraria", "Main", ""},
    {"Terraria.ID", "WallID", "Sets"},
    {"Terraria.ID", "WallID", "Sets", "Conversion"},
    {"Terraria.GameContent", "TextureAssets", ""},
    {"Terraria.Map", "MapHelper", ""},
});

struct Refs {
    bool tried = false, ok = false;
    FieldInfo* textures = nullptr;
    FieldInfo* house = nullptr;
    FieldInfo* blend = nullptr;
    FieldInfo* count = nullptr;
};

Refs& refs() {
    static Refs r;
    if (r.tried) return r;
    r.tried = true;
    using namespace il2cpp;
    Il2CppClass* textures = findClass({"Terraria.GameContent", "TextureAssets", {}});
    Il2CppClass* main = findClass({"Terraria", "Main", {}});
    Il2CppClass* wallId = findClass({"Terraria.ID", "WallID", {}});
    r.textures = textures ? findField(textures, "Wall") : nullptr;
    r.house = main ? findField(main, "wallHouse") : nullptr;
    r.blend = main ? findField(main, "wallBlend") : nullptr;
    r.count = wallId ? findField(wallId, "Count") : nullptr;
    r.ok = r.textures && r.house && r.blend && r.count;
    if (!r.ok) {
        BL_ERROR("paredes de mod: refs faltando (TextureAssets.Wall=%p Main.wallHouse=%p Main.wallBlend=%p "
                 "WallID.Count=%p)", (void*)r.textures, (void*)r.house, (void*)r.blend, (void*)r.count);
    }
    return r;
}

Il2CppArray* readStatic(FieldInfo* f) {
    Il2CppArray* arr = nullptr;
    il2cpp::api().field_static_get_value(f, &arr);
    return arr;
}

int readWallCount() {
    uint16_t n = 0;
    il2cpp::api().field_static_get_value(refs().count, &n);
    return n;
}

void writeWallCount(int total) {
    const uint16_t n = static_cast<uint16_t>(total);
    il2cpp::api().field_static_set_value(refs().count, const_cast<uint16_t*>(&n));
}

void applyTexture(int type, const Entry& e) {
    Il2CppObject* asset = e.asset ? il2cpp::api().gchandle_get_target(e.asset) : nullptr;
    if (asset) content::setTableElement(refs().textures, type, asset);
}

void applyBlend(int from, int to) {
    Il2CppArray* blend = readStatic(refs().blend);
    if (!blend || blend->length < static_cast<uintptr_t>(to)) return;
    auto* v = static_cast<int32_t*>(arrayData(blend));
    for (int t = from; t < to; ++t) {
        if (v[t] == 0) v[t] = t;
    }
}

void onTableRegrown(FieldInfo* f, int size) {
    const Refs& r = refs();
    if (f == r.blend) applyBlend(kVanillaWallCount, size);
    if (f != r.textures) return;
    std::lock_guard<std::mutex> l(g_mx);
    for (size_t i = 0; i < g_regs.size() && static_cast<int>(i) < size - kVanillaWallCount; ++i) {
        applyTexture(kVanillaWallCount + static_cast<int>(i), g_regs[i]);
    }
}

bool gameReady(int size) {
    const Refs& r = refs();
    if (!r.ok) return false;
    Il2CppArray* textures = readStatic(r.textures);
    Il2CppArray* house = readStatic(r.house);
    return textures && house && textures->length == static_cast<uintptr_t>(size) &&
           house->length == static_cast<uintptr_t>(size);
}

} // namespace

int registerModWall(ModWallDef def) {
    std::lock_guard<std::mutex> l(g_mx);
    for (const Entry& e : g_regs) {
        if (e.def.mod == def.mod && e.def.name == def.name) return -1;
    }
    Entry e;
    e.def = std::move(def);
    g_regs.push_back(std::move(e));
    g_total.store(static_cast<int>(g_regs.size()), std::memory_order_release);
    return kVanillaWallCount + static_cast<int>(g_regs.size()) - 1;
}

bool isModWall(int type) {
    return type >= kVanillaWallCount && type < kVanillaWallCount + g_total.load(std::memory_order_acquire);
}

int wallTypeCount() {
    return kVanillaWallCount + g_installed.load(std::memory_order_acquire);
}

void tickModWalls() {
    const int total = g_total.load(std::memory_order_acquire);
    if (total == 0 || g_failed) return;
    const int installed = g_installed.load(std::memory_order_relaxed);
    const int from = kVanillaWallCount + installed;

    if (installed == total) {
        g_tables.checkPending(from);
        applyModTileMap(tileTypeCount(), false);
        static int frame = 0;
        if (++frame % 120 == 0) g_tables.watch(from, onTableRegrown);
        return;
    }
    if (!gameReady(from)) return;
    if (installed > 0) {
        BL_ERROR("paredes de mod: %d registrada(s) depois da instalacao; ficam sem tabela", total - installed);
        g_failed = true;
        return;
    }
    const int vanilla = readWallCount();
    if (vanilla != kVanillaWallCount) {
        BL_ERROR("paredes de mod: WallID.Count do jogo e %d, esperado %d; paredes de mod desligadas",
                 vanilla, kVanillaWallCount);
        g_failed = true;
        return;
    }

    const int to = kVanillaWallCount + total;
    const int grown = g_tables.grow(from, to);
    if (grown < 0) {
        BL_ERROR("paredes de mod: nenhuma tabela de parede achada; paredes de mod desligadas");
        g_failed = true;
        return;
    }
    writeWallCount(to);
    installModTileMap();
    g_installed.store(total, std::memory_order_release);
    applyBlend(from, to);

    {
        std::lock_guard<std::mutex> l(g_mx);
        for (int i = installed; i < total; ++i) {
            Entry& e = g_regs[static_cast<size_t>(i)];
            const int type = kVanillaWallCount + i;
            int w = 0, h = 0;
            Il2CppObject* asset = content::loadTextureAsset(e.def.texture, nullptr, 0,
                                                            e.def.mod + "/" + e.def.name, &w, &h);
            if (asset) e.asset = il2cpp::api().gchandle_new(asset, false);
            else BL_ERROR("paredes de mod: %s/%s sem textura (%s)", e.def.mod.c_str(), e.def.name.c_str(),
                          e.def.texture.c_str());
            applyTexture(type, e);
        }
    }
    BL_INFO("paredes de mod: %d instalada(s) (ids %d..%d), %d tabela(s) aumentadas de %d para %d; "
            "WallID.Count = %d", total - installed, from, to - 1, grown, from, to, readWallCount());
    if (WallsInstalledHook hook = g_installedHook.load(std::memory_order_acquire)) {
        hook(from, to - 1);
    }
}

bool modWallsSettled() {
    return g_failed || g_installed.load(std::memory_order_relaxed) == g_total.load(std::memory_order_acquire);
}

void setWallsInstalledHook(WallsInstalledHook hook) {
    g_installedHook.store(hook, std::memory_order_release);
}

int modWallTypeByName(const std::string& mod, const std::string& name) {
    std::lock_guard<std::mutex> l(g_mx);
    for (size_t i = 0; i < g_regs.size(); ++i) {
        if (g_regs[i].def.mod == mod && g_regs[i].def.name == name) {
            return kVanillaWallCount + static_cast<int>(i);
        }
    }
    return -1;
}

std::string modWallKey(int type) {
    std::lock_guard<std::mutex> l(g_mx);
    const size_t i = static_cast<size_t>(type - kVanillaWallCount);
    if (type < kVanillaWallCount || i >= g_regs.size()) return {};
    return g_regs[i].def.mod + "/" + g_regs[i].def.name;
}

int modWallTypeByKey(const std::string& key) {
    const size_t slash = key.find('/');
    if (slash == std::string::npos) return -1;
    return modWallTypeByName(key.substr(0, slash), key.substr(slash + 1));
}

std::vector<ModWallInfo> modWalls() {
    std::lock_guard<std::mutex> l(g_mx);
    std::vector<ModWallInfo> out;
    for (size_t i = 0; i < g_regs.size(); ++i) {
        const ModWallDef& d = g_regs[i].def;
        out.push_back({kVanillaWallCount + static_cast<int>(i), d.mod, d.name, d.texture});
    }
    return out;
}

} // namespace bl::runtime
