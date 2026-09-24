#include "runtime/ModProjectiles.h"
#include "core/Log.h"
#include "hook/HookManager.h"
#include "il2cpp/Api.h"
#include "il2cpp/Resolver.h"
#include "runtime/ContentAssets.h"
#include "runtime/GameRefs.h"
#include "runtime/TypeTables.h"

#include <atomic>
#include <mutex>

namespace bl::runtime {

namespace {

struct Entry {
    ModProjectileDef def;
    int width = 0, height = 0;   // de UM quadro
    int textureHeight = 0;       // a tira inteira
    uint32_t asset = 0;          // gchandle do Asset<Texture2D>: reaplicado se a tabela for refeita
};

std::mutex g_mx;
std::vector<Entry> g_regs;               // indice = type - kVanillaProjectileCount
std::atomic<ProjectilesInstalledHook> g_installedHook{nullptr};
std::atomic<int> g_total{0};
std::atomic<int> g_installed{0};
bool g_failed = false;

// Conferida contra toda alocacao de 1111 posicoes na libil2cpp (`mov #0x457`
// antes de um new[]): TextureAssets, ProjectileID.Sets, Lang e Main tem as
// estaticas; a de Player e por instancia (abaixo).
TypeTables g_tables("projeteis de mod", kVanillaProjectileCount, {
    {"Terraria.ID", "ProjectileID", "Sets"},
    {"Terraria", "Main", ""},
    {"Terraria", "Lang", ""},
    {"Terraria", "Projectile", ""},
    {"Terraria.GameContent", "TextureAssets", ""},
});

struct Refs {
    bool tried = false, ok = false;
    FieldInfo* textures = nullptr;     // TextureAssets.Projectile
    FieldInfo* names = nullptr;        // Lang._projectileNameCache
    FieldInfo* frames = nullptr;       // Main.projFrames
    FieldInfo* players = nullptr;      // Main.player
    int32_t ownedCounts = -1;          // Player.ownedProjectileCounts
    int32_t width = -1, height = -1;   // Entity
    int32_t active = -1;               // Projectile (declarado nele, nao em Entity)
    const MethodInfo* playerCtor = nullptr;
};

Refs& refs() {
    static Refs r;
    if (r.tried) return r;
    r.tried = true;
    using namespace il2cpp;
    Il2CppClass* textures = findClass({"Terraria.GameContent", "TextureAssets", {}});
    Il2CppClass* lang = findClass({"Terraria", "Lang", {}});
    Il2CppClass* main = findClass({"Terraria", "Main", {}});
    Il2CppClass* player = findClass({"Terraria", "Player", {}});
    Il2CppClass* projectile = findClass({"Terraria", "Projectile", {}});
    r.textures = textures ? findField(textures, "Projectile") : nullptr;
    r.names = lang ? findField(lang, "_projectileNameCache") : nullptr;
    r.frames = main ? findField(main, "projFrames") : nullptr;
    r.players = main ? findField(main, "player") : nullptr;
    r.ownedCounts = player ? fieldOffset(player, "ownedProjectileCounts") : -1;
    r.width = projectile ? fieldOffset(projectile, "width") : -1;
    r.height = projectile ? fieldOffset(projectile, "height") : -1;
    r.active = projectile ? fieldOffset(projectile, "active") : -1;
    r.playerCtor = player ? api().class_get_method_from_name(player, ".ctor", 0) : nullptr;
    r.ok = r.textures && r.names && r.frames && r.players && r.ownedCounts >= 0 && r.width >= 0 &&
           r.height >= 0 && r.active >= 0 && r.playerCtor;
    if (!r.ok) {
        BL_ERROR("projeteis de mod: refs faltando (TextureAssets.Projectile=%p "
                 "Lang._projectileNameCache=%p Main.projFrames=%p Player.ownedProjectileCounts=%d)",
                 (void*)r.textures, (void*)r.names, (void*)r.frames, r.ownedCounts);
    }
    return r;
}

Il2CppArray* readStatic(FieldInfo* f) {
    Il2CppArray* arr = nullptr;
    il2cpp::api().field_static_get_value(f, &arr);
    return arr;
}

void applyName(int type, const Entry& e) {
    const std::string text = content::textForCulture(e.def.names, e.def.name);
    if (Il2CppObject* t = content::makeLocalizedText("ProjectileName." + e.def.name, text)) {
        content::setTableElement(refs().names, type, t);
    }
}

void applyTexture(int type, const Entry& e) {
    Il2CppObject* asset = e.asset ? il2cpp::api().gchandle_get_target(e.asset) : nullptr;
    if (asset) content::setTableElement(refs().textures, type, asset);
}

void applyFrames(int type, const Entry& e) {
    Il2CppArray* frames = readStatic(refs().frames);
    if (frames && static_cast<uintptr_t>(type) < frames->length) {
        static_cast<int32_t*>(arrayData(frames))[type] = e.def.frames > 0 ? e.def.frames : 1;
    }
}

/** Tabela que o jogo refez (a troca de idioma refaz os nomes): reaplica o nosso. */
void onTableRegrown(FieldInfo* f, int size) {
    const Refs& r = refs();
    std::lock_guard<std::mutex> l(g_mx);
    for (size_t i = 0; i < g_regs.size() && static_cast<int>(i) < size - kVanillaProjectileCount; ++i) {
        const int type = kVanillaProjectileCount + static_cast<int>(i);
        if (f == r.names) applyName(type, g_regs[i]);
        if (f == r.textures) applyTexture(type, g_regs[i]);
        if (f == r.frames) applyFrames(type, g_regs[i]);
    }
}

bool gameReady(int size) {
    const Refs& r = refs();
    if (!r.ok) return false;
    Il2CppArray* textures = readStatic(r.textures);
    Il2CppArray* names = readStatic(r.names);
    return textures && names && readStatic(r.frames) &&
           textures->length == static_cast<uintptr_t>(size) && names->length >= static_cast<uintptr_t>(size);
}

// ---- Player.ownedProjectileCounts ----
//
// int[ProjectileID.Count] em CADA jogador, escrito todo quadro pelo tipo de
// cada projetil vivo dele: sem aumentar, um projetil de mod escreve alem do fim
// do array. Os 256 de Main.player ja existem na instalacao; os que nascerem
// depois (carregar personagem, clone para a rede) passam pelo construtor.

using PlayerCtorFn = void (*)(Il2CppObject*, const MethodInfo*);
PlayerCtorFn g_origPlayerCtor = nullptr;

void hkPlayerCtor(Il2CppObject* self, const MethodInfo* m) {
    g_origPlayerCtor(self, m);
    if (projectileTypeCount() > kVanillaProjectileCount) {
        TypeTables::growInstanceTable(self, refs().ownedCounts, kVanillaProjectileCount,
                                      projectileTypeCount(), nullptr);
    }
}

void growPlayers(int size) {
    static bool hooked = false;
    const Refs& r = refs();
    if (!hooked) {
        hooked = true;
        if (!hook::install(r.playerCtor, hkPlayerCtor, &g_origPlayerCtor)) {
            BL_ERROR("projeteis de mod: sem hook no construtor de Player; jogador novo escreve "
                     "fora de ownedProjectileCounts");
        }
    }
    Il2CppArray* players = readStatic(r.players);
    int grown = 0;
    for (uintptr_t i = 0; players && i < players->length; ++i) {
        Il2CppObject* p = static_cast<Il2CppObject**>(arrayData(players))[i];
        if (!p) continue;
        TypeTables::growInstanceTable(p, r.ownedCounts, kVanillaProjectileCount, size, nullptr);
        ++grown;
    }
    BL_INFO("projeteis de mod: ownedProjectileCounts aumentada em %d jogador(es)", grown);
}

} // namespace

int registerModProjectile(ModProjectileDef def) {
    std::lock_guard<std::mutex> l(g_mx);
    for (const Entry& e : g_regs) {
        if (e.def.mod == def.mod && e.def.name == def.name) return -1;
    }
    Entry e;
    e.def = std::move(def);
    g_regs.push_back(std::move(e));
    g_total.store(static_cast<int>(g_regs.size()), std::memory_order_release);
    return kVanillaProjectileCount + static_cast<int>(g_regs.size()) - 1;
}

bool isModProjectile(int type) {
    return type >= kVanillaProjectileCount &&
           type < kVanillaProjectileCount + g_total.load(std::memory_order_acquire);
}

int projectileTypeCount() {
    return kVanillaProjectileCount + g_installed.load(std::memory_order_acquire);
}

void tickModProjectiles() {
    const int total = g_total.load(std::memory_order_acquire);
    if (total == 0 || g_failed) return;
    const int installed = g_installed.load(std::memory_order_relaxed);
    const int from = kVanillaProjectileCount + installed;

    if (installed == total) {
        g_tables.checkPending(from);
        static int frame = 0;
        if (++frame % 120 == 0) g_tables.watch(from, onTableRegrown);
        else {
            Il2CppArray* names = readStatic(refs().names);
            if (names && names->length < static_cast<uintptr_t>(from)) g_tables.watch(from, onTableRegrown);
        }
        return;
    }
    if (!gameReady(from)) return;

    const int to = kVanillaProjectileCount + total;
    const int grown = g_tables.grow(from, to);
    if (grown < 0) {
        BL_ERROR("projeteis de mod: nenhuma tabela de projetil achada; projeteis de mod desligados");
        g_failed = true;
        return;
    }
    g_installed.store(total, std::memory_order_release);
    growPlayers(to);

    {
        std::lock_guard<std::mutex> l(g_mx);
        for (int i = installed; i < total; ++i) {
            Entry& e = g_regs[static_cast<size_t>(i)];
            const int type = kVanillaProjectileCount + i;
            int w = 0, h = 0;
            Il2CppObject* asset = content::loadTextureAsset(e.def.texture, nullptr, 0,
                                                            e.def.mod + "/" + e.def.name, &w, &h);
            if (asset) e.asset = il2cpp::api().gchandle_new(asset, false);
            e.width = w;
            e.textureHeight = h;
            e.height = e.def.frames > 1 ? h / e.def.frames : h;
            applyTexture(type, e);
            applyName(type, e);
            applyFrames(type, e);
        }
    }
    BL_INFO("projeteis de mod: %d instalado(s) (ids %d..%d), %d tabela(s) aumentadas de %d para %d",
            total - installed, from, to - 1, grown, from, to);
    // FORA da trava: o SetStaticDefaults entra no JS do mod, que pode pedir
    // setModProjectileFrames.
    if (ProjectilesInstalledHook hook = g_installedHook.load(std::memory_order_acquire)) {
        hook(from, to - 1);
    }
}

bool modProjectilesSettled() {
    return g_failed || g_installed.load(std::memory_order_relaxed) == g_total.load(std::memory_order_acquire);
}

void setProjectilesInstalledHook(ProjectilesInstalledHook hook) {
    g_installedHook.store(hook, std::memory_order_release);
}

void setModProjectileFrames(int type, int frames) {
    if (frames < 1) return;
    std::lock_guard<std::mutex> l(g_mx);
    const size_t i = static_cast<size_t>(type - kVanillaProjectileCount);
    if (type < kVanillaProjectileCount || i >= g_regs.size()) return;
    Entry& e = g_regs[i];
    e.def.frames = frames;
    if (e.textureHeight > 0) e.height = e.textureHeight / frames;
    if (static_cast<int>(i) < g_installed.load(std::memory_order_relaxed)) applyFrames(type, e);
}

int modProjectileTypeByName(const std::string& mod, const std::string& name) {
    std::lock_guard<std::mutex> l(g_mx);
    for (size_t i = 0; i < g_regs.size(); ++i) {
        if (g_regs[i].def.mod == mod && g_regs[i].def.name == name) {
            return kVanillaProjectileCount + static_cast<int>(i);
        }
    }
    return -1;
}

void finishModProjectile(Il2CppObject* projectile, int type) {
    const Refs& r = refs();
    if (!r.ok || !projectile) return;
    int w = 0, h = 0;
    {
        std::lock_guard<std::mutex> l(g_mx);
        const size_t i = static_cast<size_t>(type - kVanillaProjectileCount);
        if (i < g_regs.size()) { w = g_regs[i].width; h = g_regs[i].height; }
    }
    // O SetDefaults do jogo termina todo tipo que nao conhece em `active =
    // false` (o `else` do switch, visto na disassembly), e o NewProjectile
    // devolvia um projetil morto. O resto do final dele roda normalmente.
    field<uint8_t>(projectile, r.active) = 1;
    if (field<int32_t>(projectile, r.width) <= 0 && w > 0) field<int32_t>(projectile, r.width) = w;
    if (field<int32_t>(projectile, r.height) <= 0 && h > 0) field<int32_t>(projectile, r.height) = h;
}

} // namespace bl::runtime
