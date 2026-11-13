#include <string>
#include <vector>
#include "boot/Boot.h"
#include "boot/FramePacing.h"
#include "boot/QuickStart.h"
#include "menu/Cheats.h"
#include "core/Config.h"
#include "core/Log.h"
#include "hook/HookManager.h"
#include "il2cpp/Api.h"
#include "il2cpp/Resolver.h"
#include "il2cpp/Signature.h"
#include "content/common/GameRefs.h"
#include "content/common/ModContent.h"
#include "content/common/ModLanguage.h"
#include "content/textures/FontPacks.h"
#include "content/textures/TexturePacks.h"
#include "content/items/ModItems.h"
#include "content/npcs/ModNpcs.h"
#include "content/buffs/ModBuffs.h"
#include "content/dusts/ModDusts.h"
#include "content/tiles/ModTiles.h"
#include "content/walls/ModWalls.h"
#include "menu/MenuCatalog.h"
#include "content/projectiles/ModProjectiles.h"
#include "menu/DevTools.h"
#include "menu/NetRequests.h"
#include "script/api/Console.h"
#include "menu/Powers.h"
#include <algorithm>
#include <atomic>
#include <unordered_map>
#include <chrono>
#include <cmath>
#include <cstdio>
#include <cstdlib>
#include <cstring>

namespace bl::runtime {

namespace {

struct Vector2 { float x; float y; };

std::atomic<uint64_t> g_pendingGive{0};

std::atomic<uint64_t> g_pendingSpawn{0};

std::atomic<uint64_t> g_pendingBuff{0};

uint64_t packGive(int type, int stack) {
    return (static_cast<uint64_t>(static_cast<uint32_t>(type)) << 32) |
           static_cast<uint32_t>(stack);
}

const std::string& cmdPath() { return config().cmdPath; }

FieldInfo* g_playerField = nullptr;
const MethodInfo* g_getMyPlayer = nullptr;
const MethodInfo* g_getGameMenu = nullptr;
const MethodInfo* g_newItem = nullptr;
const MethodInfo* g_newNpc = nullptr;
const MethodInfo* g_sourceCtor = nullptr;
FieldInfo* g_npcField = nullptr;
FieldInfo* g_maxTilesX = nullptr;
int32_t g_offDirection = -1;
Il2CppClass* g_itemType = nullptr;
const MethodInfo* g_itemCtor = nullptr;
const MethodInfo* g_setDefaults = nullptr;
int32_t g_offInventory = -1;
int32_t g_offItemType = -1, g_offItemStack = -1, g_offItemMaxStack = -1;
Il2CppClass* g_sourceClass = nullptr;
bool g_refsOk = false;

constexpr int kMsgSyncNpc = 23;
constexpr int kMsgSyncEquipment = 5;
constexpr int kMainInventory = 50;
constexpr int kMaxSpawn = 99;
constexpr int kMaxNpcs = 200;

constexpr float kSpawnAhead = 144.f;
constexpr float kSpawnLift = 80.f;
constexpr float kSpawnGap = 16.f;
constexpr float kSpawnRowLength = 640.f;
constexpr float kSpawnWorldMargin = 640.f;

int unboxInt(Il2CppObject* boxed) {
    if (!boxed) return 0;
    return *reinterpret_cast<int*>(reinterpret_cast<char*>(boxed) + sizeof(Il2CppObject));
}

bool resolveCheatRefs() {
    using namespace il2cpp;
    auto& a = api();
    Il2CppClass* main = findClass({"Terraria", "Main", {}});
    Il2CppClass* item = findClass({"Terraria", "Item", {}});
    if (!main || !item) { BL_ERROR("cheats: Main/Item not resolved"); return false; }

    g_playerField = a.class_get_field_from_name(main, "player");
    g_getMyPlayer = a.class_get_method_from_name(main, "get_myPlayer", 0);
    g_getGameMenu = a.class_get_method_from_name(main, "get_gameMenu", 0);
    if (!g_getGameMenu) BL_WARN("cheats: get_gameMenu not found; the button stays always visible");
    g_newItem = findMethodBySignature(item, parseSignature(
        "int NewItem(int X, int Y, int Width, int Height, int Type, int Stack, "
        "bool noBroadcast, int pfix, NewItemOwnership ownership)"));

    if (Il2CppClass* npc = findClass({"Terraria", "NPC", {}})) {
        g_newNpc = findMethodBySignature(npc, parseSignature(
            "int NewNPC(IEntitySource source, int X, int Y, int Type, int Start, "
            "float ai0, float ai1, float ai2, float ai3, int Target)"));
    }
    g_itemType = item;
    g_itemCtor = findMethodBySignature(item, parseSignature("void .ctor()"));
    g_setDefaults = findMethodBySignature(item, parseSignature("void SetDefaults(int Type, ItemVariant variant)"));
    g_offItemType = fieldOffset(item, "type");
    g_offItemStack = fieldOffset(item, "stack");
    g_offItemMaxStack = fieldOffset(item, "maxStack");
    if (Il2CppClass* player = findClass({"Terraria", "Player", {}})) {
        g_offInventory = fieldOffset(player, "inventory");
        g_offDirection = fieldOffset(player, "direction");
    }
    g_npcField = a.class_get_field_from_name(main, "npc");
    g_maxTilesX = a.class_get_field_from_name(main, "maxTilesX");
    if (!g_itemCtor || !g_setDefaults || g_offInventory < 0 || g_offItemType < 0 || g_offItemStack < 0 ||
        g_offItemMaxStack < 0) {
        BL_WARN("cheats: giving an item into the inventory is unavailable (ctor=%p SetDefaults=%p inventory=%d); it drops on the ground",
                (const void*)g_itemCtor, (const void*)g_setDefaults, g_offInventory);
    }

    g_sourceClass = findClass({"Terraria.DataStructures", "EntitySource_DebugCommand", {}});
    if (g_sourceClass) {
        g_sourceCtor = a.class_get_method_from_name(g_sourceClass, ".ctor", 0);
    }
    if (!g_newNpc || !g_sourceCtor) {
        BL_WARN("cheats: invocar NPC indisponivel (NewNPC=%p source=%p)",
                (const void*)g_newNpc, (const void*)g_sourceCtor);
    }

    if (!g_playerField || !g_getMyPlayer || !g_newItem) {
        BL_ERROR("cheats: references missing (player=%p get_myPlayer=%p NewItem=%p)",
                 (void*)g_playerField, (void*)g_getMyPlayer, (void*)g_newItem);
        return false;
    }
    return true;
}

Il2CppObject* playerAt(int index) {
    auto& a = il2cpp::api();
    Il2CppArray* players = nullptr;
    a.field_static_get_value(g_playerField, &players);
    if (!players) return nullptr;
    if (index < 0) {
        Il2CppObject* exc = nullptr;
        index = unboxInt(a.runtime_invoke(g_getMyPlayer, nullptr, nullptr, &exc));
        if (exc) return nullptr;
    }
    if (index < 0 || static_cast<uintptr_t>(index) >= players->length) return nullptr;
    return reinterpret_cast<Il2CppObject**>(arrayData(players))[index];
}

Il2CppObject* localPlayer() { return playerAt(-1); }

std::atomic<bool> g_inWorld{true};

void updateInWorld() {
    if (!g_getGameMenu) return;
    Il2CppObject* exc = nullptr;
    Il2CppObject* r = il2cpp::api().runtime_invoke(g_getGameMenu, nullptr, nullptr, &exc);
    if (exc || !r) return;
    const bool menu = *(reinterpret_cast<uint8_t*>(r) + sizeof(Il2CppObject)) != 0;
    g_inWorld.store(!menu, std::memory_order_relaxed);
}

using DoUpdateFn = void (*)(Il2CppObject*, Il2CppObject*, const MethodInfo*);
DoUpdateFn g_origDoUpdate = nullptr;

char g_lastCmd[128] = {0};

void pollCommands() {
    const std::string& path = cmdPath();
    if (path.empty()) return;
    FILE* f = fopen(path.c_str(), "r");
    if (!f) return;
    char buf[128] = {0};
    size_t n = fread(buf, 1, sizeof(buf) - 1, f);
    fclose(f);
    if (n == 0) { g_lastCmd[0] = 0; return; }

    remove(path.c_str());
    if (std::strcmp(buf, g_lastCmd) == 0) return;
    std::strncpy(g_lastCmd, buf, sizeof(g_lastCmd) - 1);

    if (std::strncmp(buf, "give ", 5) == 0) {
        char* end = nullptr;
        int type = static_cast<int>(std::strtol(buf + 5, &end, 10));
        int stack = end ? static_cast<int>(std::strtol(end, nullptr, 10)) : 0;
        if (type > 0) giveItem(type, stack > 0 ? stack : 1);
    }
}

void runSelftestOnce() {
    static bool done = false;
    if (done) return;
    done = true;

    Il2CppObject* exc = nullptr;
    int me = unboxInt(il2cpp::api().runtime_invoke(g_getMyPlayer, nullptr, nullptr, &exc));
    if (exc) BL_ERROR("cheats: selftest runtime_invoke get_myPlayer() lancou excecao");
    else BL_DEBUG("cheats: selftest runtime_invoke get_myPlayer() = %d (ok)", me);
}

int g_itemTotal = kVanillaItemCount;
int g_npcTotal = kVanillaNpcCount;
constexpr auto kBudgetPerFrame = std::chrono::microseconds(2000);

std::vector<std::u16string> g_itemNames;
std::vector<std::u16string> g_npcNames;
std::vector<int> g_npcFrames;
const MethodInfo* g_itemNameOf = nullptr;
const MethodInfo* g_npcNameOf = nullptr;
int g_nameProgress = -1;
std::chrono::steady_clock::time_point g_nameReadStart;
int g_nameReadFrames = 0;
int64_t g_nameReadWorstUs = 0;
std::atomic<bool> g_namesReady{false};
std::atomic<bool> g_namesWanted{false};
constexpr int kCultureCheckFrames = 30;
const MethodInfo* g_activeCulture = nullptr;
Il2CppObject* g_namesCulture = nullptr;
int g_cultureCheckWait = 0;
std::vector<std::u16string> g_nextItemNames;
std::vector<std::u16string> g_nextNpcNames;
int g_renameProgress = -1;
std::atomic<int> g_namesGeneration{0};
std::mutex g_namesLock;

std::vector<uint8_t> g_itemClass;
std::vector<uint8_t> g_itemSub;
std::vector<int32_t> g_itemStack;

struct ItemFields {
    int32_t damage, pick, axe, hammer, fishingPole, ammo, notAmmo,
        melee, ranged, magic, summon, sentry, accessory, headSlot, bodySlot,
        legSlot, potion, consumable, healLife, healMana, buffType, createTile,
        createWall, maxStack;
    int32_t useStyle, shoot, useAmmo, wingSlot, shoeSlot, shieldSlot, vanity, dye, paint,
        bait, material, mountType;
};

struct KindTables {
    Il2CppArray *staff = nullptr, *yoyo = nullptr, *drill = nullptr, *chainsaw = nullptr,
        *food = nullptr, *torches = nullptr, *bossBag = nullptr, *crate = nullptr,
        *boomerangs = nullptr, *whip = nullptr, *projHook = nullptr, *tileSolid = nullptr;
    int arrow = 40, bullet = 97, rocket = 771;
} g_kinds;

bool flagAt(Il2CppArray* t, int i) {
    return t && i >= 0 && static_cast<uintptr_t>(i) < t->length && static_cast<const uint8_t*>(arrayData(t))[i];
}

int projectileAi(int type) {
    static std::unordered_map<int, int> cache;
    static Il2CppObject* scratch = nullptr;
    static const MethodInfo* setDefaults = nullptr;
    static int32_t offAi = -1;
    static bool tried = false;
    if (type <= 0) return -1;
    auto it = cache.find(type);
    if (it != cache.end()) return it->second;
    auto& a = il2cpp::api();
    if (!tried) {
        tried = true;
        Il2CppClass* proj = il2cpp::findClass({"Terraria", "Projectile", {}});
        const MethodInfo* ctor = proj ? a.class_get_method_from_name(proj, ".ctor", 0) : nullptr;
        setDefaults = proj ? il2cpp::findMethodBySignature(
            proj, il2cpp::parseSignature("void SetDefaults(int Type)")) : nullptr;
        offAi = proj ? il2cpp::fieldOffset(proj, "aiStyle") : -1;
        if (proj && ctor && setDefaults && offAi >= 0) {
            scratch = a.object_new(proj);
            Il2CppObject* exc = nullptr;
            a.runtime_invoke(ctor, scratch, nullptr, &exc);
            if (exc) scratch = nullptr;
            else a.gchandle_new(scratch, false);
        }
    }
    int ai = -1;
    if (scratch) {
        int t = type;
        void* args[1] = {&t};
        Il2CppObject* exc = nullptr;
        a.runtime_invoke(setDefaults, scratch, args, &exc);
        if (!exc) ai = field<int32_t>(scratch, offAi);
    }
    cache[type] = ai;
    return ai;
}

uint8_t classOf(Il2CppObject* it, const ItemFields& o) {
    auto i32 = [&](int32_t off) { return field<int32_t>(it, off); };
    auto b = [&](int32_t off) { return field<uint8_t>(it, off) != 0; };
    const bool hasDamage = i32(o.damage) > 0;

    if (i32(o.pick) > 0 || i32(o.axe) > 0 || i32(o.hammer) > 0 || i32(o.fishingPole) > 0)
        return kClassTool;
    if (flagAt(g_kinds.projHook, i32(o.shoot))) return kClassTool;
    if (i32(o.ammo) > 0 && !b(o.notAmmo) && i32(o.createTile) < 0) return kClassAmmo;
    if (b(o.summon) || b(o.sentry)) return kClassSummon;
    if (hasDamage && b(o.melee)) return kClassMelee;
    if (hasDamage && b(o.ranged)) return kClassRanged;
    if (hasDamage && b(o.magic)) return kClassMagic;
    if (b(o.accessory)) return kClassAccessory;
    if (i32(o.headSlot) >= 0 || i32(o.bodySlot) >= 0 || i32(o.legSlot) >= 0) return kClassArmor;
    if (b(o.potion) || (b(o.consumable) &&
        (i32(o.healLife) > 0 || i32(o.healMana) > 0 || i32(o.buffType) > 0)))
        return kClassPotion;
    if (i32(o.createTile) >= 0 || i32(o.createWall) > 0) return kClassBlock;
    return kClassOther;
}

uint8_t subOf(Il2CppObject* it, int id, uint8_t cls, const ItemFields& o) {
    auto i32 = [&](int32_t off) { return field<int32_t>(it, off); };
    auto i8 = [&](int32_t off) { return static_cast<int>(field<int8_t>(it, off)); };
    auto u8 = [&](int32_t off) { return static_cast<int>(field<uint8_t>(it, off)); };
    const KindTables& k = g_kinds;
    const int shoot = i32(o.shoot);
    switch (cls) {
        case kClassMelee: {
            if (flagAt(k.yoyo, id)) return 5;
            const int ai = shoot > 0 ? projectileAi(shoot) : -1;
            if (flagAt(k.boomerangs, id) || ai == 3) return 6;
            if (ai == 19) return 3;
            if (ai == 15) return 4;
            if (u8(o.useStyle) == 13) return 2;
            if (u8(o.useStyle) == 1) return 1;
            return 0;
        }
        case kClassRanged: {
            const int ammo = i32(o.useAmmo);
            if (ammo == k.arrow) return 1;
            if (ammo == k.bullet) return 2;
            if (ammo == k.rocket) return 3;
            if (u8(o.consumable)) return 4;
            return 0;
        }
        case kClassMagic:
            return flagAt(k.staff, id) ? 1 : 0;
        case kClassSummon:
            if (flagAt(k.whip, shoot)) return 3;
            if (u8(o.sentry)) return 2;
            if (i32(o.buffType) > 0) return 1;
            return 0;
        case kClassAmmo: {
            const int ammo = i32(o.ammo);
            if (ammo == k.arrow) return 1;
            if (ammo == k.bullet) return 2;
            if (ammo == k.rocket) return 3;
            return 0;
        }
        case kClassTool:
            if (flagAt(k.drill, id) || flagAt(k.chainsaw, id)) return 4;
            if (i32(o.fishingPole) > 0) return 5;
            if (flagAt(k.projHook, shoot)) return 6;
            if (i32(o.pick) > 0) return 1;
            if (i32(o.axe) > 0) return 2;
            if (i32(o.hammer) > 0) return 3;
            return 0;
        case kClassAccessory:
            if (i8(o.wingSlot) > 0) return 1;
            if (i8(o.shoeSlot) > 0) return 2;
            if (i8(o.shieldSlot) > 0) return 3;
            return 0;
        case kClassArmor:
            if (u8(o.vanity)) return 4;
            if (i32(o.headSlot) >= 0) return 1;
            if (i32(o.bodySlot) >= 0) return 2;
            if (i32(o.legSlot) >= 0) return 3;
            return 0;
        case kClassPotion:
            if (i32(o.healLife) > 0) return 1;
            if (i32(o.healMana) > 0) return 2;
            if (flagAt(k.food, id)) return 3;
            if (i32(o.buffType) > 0) return 4;
            return 0;
        case kClassBlock: {
            if (flagAt(k.torches, id)) return 3;
            if (i32(o.createWall) > 0) return 2;
            const int tile = i32(o.createTile);
            if (tile >= 0 && flagAt(k.tileSolid, tile)) return 1;
            if (tile >= 0) return 4;
            return 0;
        }
        case kClassOther:
            if (flagAt(k.bossBag, id) || flagAt(k.crate, id)) return 5;
            if (i32(o.mountType) >= 0) return 4;
            if (i32(o.bait) > 0) return 3;
            if (u8(o.dye) > 0 || u8(o.paint) > 0) return 2;
            if (u8(o.material)) return 1;
            return 0;
        default:
            return 0;
    }
}

Il2CppArray* staticArray(const char* ns, const char* cls, const char* nested, const char* name) {
    Il2CppClass* c = il2cpp::findClassQuiet(ns, cls);
    if (c && nested) c = il2cpp::findNested(c, nested);
    FieldInfo* f = c ? il2cpp::findField(c, name) : nullptr;
    Il2CppArray* arr = nullptr;
    if (f) {
        il2cpp::api().runtime_class_init(c);
        il2cpp::api().field_static_get_value(f, &arr);
    }
    if (!arr) BL_WARN("cheats: %s.%s not found; this subcategory stays empty", cls, name);
    return arr;
}

int staticInt(const char* ns, const char* cls, const char* name, int fallback) {
    Il2CppClass* c = il2cpp::findClassQuiet(ns, cls);
    FieldInfo* f = c ? il2cpp::findField(c, name) : nullptr;
    int v = fallback;
    if (f) il2cpp::api().field_static_get_value(f, &v);
    return v;
}

void loadKindTables() {
    KindTables& k = g_kinds;
    k.staff = staticArray("Terraria", "Item", nullptr, "staff");
    k.yoyo = staticArray("Terraria.ID", "ItemID", "Sets", "Yoyo");
    k.drill = staticArray("Terraria.ID", "ItemID", "Sets", "IsDrill");
    k.chainsaw = staticArray("Terraria.ID", "ItemID", "Sets", "IsChainsaw");
    k.food = staticArray("Terraria.ID", "ItemID", "Sets", "IsFood");
    k.torches = staticArray("Terraria.ID", "ItemID", "Sets", "Torches");
    k.bossBag = staticArray("Terraria.ID", "ItemID", "Sets", "BossBag");
    k.crate = staticArray("Terraria.ID", "ItemID", "Sets", "IsFishingCrate");
    k.boomerangs = staticArray("Terraria.GameContent.Prefixes", "PrefixLegacy", "ItemSets", "BoomerangsChakrams");
    k.whip = staticArray("Terraria.ID", "ProjectileID", "Sets", "IsAWhip");
    k.projHook = staticArray("Terraria", "Main", nullptr, "projHook");
    k.tileSolid = staticArray("Terraria", "Main", nullptr, "tileSolid");
    k.arrow = staticInt("Terraria.ID", "AmmoID", "Arrow", 40);
    k.bullet = staticInt("Terraria.ID", "AmmoID", "Bullet", 97);
    k.rocket = staticInt("Terraria.ID", "AmmoID", "Rocket", 771);
}

void classifyModItems(const ItemFields& o) {
    auto& a = il2cpp::api();
    Il2CppClass* itemCls = il2cpp::findClass({"Terraria", "Item", {}});
    const MethodInfo* ctor = itemCls ? a.class_get_method_from_name(itemCls, ".ctor", 0) : nullptr;
    const MethodInfo* setDefaults = itemCls ? il2cpp::findMethodBySignature(
        itemCls, il2cpp::parseSignature("void SetDefaults(int Type, ItemVariant variant)")) : nullptr;
    if (!ctor || !setDefaults) return;
    Il2CppObject* scratch = nullptr;
    int done = 0;
    for (int id = kVanillaItemCount; id < g_itemTotal; ++id) {
        if (g_itemClass[static_cast<size_t>(id)] != kClassNone || isUnloadedType(id)) continue;
        if (!scratch) {
            scratch = a.object_new(itemCls);
            Il2CppObject* exc = nullptr;
            a.runtime_invoke(ctor, scratch, nullptr, &exc);
            if (exc) return;
        }
        int t = id;
        void* args[2] = {&t, nullptr};
        Il2CppObject* exc = nullptr;
        a.runtime_invoke(setDefaults, scratch, args, &exc);
        if (exc || field<int32_t>(scratch, o.maxStack) <= 0) continue;
        const uint8_t c = classOf(scratch, o);
        g_itemClass[static_cast<size_t>(id)] = c;
        g_itemSub[static_cast<size_t>(id)] = subOf(scratch, id, c, o);
        const bool unique = (c == kClassMelee || c == kClassRanged || c == kClassMagic ||
                            c == kClassSummon || c == kClassTool || c == kClassAccessory ||
                            c == kClassArmor) && !field<uint8_t>(scratch, o.consumable);
        const int declared = field<int32_t>(scratch, o.maxStack);
        g_itemStack[static_cast<size_t>(id)] = unique ? 1 : (declared > 1 ? declared : 9999);
        ++done;
    }
    if (done) BL_DEBUG("cheats: %d item(ns) de mod classificado(s)", done);
}

struct Classification {
    bool failed = false;
    FieldInfo* fDict = nullptr;
    int32_t offEntries = -1, offCount = -1;
    size_t stride = 0, oKey = 0, oVal = 0, oHash = 0;
    ItemFields o{};
    int32_t pos = -1;
    int32_t readCount = 0;
} g_class;

void failClassification(const char* reason) {
    BL_ERROR("cheats: %s; the item sections stay empty (All items still works)", reason);
    g_class.failed = true;
}

Il2CppArray* dictionaryEntries(int32_t* count) {
    auto& a = il2cpp::api();
    Il2CppObject* dict = nullptr;
    a.field_static_get_value(g_class.fDict, &dict);
    if (!dict) return nullptr;
    *count = field<int32_t>(dict, g_class.offCount);
    return field<Il2CppArray*>(dict, g_class.offEntries);
}

bool prepareClassification() {
    using namespace il2cpp;
    auto& a = api();
    Classification& k = g_class;

    Il2CppClass* cs = findClass({"Terraria.ID", "ContentSamples", {}});
    k.fDict = cs ? findField(cs, "ItemsByType") : nullptr;
    Il2CppObject* dict = nullptr;
    if (k.fDict) a.field_static_get_value(k.fDict, &dict);
    Il2CppClass* itemCls = findClass({"Terraria", "Item", {}});
    if (!dict || !itemCls) { failClassification("ContentSamples.ItemsByType not found"); return false; }

    Il2CppClass* dc = a.object_get_class(dict);
    FieldInfo* fEntries = a.class_get_field_from_name(dc, "_entries");
    FieldInfo* fCount = a.class_get_field_from_name(dc, "_count");
    if (!fEntries || !fCount) { failClassification("Dictionary layout differs from the expected"); return false; }
    k.offEntries = static_cast<int32_t>(a.field_get_offset(fEntries));
    k.offCount = static_cast<int32_t>(a.field_get_offset(fCount));

    int32_t count = 0;
    Il2CppArray* entries = dictionaryEntries(&count);
    Il2CppClass* ec = entries ? a.class_get_element_class(
                                    a.object_get_class(reinterpret_cast<Il2CppObject*>(entries)))
                              : nullptr;
    if (!ec) { failClassification("dictionary entries without a type"); return false; }
    FieldInfo* fKey = a.class_get_field_from_name(ec, "key");
    FieldInfo* fVal = a.class_get_field_from_name(ec, "value");
    FieldInfo* fHash = a.class_get_field_from_name(ec, "hashCode");
    if (!fKey || !fVal || !fHash) { failClassification("Entry without key/value/hashCode"); return false; }

    uint32_t align = 0;
    k.stride = static_cast<size_t>(a.class_value_size(ec, &align));
    k.oKey = a.field_get_offset(fKey);
    k.oVal = a.field_get_offset(fVal);
    k.oHash = a.field_get_offset(fHash);
    if (k.oVal + sizeof(void*) > k.stride) {
        k.oKey -= sizeof(Il2CppObject);
        k.oVal -= sizeof(Il2CppObject);
        k.oHash -= sizeof(Il2CppObject);
    }

    ItemFields& o = k.o;
    struct { int32_t* dst; const char* name; } fields[] = {
        {&o.damage, "damage"}, {&o.pick, "pick"}, {&o.axe, "axe"},
        {&o.hammer, "hammer"}, {&o.fishingPole, "fishingPole"}, {&o.ammo, "ammo"},
        {&o.notAmmo, "notAmmo"}, {&o.melee, "melee"}, {&o.ranged, "ranged"},
        {&o.magic, "magic"}, {&o.summon, "summon"}, {&o.sentry, "sentry"},
        {&o.accessory, "accessory"}, {&o.headSlot, "headSlot"}, {&o.bodySlot, "bodySlot"},
        {&o.legSlot, "legSlot"}, {&o.potion, "potion"}, {&o.consumable, "consumable"},
        {&o.healLife, "healLife"}, {&o.healMana, "healMana"}, {&o.buffType, "buffType"},
        {&o.createTile, "createTile"}, {&o.createWall, "createWall"}, {&o.maxStack, "maxStack"},
        {&o.useStyle, "useStyle"}, {&o.shoot, "shoot"}, {&o.useAmmo, "useAmmo"},
        {&o.wingSlot, "wingSlot"}, {&o.shoeSlot, "shoeSlot"}, {&o.shieldSlot, "shieldSlot"},
        {&o.vanity, "vanity"}, {&o.dye, "dye"}, {&o.paint, "paint"}, {&o.bait, "bait"},
        {&o.material, "material"}, {&o.mountType, "mountType"},
    };
    for (auto& c : fields) {
        *c.dst = fieldOffset(itemCls, c.name);
        if (*c.dst < 0) {
            BL_ERROR("cheats: Item.%s not found; the item sections stay empty", c.name);
            k.failed = true;
            return false;
        }
    }

    g_itemClass.assign(g_itemTotal, kClassNone);
    g_itemSub.assign(g_itemTotal, 0);
    g_itemStack.assign(g_itemTotal, 0);
    loadKindTables();
    k.pos = 0;
    return true;
}

template <typename TimePoint>
bool classifyUntil(TimePoint deadline) {
    Classification& k = g_class;
    if (k.failed) return true;
    if (k.pos < 0 && !prepareClassification()) return true;

    int32_t count = 0;
    Il2CppArray* entries = dictionaryEntries(&count);
    if (!entries) { failClassification("the samples dictionary vanished midway"); return true; }
    const char* base = static_cast<const char*>(arrayData(entries));
    const ItemFields& o = k.o;
    for (; k.pos < count && static_cast<uintptr_t>(k.pos) < entries->length; ++k.pos) {
        if ((k.pos & 63) == 0 && k.pos > 0 && std::chrono::steady_clock::now() > deadline) {
            return false;
        }
        const char* ent = base + static_cast<size_t>(k.pos) * k.stride;
        if (*reinterpret_cast<const int32_t*>(ent + k.oHash) < 0) continue;
        const int32_t id = *reinterpret_cast<const int32_t*>(ent + k.oKey);
        auto* item = *reinterpret_cast<Il2CppObject* const*>(ent + k.oVal);
        if (!item || id <= 0 || id >= g_itemTotal) continue;
        const uint8_t c = classOf(item, o);
        g_itemClass[id] = c;
        g_itemSub[id] = subOf(item, id, c, o);
        const bool unique = (c == kClassMelee || c == kClassRanged || c == kClassMagic ||
                            c == kClassSummon || c == kClassTool || c == kClassAccessory ||
                            c == kClassArmor) && !field<uint8_t>(item, o.consumable);
        const int declared = field<int32_t>(item, o.maxStack);
        g_itemStack[id] = unique ? 1 : (id >= kVanillaItemCount && declared <= 1 ? 9999 : declared);
        ++k.readCount;
    }

    classifyModItems(o);

    int perClass[11] = {};
    for (uint8_t c : g_itemClass) if (c < 11) ++perClass[c];
    BL_DEBUG("cheats: %d itens classificados | corpo %d, distancia %d, magia %d, "
             "invocacao %d, municao %d, ferramenta %d, acessorio %d, armadura %d, "
             "pocao %d, bloco %d, outros %d", k.readCount, perClass[1], perClass[2],
             perClass[3], perClass[4], perClass[5], perClass[6], perClass[7],
             perClass[8], perClass[9], perClass[10], perClass[0]);
    return true;
}

std::u16string toU16(Il2CppString* s) {
    if (!s || s->length <= 0) return {};
    return std::u16string(reinterpret_cast<const char16_t*>(s->chars),
                          static_cast<size_t>(s->length));
}

Il2CppObject* activeCulture() {
    auto& a = il2cpp::api();
    if (!g_activeCulture) {
        Il2CppClass* language = il2cpp::findClass({"Terraria.Localization", "Language", {}});
        g_activeCulture = language ? a.class_get_method_from_name(language, "get_ActiveCulture", 0) : nullptr;
        if (!g_activeCulture) return nullptr;
    }
    Il2CppObject* exc = nullptr;
    Il2CppObject* r = a.runtime_invoke(g_activeCulture, nullptr, nullptr, &exc);
    return exc ? nullptr : r;
}

bool readNamesUntil(std::vector<std::u16string>& items, std::vector<std::u16string>& npcs,
                    int& progress, std::chrono::steady_clock::time_point deadline) {
    auto& a = il2cpp::api();
    const int start = progress;
    const int total = g_itemTotal + g_npcTotal;
    for (; progress < total; ++progress) {
        if ((progress & 15) == 0 && progress > start &&
            std::chrono::steady_clock::now() > deadline) {
            return false;
        }
        const bool item = progress < g_itemTotal;
        int id = item ? progress : progress - g_itemTotal;
        void* args[1] = {&id};
        Il2CppObject* exc = nullptr;
        Il2CppObject* r = a.runtime_invoke(item ? g_itemNameOf : g_npcNameOf,
                                           nullptr, args, &exc);
        if (exc) continue;
        std::u16string name = toU16(reinterpret_cast<Il2CppString*>(r));
        if (item) items[id] = std::move(name);
        else npcs[id] = std::move(name);
    }
    return true;
}

void refreshNamesSlice() {
    if (!g_itemNameOf || !g_npcNameOf) return;
    if (g_renameProgress < 0) {
        if (++g_cultureCheckWait < kCultureCheckFrames) return;
        g_cultureCheckWait = 0;
        Il2CppObject* culture = activeCulture();
        if (!culture || culture == g_namesCulture) return;
        g_namesCulture = culture;
        g_nextItemNames.assign(g_itemTotal, std::u16string());
        g_nextNpcNames.assign(g_npcTotal, std::u16string());
        g_renameProgress = 0;
        g_namesGeneration.store(-g_namesGeneration.load() - 1);
    }
    using Clock = std::chrono::steady_clock;
    const auto t0 = Clock::now();
    if (!readNamesUntil(g_nextItemNames, g_nextNpcNames, g_renameProgress, t0 + kBudgetPerFrame)) {
        return;
    }
    std::vector<std::u16string> buffs = readBuffNames();
    {
        std::lock_guard<std::mutex> guard(g_namesLock);
        g_itemNames.swap(g_nextItemNames);
        g_npcNames.swap(g_nextNpcNames);
        replaceBuffNames(buffs);
    }
    std::vector<std::u16string>().swap(g_nextItemNames);
    std::vector<std::u16string>().swap(g_nextNpcNames);
    g_renameProgress = -1;
    g_namesGeneration.store(-g_namesGeneration.load(), std::memory_order_release);
    BL_DEBUG("cheats: language changed; names reloaded (generation %d)", g_namesGeneration.load());
}

void readNamesSlice() {
    if (!g_namesWanted.load(std::memory_order_relaxed)) return;
    if (g_namesReady.load(std::memory_order_relaxed)) { refreshNamesSlice(); return; }
    auto& a = il2cpp::api();

    if (g_nameProgress < 0) {
        Il2CppClass* lang = il2cpp::findClass({"Terraria", "Lang", {}});
        g_itemNameOf = lang ? a.class_get_method_from_name(lang, "GetItemNameValue", 1) : nullptr;
        g_npcNameOf  = lang ? a.class_get_method_from_name(lang, "GetNPCNameValue", 1) : nullptr;
        if (!g_itemNameOf || !g_npcNameOf) {
            BL_ERROR("cheats: Lang.GetItemNameValue/GetNPCNameValue not found; "
                     "the menu shows only the ids");
            g_namesReady.store(true, std::memory_order_release);
            return;
        }
        g_itemTotal = itemTypeCount();
        while (g_itemTotal > kVanillaItemCount && isUnloadedType(g_itemTotal - 1)) --g_itemTotal;
        g_itemNames.assign(g_itemTotal, std::u16string());
        g_npcTotal = npcTypeCount();
        g_npcNames.assign(g_npcTotal, std::u16string());
        g_nameProgress = 0;
        g_nameReadStart = std::chrono::steady_clock::now();
        g_namesCulture = activeCulture();
    }

    using Clock = std::chrono::steady_clock;
    const auto t0 = Clock::now();
    const bool classified = readNamesUntil(g_itemNames, g_npcNames, g_nameProgress, t0 + kBudgetPerFrame) &&
                            classifyUntil(t0 + kBudgetPerFrame);
    ++g_nameReadFrames;
    const int64_t us = std::chrono::duration_cast<std::chrono::microseconds>(
        Clock::now() - t0).count();
    if (us > g_nameReadWorstUs) g_nameReadWorstUs = us;
    if (classified) {
        Il2CppClass* main = il2cpp::findClass({"Terraria", "Main", {}});
        FieldInfo* f = main ? il2cpp::findField(main, "npcFrameCount") : nullptr;
        Il2CppArray* arr = nullptr;
        if (f) a.field_static_get_value(f, &arr);
        if (arr) {
            const int32_t* v = static_cast<const int32_t*>(arrayData(arr));
            size_t n = static_cast<size_t>(arr->length);
            g_npcFrames.assign(v, v + n);
        } else {
            BL_ERROR("cheats: Main.npcFrameCount did not arrive; the list will show "
                     "each NPC's whole sheet");
        }
        buildMenuCatalogExtras();
        g_namesReady.store(true, std::memory_order_release);
        int unnamed = 0;
        for (const auto& n : g_itemNames) if (n.empty()) ++unnamed;
        const auto ms = std::chrono::duration_cast<std::chrono::milliseconds>(
            Clock::now() - g_nameReadStart).count();
        BL_DEBUG("cheats: names and classes ready (%d items, %d NPCs; %d items without a "
                 "name) in %lld ms, %d frames, worst frame %.1f ms", g_itemTotal, g_npcTotal,
                 unnamed, static_cast<long long>(ms), g_nameReadFrames, g_nameReadWorstUs / 1000.0);
    }
}

void giveBuff(int type, int seconds) {
    auto& a = il2cpp::api();
    static const MethodInfo* addBuff = nullptr;
    if (!addBuff) {
        Il2CppClass* player = il2cpp::findClass({"Terraria", "Player", {}});
        addBuff = player ? il2cpp::findMethodBySignature(
            player, il2cpp::parseSignature("void AddBuff(int type, int time, bool fromNetPvP)")) : nullptr;
        if (!addBuff) { BL_ERROR("cheats: Player.AddBuff not found"); return; }
    }
    Il2CppObject* p = playerAt(-1);
    if (!p) return;
    int t = type, time = seconds * 60;
    uint8_t fromNet = 0;
    void* args[3] = {&t, &time, &fromNet};
    Il2CppObject* exc = nullptr;
    a.runtime_invoke(addBuff, p, args, &exc);
    if (exc) BL_ERROR("cheats: AddBuff(%d) lancou excecao", type);
}

void allowDeprecatedItems() {
    static bool done = false;
    if (done) return;
    done = true;
    Il2CppArray* deprecated = staticArray("Terraria.ID", "ItemID", "Sets", "Deprecated");
    if (deprecated) std::memset(arrayData(deprecated), 0, deprecated->length);
}

void hkDoUpdate(Il2CppObject* self, Il2CppObject* gt, const MethodInfo* m) {
    runtime::noteGameThread();
    tickFramePacing();
    runSelftestOnce();
    allowDeprecatedItems();
    updateInWorld();
    readNamesSlice();
    tickPowers();
    tickModLanguage();
    if (gameInitialized()) {
        tickModItems();
        tickModProjectiles();
        tickModNpcs();
        tickModBuffs();
        tickModDusts();
        tickModTiles();
        tickModWalls();
    }
    tickTexturePacks();
    tickFontPacks();
    tickContentReady();
    tickQuickStart();
    tickNetRequests();
    script::tickConsole();
    tickRestart();
    if (uint64_t req = g_pendingGive.exchange(0)) {
        giveItem(static_cast<int>(req >> 32), static_cast<int>(req & 0xffffffffu));
    }
    if (uint64_t req = g_pendingBuff.exchange(0)) {
        giveBuff(static_cast<int>(req >> 32), static_cast<int>(req & 0xffffffffu));
    }
    if (uint64_t req = g_pendingSpawn.exchange(0)) {
        int npcType = static_cast<int>(req >> 32);
        int spawnCount = static_cast<int>(req & 0xffffffffu);
        spawnNpcs(npcType, spawnCount);
    }
    pollCommands();
    g_origDoUpdate(self, gt, m);
}

}

bool namesReady() {
    g_namesWanted.store(true, std::memory_order_relaxed);
    return g_namesReady.load(std::memory_order_acquire);
}
const std::vector<std::u16string>& itemNames() { return g_itemNames; }
const std::vector<std::u16string>& npcNames() { return g_npcNames; }
std::mutex& namesLock() { return g_namesLock; }
int namesGeneration() { return g_namesGeneration.load(std::memory_order_acquire); }
const std::vector<int>& npcFrames() { return g_npcFrames; }
bool inWorld() { return g_inWorld.load(std::memory_order_relaxed); }
const std::vector<uint8_t>& itemClasses() { return g_itemClass; }
const std::vector<uint8_t>& itemSubClasses() { return g_itemSub; }

void requestGive(int type, int stack) {
    if (type > 0) g_pendingGive.store(packGive(type, stack > 0 ? stack : 1));
}

void requestBuff(int type, int seconds) {
    if (type <= 0) return;
    if (seconds < 1) seconds = 1;
    g_pendingBuff.store((static_cast<uint64_t>(type) << 32) | static_cast<uint32_t>(seconds));
}

void requestSpawn(int type, int count) {
    if (count < 1) count = 1;
    if (count > kMaxSpawn) count = kMaxSpawn;
    if (type > 0) {
        g_pendingSpawn.store((static_cast<uint64_t>(type) << 32) |
                             static_cast<uint32_t>(count));
    }
}

void spawnNpcs(int type, int count, int player) {
    auto& a = il2cpp::api();
    if (!g_refsOk || !g_newNpc || !g_sourceCtor) {
        BL_ERROR("cheats: invocar NPC indisponivel");
        return;
    }
    if (count < 1) count = 1;
    if (count > kMaxSpawn) count = kMaxSpawn;
    const int mode = netMode();
    if (mode == 1) {
        sendToServer("npc " + std::to_string(type) + " " + std::to_string(count));
        return;
    }
    Il2CppObject* p = playerAt(player);
    if (!p) { BL_WARN("spawnNpcs: no player %d (outside the world?)", player); return; }

    static Il2CppObject* source = nullptr;
    if (!source) {
        source = a.object_new(g_sourceClass);
        Il2CppObject* exc = nullptr;
        a.runtime_invoke(g_sourceCtor, source, nullptr, &exc);
        if (exc) { BL_ERROR("spawnNpcs: the source constructor threw an exception"); source = nullptr; return; }
    }

    const auto& e = game().entity;
    const Vector2 pos = field<Vector2>(p, e.position);
    const int dir = g_offDirection >= 0 && field<int32_t>(p, g_offDirection) < 0 ? -1 : 1;
    int32_t tilesX = 0;
    if (g_maxTilesX) a.field_static_get_value(g_maxTilesX, &tilesX);
    const float worldRight = tilesX * 16.f - kSpawnWorldMargin;

    const float rowStart = pos.x + field<int32_t>(p, e.width) * 0.5f + dir * kSpawnAhead;
    float cursor = rowStart;
    float bottom = pos.y - kSpawnLift;
    float rowHeight = 0.f;
    int spawned = 0, lastIdx = -1;
    for (int i = 0; i < count; ++i) {
        int x = static_cast<int>(cursor), y = static_cast<int>(bottom);
        int t = type, start = 0, target = 255;
        float ai0 = 0, ai1 = 0, ai2 = 0, ai3 = 0;
        void* args[10] = { source, &x, &y, &t, &start, &ai0, &ai1, &ai2, &ai3, &target };

        Il2CppObject* exc = nullptr;
        const int idx = unboxInt(a.runtime_invoke(g_newNpc, nullptr, args, &exc));
        if (exc) { BL_ERROR("spawnNpcs: NewNPC lancou excecao (type=%d)", type); return; }
        if (idx < 0 || idx >= kMaxNpcs) break;

        Il2CppArray* npcs = nullptr;
        if (g_npcField) a.field_static_get_value(g_npcField, &npcs);
        Il2CppObject* npc = npcs && static_cast<uintptr_t>(idx) < npcs->length
            ? reinterpret_cast<Il2CppObject**>(arrayData(npcs))[idx] : nullptr;
        const float w = npc ? static_cast<float>(field<int32_t>(npc, e.width)) : 32.f;
        const float h = npc ? static_cast<float>(field<int32_t>(npc, e.height)) : 32.f;
        if (npc) {
            float left = dir > 0 ? cursor : cursor - w;
            if (tilesX > 0) left = std::max(kSpawnWorldMargin, std::min(left, worldRight - w));
            field<Vector2>(npc, e.position).x = left;
        }
        cursor += dir * (w + kSpawnGap);
        rowHeight = std::max(rowHeight, h);
        if (std::fabs(cursor - rowStart) > kSpawnRowLength) {
            cursor = rowStart;
            bottom = std::max(kSpawnWorldMargin, bottom - rowHeight - kSpawnGap);
            rowHeight = 0.f;
        }
        if (mode & 2) sendData(kMsgSyncNpc, idx);
        ++spawned;
        lastIdx = idx;
    }
    BL_INFO("spawnNpcs: type=%d, %d of %d near player %d (side %d, netMode %d, last npc[%d])",
            type, spawned, count, player, dir, mode, lastIdx);
}

namespace {

int stackCap(int type, Il2CppObject* item) {
    if (type > 0 && static_cast<size_t>(type) < g_itemStack.size() && g_itemStack[type] > 0) return g_itemStack[type];
    const int max = field<int32_t>(item, g_offItemMaxStack);
    return max > 0 ? max : 1;
}

int giveToInventory(Il2CppObject* p, int type, int stack) {
    auto& a = il2cpp::api();
    auto* inv = field<Il2CppArray*>(p, g_offInventory);
    if (!inv) return stack;
    auto** slots = reinterpret_cast<Il2CppObject**>(arrayData(inv));
    const int main = inv->length < kMainInventory ? static_cast<int>(inv->length) : kMainInventory;
    Il2CppObject* exc = nullptr;
    const int mine = unboxInt(a.runtime_invoke(g_getMyPlayer, nullptr, nullptr, &exc));
    const bool sync = netMode() != 0;
    if (type > 0 && static_cast<size_t>(type) < g_itemStack.size() && g_itemStack[type] > 0 &&
        stack > g_itemStack[type]) {
        stack = g_itemStack[type];
    }
    int left = stack;

    auto newItem = [&](int slot) -> Il2CppObject* {
        Il2CppObject* it = a.object_new(g_itemType);
        Il2CppObject* exc = nullptr;
        a.runtime_invoke(g_itemCtor, it, nullptr, &exc);
        if (exc) return nullptr;
        int t = type;
        void* args[2] = {&t, nullptr};
        a.runtime_invoke(g_setDefaults, it, args, &exc);
        if (exc) { BL_ERROR("giveItem: SetDefaults(%d) lancou excecao", type); return nullptr; }
        a.gc_wbarrier_set_field(reinterpret_cast<Il2CppObject*>(inv), reinterpret_cast<void**>(&slots[slot]), it);
        return it;
    };
    auto synced = [&](int slot) {
        if (sync && mine >= 0) sendData(kMsgSyncEquipment, mine, static_cast<float>(slot), 0.0f);
    };

    for (int i = 0; i < main && left > 0; ++i) {
        Il2CppObject* it = slots[i];
        if (!it || field<int32_t>(it, g_offItemType) != type) continue;
        const int have = field<int32_t>(it, g_offItemStack);
        const int room = stackCap(type, it) - have;
        if (room <= 0) continue;
        const int add = left < room ? left : room;
        field<int32_t>(it, g_offItemStack) = have + add;
        left -= add;
        synced(i);
    }
    for (int i = 0; i < main && left > 0; ++i) {
        Il2CppObject* it = slots[i];
        if (it && field<int32_t>(it, g_offItemType) > 0 && field<int32_t>(it, g_offItemStack) > 0) continue;
        it = newItem(i);
        if (!it) break;
        const int cap = stackCap(type, it);
        const int put = left < cap ? left : cap;
        field<int32_t>(it, g_offItemStack) = put;
        left -= put;
        synced(i);
    }
    return left;
}

}

void giveItem(int type, int stack, int player) {
    using namespace il2cpp;
    if (!g_refsOk) { BL_ERROR("cheats: references not ready"); return; }
    auto& a = api();

    Il2CppObject* p = playerAt(player);
    if (!p) { BL_WARN("giveItem: no player %d (outside the world?)", player); return; }

    if ((player < 0 || p == localPlayer()) && g_itemCtor && g_setDefaults && g_offInventory >= 0 &&
        g_offItemMaxStack >= 0) {
        const int left = giveToInventory(p, type, stack > 0 ? stack : 1);
        if (left > 0) BL_WARN("giveItem: inventory full; %d of %d left out", left, type);
        BL_INFO("giveItem: type=%d x%d requested, in the inventory (player %d)", type, stack, player);
        return;
    }

    Vector2 pos = field<Vector2>(p, game().entity.position);
    int x = static_cast<int>(pos.x), y = static_cast<int>(pos.y);
    int w = 10, h = 10, t = type, n = stack > 0 ? stack : 1, pfix = 0;
    if (type > 0 && static_cast<size_t>(type) < g_itemStack.size() &&
        g_itemStack[type] > 0 && n > g_itemStack[type]) {
        n = g_itemStack[type];
    }
    uint8_t noBroadcast = 0;
    int32_t ownership = 0;
    void* args[9] = { &x, &y, &w, &h, &t, &n, &noBroadcast, &pfix, &ownership };

    Il2CppObject* exc = nullptr;
    int idx = unboxInt(a.runtime_invoke(g_newItem, nullptr, args, &exc));
    if (exc) { BL_ERROR("giveItem: NewItem lancou excecao"); return; }
    BL_INFO("giveItem: type=%d x%d given at (%d,%d) -> item[%d] (player %d)", t, n, x, y, idx, player);
}

void installCheats() {
    installNetRequests();
    g_refsOk = resolveCheatRefs();
    if (!g_refsOk) { BL_ERROR("cheats: disabled (references missing)"); return; }

    Il2CppClass* main = il2cpp::findClass({"Terraria", "Main", {}});
    const MethodInfo* doUpdate = main
        ? il2cpp::api().class_get_method_from_name(main, "DoUpdate", 1) : nullptr;
    if (doUpdate && hook::install(doUpdate, hkDoUpdate, &g_origDoUpdate)) {
        const std::string& cmd = cmdPath();
        BL_DEBUG("cheats: ready (file dev channel: %s)",
                 cmd.empty() ? "off" : cmd.c_str());
    } else {
        BL_ERROR("cheats: failed to hook Main.DoUpdate");
    }
}

}
