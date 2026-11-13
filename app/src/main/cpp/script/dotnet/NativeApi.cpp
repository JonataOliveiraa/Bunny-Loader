#include "script/dotnet/NativeApi.h"
#include "script/dotnet/DotnetCall.h"
#include "script/dotnet/HookProfile.h"
#include "script/dotnet/HookGate.h"
#include "boot/Boot.h"
#include "boot/QuickStart.h"
#include "content/buffs/BuffIconLook.h"
#include "content/players/PlayerStages.h"
#include "content/ui/EmoteMenuIcon.h"
#include "content/tiles/ModTiles.h"
#include "content/tiles/ModWater.h"
#include "content/tiles/ModTileMap.h"
#include "content/tiles/TileEvents.h"
#include "content/walls/ModWalls.h"
#include "content/tiles/TileTextureHook.h"
#include "content/walls/WallEvents.h"
#include "content/tiles/ModTrees.h"
#include "content/common/BunnyHeadIcon.h"
#include "content/common/PlanetIcon.h"
#include "content/ui/ResourceDraws.h"
#include "content/buffs/ModBuffs.h"
#include "content/projectiles/ModProjectiles.h"
#include "content/npcs/ModBossHeads.h"
#include "content/npcs/ModNpcSave.h"
#include "content/systems/SystemStages.h"
#include "content/npcs/ModNpcs.h"
#include "content/npcs/NpcEvents.h"
#include "content/npcs/ModTownNpcs.h"
#include "content/npcs/TownNpcProfiles.h"
#include "content/npcs/NpcTownHooks.h"
#include "content/npcs/NpcLootHooks.h"
#include "content/projectiles/ProjectileStrikes.h"
#include "content/projectiles/ProjectileDrawSplit.h"
#include "content/dusts/ModDusts.h"
#include "content/common/ContentAssets.h"
#include "content/common/ModContent.h"
#include "content/textures/FontPacks.h"
#include "content/common/ModLanguage.h"
#include "content/common/ModNet.h"
#include "content/common/ServerSyncBridge.h"
#include "core/Config.h"
#include "graphics/ScreenApi.h"
#include "content/common/TypeTables.h"
#include "content/items/ItemDrawStages.h"
#include "content/items/ModEquips.h"
#include "content/items/ModItemData.h"
#include "content/items/ModItemSave.h"
#include "content/items/ModPrefixes.h"
#include "content/items/ModRarities.h"
#include "content/items/ModItems.h"
#include "content/items/VisualItemClone.h"
#include "content/sounds/AndroidAudio.h"
#include "core/Log.h"
#include "hook/CodePatch.h"
#include "hook/HookManager.h"
#include "il2cpp/Api.h"
#include "il2cpp/Resolver.h"
#include "il2cpp/Signature.h"
#include "menu/ModMenu.h"
#include "mods/ModLoader.h"
#include "script/api/Console.h"

#include <dlfcn.h>
#include <sys/stat.h>
#include <unistd.h>

#include <algorithm>
#include <cstdio>
#include <cstdlib>
#include <cstring>
#include <map>
#include <mutex>

namespace bl::dotnet {

namespace {

struct NativeApi {
    uint32_t size;
    void (*log)(int32_t level, const char* text);
    void* (*findClass)(const char* ns, const char* name);
    int32_t (*fieldOffset)(void* cls, const char* name);
    void* (*findMethod)(void* cls, const char* name, int32_t argc);
    void** (*installHook)(void* method, void* replacement);
    void* (*methodPointer)(void* method);
    void* (*newObject)(void* cls);
    void* (*findNestedClass)(void* outer, const char* name);
    void* (*findMethodBySignature)(void* cls, const char* signature);
    int32_t (*checkLayout)(void* method, const int32_t* sizes, int32_t count);
    int32_t (*callGame)(void* fn, CallFrame* frame);
    const char* (*lastCallError)();
    void* (*findField)(void* cls, const char* name);
    void* (*staticFieldAddress)(void* field);
    void (*staticFieldGet)(void* field, void* value);
    void (*staticFieldSet)(void* field, void* value);
    void* (*newString)(const uint16_t* chars, int32_t length);
    int32_t (*classValueSize)(void* cls);
    int32_t (*isInstance)(void* obj, void* cls);
    uint32_t (*gcHandleNew)(void* obj, int32_t pinned);
    void* (*gcHandleTarget)(uint32_t handle);
    void (*gcHandleFree)(uint32_t handle);
    void* (*newArray)(void* elementClass, int32_t length);
    const char* (*className)(void* cls);
    void* (*virtualMethod)(void* obj, void* method);
    int32_t (*registerItem)(const char* mod, const char* name, const char* texture,
                            const char* displayName, const char* tooltip);
    void (*prepareItem)(void* item, int32_t type);
    void (*finishItem)(void* item, int32_t type);
    void (*onItemsInstalled)(void (*callback)(int32_t first, int32_t last));
    void (*onContentReady)(int32_t (*callback)());
    int32_t (*isGameThread)();
    void* (*requestModTexture)(const char* file, const char* assetName);
    void* (*requestVanillaTexture)(const char* name, int32_t* missingOnMobile);
    void* (*textureAssetValue)(void* asset);
    int32_t (*modManifestField)(const char* uid, int32_t field, char* out, int32_t size);
    void* (*languageText)(const char* key, const char* packedTexts, int32_t replace);
    uint32_t (*gcHandleNewWeak)(void* obj);
    void (*onItemData)(int32_t (*save)(void* item, const char* current, void* sink), int32_t (*load)(void* item, const char* data));
    void (*writeString)(void* sink, const char* text);
    int32_t (*netInstall)(void (*handler)(const uint8_t* data, int32_t length, int32_t from));
    int32_t (*netSend)(const uint8_t* data, int32_t length, int32_t toClient, int32_t ignoreClient);
    int32_t (*netMode)();
    void (*netHello)(const char* hello, const char* refusal);
    int32_t (*itemDrawListen)(int32_t kind, int32_t (*pre)(void* args), void (*post)(), uint8_t* active);
    void (*consolePrint)(int32_t kind, const char* text);
    void* (*expandChestItem)(int32_t chest, int32_t slot);
    int32_t (*storeChestItem)(int32_t chest, int32_t slot, void* item);
    int32_t (*allocateRarity)();
    int32_t (*rarityCount)();
    int32_t (*registerPrefix)(const char* mod, const char* name);
    int32_t (*prefixCount)();
    int32_t (*growEquipSets)(const char* kind, int32_t from, int32_t to);
    void* (*resizeArray)(void* array, int32_t length);
    int32_t (*registerBuff)(const char* mod, const char* name, const char* texture, const char* displayName, const char* description);
    void (*onBuffsInstalled)(void (*callback)(int32_t first, int32_t last));
    void* (*buffIconLook)();
    int32_t (*onNaturalRegen)(float (*callback)(void* player, float regen));
    int32_t (*onConsumeBait)(int32_t (*callback)(void* player, void* bait));
    int32_t (*onDyeReward)(int32_t (*callback)(void* player, int32_t* pool, int32_t count, int32_t capacity));
    int32_t (*onResourceDraw)(int32_t (*callback)(int32_t phase, void* spriteBatch, void* args));
    void (*resourceDrawActive)(int32_t active);
    void* (*assetOfTexture)(void* texture);
    int32_t (*registerProjectile)(const char* mod, const char* name, const char* texture, const char* displayName);
    void (*onProjectilesInstalled)(void (*callback)(int32_t first, int32_t last));
    void (*finishProjectile)(void* projectile, int32_t type);
    void (*setProjectileFrames)(int32_t type, int32_t frames);
    int32_t (*registerNPC)(const char* mod, const char* name, const char* texture, const char* displayName);
    void (*onNPCsInstalled)(void (*callback)(int32_t first, int32_t last));
    void (*finishNPC)(void* npc, int32_t type, void* spawnParams);
    void (*setNPCFrames)(int32_t type, int32_t frames);
    int32_t (*npcCount)();
    int32_t (*watchProjectileDamage)();
    void* (*projectileDamaging)();
    int32_t (*beginProjectileDrawSplit)(void* texture, int32_t extras, int32_t sprite);
    void (*endProjectileDrawSplit)();
    int32_t (*npcEvents)(int32_t type, uint32_t events, int32_t (*callback)(int32_t event, void* npc, void* args));
    void (*npcOriginal)(void* npc, int32_t event, int32_t argument);
    void* (*npcImmunityData)(int32_t type);
    int32_t (*registerDust)(const char* mod, const char* name, const char* texture, uint32_t hooks);
    void (*setDustUpdateType)(int32_t type, int32_t updateType);
    void (*setDustCallbacks)(void (*spawn)(int32_t, void*), void (*update)(void**, int32_t, uint8_t*),
                             void (*midUpdate)(void**, int32_t, uint8_t*), void (*preDraw)(void**, int32_t, uint8_t*),
                             int32_t (*getAlpha)(void*, uint32_t*));
    void (*onDustsInstalled)(void (*callback)(int32_t first, int32_t last));
    void* (*dustTextureAsset)(int32_t type);
    int32_t (*reserveBossHead)(const char* texture, const char* assetName, int32_t npcType);
    int32_t (*bossHeadSlotOf)(const char* texture);
    void* (*newParameterObject)(void* method, int32_t parameter);
    int32_t (*installNpcLootHooks)(uint32_t flags, runtime::NpcLootCallback callback);
    int32_t (*reserveTownHead)(int32_t type, int32_t variant, const char* texture, const char* asset);
    int32_t (*townHeadSlot)(int32_t type, int32_t variant);
    int32_t (*installTownNPCHooks)(int32_t type, uint32_t flags, runtime::NpcTownCallback callback);
    int32_t (*installEmoteMenuIcon)();
    void (*setEmoteMenuIcon)(void* texture);
    int32_t (*beginDrawAdjust)(void* texture, int32_t kind, runtime::DrawAdjustFn fn);
    void (*endDrawAdjust)();
    void (*onNpcData)(runtime::ManagedNpcNeeds needs, runtime::ManagedNpcSave save, runtime::ManagedNpcLoad load);
    int32_t (*installTownProfiles)(runtime::TownProfileCallback callback);
    int32_t (*setTownProfileCarrier)(int32_t type, int32_t onlyExisting);
    int32_t (*registerTile)(const char* mod, const char* name, const char* texture);
    void (*onTilesInstalled)(runtime::TilesInstalledHook callback);
    int32_t (*tileMapEntry)(int32_t type, uint32_t color, const char* name);
    void (*tileAnimation)(int32_t type, int32_t height);
    int32_t (*tileEvents)(int32_t type, uint32_t flags, runtime::TileEventCallback callback);
    int32_t (*scanTiles)(int32_t x, int32_t y, int32_t width, int32_t height, uint32_t flags, int32_t* cells, int32_t capacity);
    void (*readTileDrawData)(void* renderer, int32_t x, int32_t y, int32_t offset, int32_t type, void* data, int32_t* halfBrick);
    int32_t (*installSystemStage)(int32_t stage, runtime::SystemStageFn callback);
    int32_t (*hookProfile)(int32_t index, uint64_t* values, const char** name);
    int32_t (*musicRegister)(const char* path);
    void (*musicVolume)(int32_t id, float volume);
    int32_t (*musicState)(int32_t id);
    int32_t (*setWaterStyleCount)(int32_t total);
    int32_t (*beginDrawTint)(void* texture, runtime::DrawTintFn fn);
    void (*endDrawTint)();
    int32_t (*beginDrawCapture)(void* texture, runtime::DrawCaptureFn fn);
    void (*endDrawCapture)();
    int32_t (*soundLoad)(const char* path);
    int32_t (*soundEnqueue)(int32_t group, int32_t id, float left, float right, float rate, int32_t max, int32_t ignore);
    void (*soundCancel)(int32_t handle);
    int32_t (*soundPlaybackState)(int32_t handle);
    int32_t (*soundActive)(int32_t group);
    int32_t (*registerWall)(const char* mod, const char* name, const char* texture);
    void (*onWallsInstalled)(runtime::WallsInstalledHook callback);
    int32_t (*wallMapEntry)(int32_t type, uint32_t color, const char* name);
    int32_t (*wallEvents)(int32_t type, uint32_t flags, runtime::WallEventCallback callback);
    int32_t (*scanWalls)(int32_t x, int32_t y, int32_t width, int32_t height, uint32_t flags, int32_t* cells, int32_t capacity);
    void (*setModTree)(int32_t ground, void* trunk, int32_t topStyle);
    void* (*bunnyHeadTexture)();
    int32_t (*packInfo)(char* out, int32_t size);
    int32_t (*serverSync)(const char* method, const char* arg, char* out, int32_t size);
    void (*netHelloListen)(void (*listener)(int32_t from, const char* hello));
    void (*hostRules)(int32_t rules);
    int32_t (*screenShader)(const char* name, const char* source);
    int32_t (*screenShaderStatus)(int32_t id, char* error, int32_t capacity);
    int32_t (*screenSubmit)(const uint8_t* data, int32_t length);
    int32_t (*screenTexture)(const char* path, int32_t linear, int32_t repeat, int32_t* width, int32_t* height);
    int32_t (*screenSaveNeutralLut)(const char* path, int32_t size);
    int32_t (*screenStats)(int64_t* out, int32_t capacity);
    void* (*requestModFont)(const char* path);
    int32_t (*isCoreReady)();
    void* (*newDelegate)(void* klass, void* fn, void** target);
    void* (*paramClass)(void* method, int32_t parameter);
    void (*setBuffTexts)(int32_t type, const char* displayName, const char* description);
    int32_t (*installTileTextureHook)(int32_t firstType, int32_t secondType, void* (*callback)(int32_t, int32_t, int32_t));
    void** (*installGatedHook)(void* method, void* replacement, int32_t arg, int32_t pointerOffset, int32_t fieldOffset, int32_t fieldSize);
    void (*setItemTooltip)(int32_t type, const char* tooltip);
    void* (*planetTexture)();
    int32_t (*hideUnderworld)(int32_t hide);
    int32_t (*hideOcean)(int32_t hide);
    int32_t (*hideMapFrame)(int32_t hide);
    void (*onVisualItemCopy)(runtime::VisualItemCopyHook hook);
};

struct Extra {
    Il2CppClass* (*classFromType)(const Il2CppType*) = nullptr;
    void* (*stringNewUtf16)(const uint16_t*, int32_t) = nullptr;
    const MethodInfo* (*objectGetVirtualMethod)(Il2CppObject*, const MethodInfo*) = nullptr;
};

const Extra& extra() {
    static Extra e = [] {
        Extra x;
        if (void* lib = dlopen("libil2cpp.so", RTLD_NOLOAD | RTLD_NOW)) {
            x.classFromType = reinterpret_cast<Il2CppClass* (*)(const Il2CppType*)>(dlsym(lib, "il2cpp_class_from_type"));
            x.stringNewUtf16 = reinterpret_cast<void* (*)(const uint16_t*, int32_t)>(dlsym(lib, "il2cpp_string_new_utf16"));
            x.objectGetVirtualMethod = reinterpret_cast<const MethodInfo* (*)(Il2CppObject*, const MethodInfo*)>(
                dlsym(lib, "il2cpp_object_get_virtual_method"));
        }
        return x;
    }();
    return e;
}

void nativeLog(int32_t level, const char* text) {
    if (!text) return;
    if (level >= 2) BL_ERROR("[mod] %s", text);
    else if (level == 1) BL_WARN("[mod] %s", text);
    else BL_INFO("[mod] %s", text);
}

void* nativeFindClass(const char* ns, const char* name) {
    if (!ns || !name) return nullptr;
    return il2cpp::findClassQuiet(ns, name);
}

int32_t nativeFieldOffset(void* cls, const char* name) {
    if (!cls || !name) return -1;
    return il2cpp::fieldOffset(static_cast<Il2CppClass*>(cls), name);
}

void* nativeFindMethod(void* cls, const char* name, int32_t argc) {
    if (!cls || !name) return nullptr;
    auto& a = il2cpp::api();
    return const_cast<MethodInfo*>(a.class_get_method_from_name(static_cast<Il2CppClass*>(cls), name, argc));
}

void** nativeInstallHook(void* method, void* replacement) {
    if (!method || !replacement) return nullptr;
    if (profile::enabled()) return profile::installHook(static_cast<const MethodInfo*>(method), replacement);
    auto** slot = new void*(nullptr);
    if (!hook::install(static_cast<const MethodInfo*>(method), replacement, slot)) {
        delete slot;
        return nullptr;
    }
    return slot;
}

void* nativeMethodPointer(void* method) {
    return method ? il2cpp::methodPointer(static_cast<const MethodInfo*>(method)) : nullptr;
}

void* nativeNewObject(void* cls) {
    return cls ? il2cpp::api().object_new(static_cast<Il2CppClass*>(cls)) : nullptr;
}

void* nativeFindNestedClass(void* outer, const char* name) {
    if (!outer || !name) return nullptr;
    return il2cpp::findNested(static_cast<Il2CppClass*>(outer), name);
}

void* nativeParamClass(void* method, int32_t parameter) {
    const auto* info = static_cast<const MethodInfo*>(method);
    auto& a = il2cpp::api();
    const auto& e = extra();
    if (!info || parameter < 0 || static_cast<uint32_t>(parameter) >= a.method_get_param_count(info) || !e.classFromType) return nullptr;
    return e.classFromType(a.method_get_param(info, static_cast<uint32_t>(parameter)));
}

void* nativeNewDelegate(void* klass, void* fn, void** target) {
    if (!klass || !fn) return nullptr;
    auto& a = il2cpp::api();
    static Il2CppClass* const objectClass = il2cpp::findClass({"System", "Object", {}});
    if (!objectClass) return nullptr;
    auto* cls = static_cast<Il2CppClass*>(klass);
    const MethodInfo* invoke = a.class_get_method_from_name(cls, "Invoke", -1);
    const MethodInfo* ctor = a.class_get_method_from_name(cls, ".ctor", 2);
    if (!invoke || !ctor) return nullptr;
    constexpr size_t kMethodInfoSize = 0x58;
    static std::mutex lock;
    static std::map<std::pair<Il2CppClass*, void*>, void*> forged;
    void* method;
    {
        std::lock_guard<std::mutex> guard(lock);
        void*& slot = forged[{cls, fn}];
        if (!slot) {
            slot = std::calloc(1, kMethodInfoSize);
            if (!slot) return nullptr;
            std::memcpy(slot, invoke, kMethodInfoSize);
            static_cast<void**>(slot)[0] = fn;
            static_cast<void**>(slot)[1] = fn;
        }
        method = slot;
    }
    Il2CppObject* owner = a.object_new(objectClass);
    Il2CppObject* d = a.object_new(cls);
    if (!owner || !d) return nullptr;
    void* args[2] = {owner, &method};
    Il2CppObject* exc = nullptr;
    a.runtime_invoke(ctor, d, args, &exc);
    if (exc) return nullptr;
    if (target) *target = owner;
    return d;
}

void* nativeNewParameterObject(void* method, int32_t parameter) {
    const auto* info = static_cast<const MethodInfo*>(method);
    auto& a = il2cpp::api();
    const auto& e = extra();
    if (!info || parameter < 0 || static_cast<uint32_t>(parameter) >= a.method_get_param_count(info) || !e.classFromType) return nullptr;
    Il2CppClass* cls = e.classFromType(a.method_get_param(info, static_cast<uint32_t>(parameter)));
    if (!cls) return nullptr;
    a.runtime_class_init(cls);
    const MethodInfo* ctor = a.class_get_method_from_name(cls, ".ctor", 0);
    if (!ctor) return nullptr;
    Il2CppObject* value = a.object_new(cls);
    if (!value) return nullptr;
    Il2CppObject* error = nullptr;
    a.runtime_invoke(ctor, value, nullptr, &error);
    return error ? nullptr : value;
}

void* nativeFindMethodBySignature(void* cls, const char* signature) {
    if (!cls || !signature) return nullptr;
    il2cpp::Signature sig = il2cpp::parseSignature(signature);
    bool ambiguous = false;
    const MethodInfo* nameMismatch = nullptr;
    const MethodInfo* m = il2cpp::findMethodBySignature(static_cast<Il2CppClass*>(cls), sig, &ambiguous, &nameMismatch);
    if (!m && nameMismatch) {
        BL_WARN("dotnet: '%s': %s", signature, il2cpp::explainParamNames(nameMismatch, sig).c_str());
    }
    return const_cast<MethodInfo*>(m);
}

int32_t valueSizeOf(const Il2CppType* t) {
    const Extra& e = extra();
    if (!t || !e.classFromType) return -1;
    Il2CppClass* c = e.classFromType(t);
    auto& a = il2cpp::api();
    if (!c || !a.class_is_valuetype(c)) return -1;
    uint32_t align = 0;
    return a.class_value_size(c, &align);
}

int32_t nativeCheckLayout(void* method, const int32_t* sizes, int32_t count) {
    auto& a = il2cpp::api();
    const auto* m = static_cast<const MethodInfo*>(method);
    if (!m || !sizes) return 0;
    const int32_t params = static_cast<int32_t>(a.method_get_param_count(m));
    if (count != params + 1) return 0;
    for (int32_t i = 0; i < count; ++i) {
        if (sizes[i] < 0) continue;
        const Il2CppType* t = i < params ? a.method_get_param(m, static_cast<uint32_t>(i)) : a.method_get_return_type(m);
        if (valueSizeOf(t) != sizes[i]) return i;
    }
    return -1;
}

int32_t nativeCallGame(void* fn, CallFrame* frame) { return callGame(fn, frame); }

const char* nativeLastCallError() { return lastCallError(); }

void* nativeFindField(void* cls, const char* name) {
    if (!cls || !name) return nullptr;
    return il2cpp::findField(static_cast<Il2CppClass*>(cls), name);
}

constexpr size_t kClassStaticFields = 0xB8;
constexpr uint32_t kFieldAttributeStatic = 0x10;
constexpr uint32_t kFieldAttributeLiteral = 0x40;
constexpr size_t kThreadStaticOffset = static_cast<size_t>(-1);

void* staticAddressOf(FieldInfo* f) {
    auto& a = il2cpp::api();
    Il2CppClass* owner = a.field_get_parent(f);
    if (!owner) return nullptr;
    if (a.runtime_class_init) a.runtime_class_init(owner);
    const size_t offset = a.field_get_offset(f);
    if (offset == kThreadStaticOffset) return nullptr;
    auto* statics = *reinterpret_cast<uint8_t**>(reinterpret_cast<uint8_t*>(owner) + kClassStaticFields);
    return statics ? statics + offset : nullptr;
}

bool staticFastPathOk() {
    static const bool ok = [] {
        Il2CppClass* main = il2cpp::findClassQuiet("Terraria", "Main");
        FieldInfo* f = main ? il2cpp::findField(main, "versionNumber") : nullptr;
        if (!f) {
            BL_WARN("dotnet: Main.versionNumber not found; static fields use the slow path");
            return false;
        }
        void* expected = nullptr;
        il2cpp::api().field_static_get_value(f, &expected);
        void* at = staticAddressOf(f);
        const bool same = expected && at && *static_cast<void**>(at) == expected;
        if (!same) BL_WARN("dotnet: Il2CppClass::static_fields is not at 0x%zx; static fields use the slow path", kClassStaticFields);
        return same;
    }();
    return ok;
}

void* nativeStaticFieldAddress(void* field) {
    auto* f = static_cast<FieldInfo*>(field);
    if (!f) return nullptr;
    const uint32_t flags = il2cpp::api().field_get_flags(f);
    if (!(flags & kFieldAttributeStatic) || (flags & kFieldAttributeLiteral)) return nullptr;
    if (!staticFastPathOk()) return nullptr;
    return staticAddressOf(f);
}

void nativeStaticFieldGet(void* field, void* value) {
    if (field && value) il2cpp::api().field_static_get_value(static_cast<FieldInfo*>(field), value);
}

void nativeStaticFieldSet(void* field, void* value) {
    if (field) il2cpp::api().field_static_set_value(static_cast<FieldInfo*>(field), value);
}

void* nativeNewString(const uint16_t* chars, int32_t length) {
    const Extra& e = extra();
    if (!chars || length < 0) return nullptr;
    if (e.stringNewUtf16) return e.stringNewUtf16(chars, length);
    return nullptr;
}

int32_t nativeClassValueSize(void* cls) {
    if (!cls) return -1;
    uint32_t align = 0;
    return il2cpp::api().class_value_size(static_cast<Il2CppClass*>(cls), &align);
}

int32_t nativeIsInstance(void* obj, void* cls) {
    if (!obj || !cls) return 0;
    auto& a = il2cpp::api();
    Il2CppClass* c = a.object_get_class(static_cast<Il2CppObject*>(obj));
    return c && a.class_is_assignable_from(static_cast<Il2CppClass*>(cls), c) ? 1 : 0;
}

uint32_t nativeGcHandleNew(void* obj, int32_t pinned) {
    return obj ? il2cpp::api().gchandle_new(static_cast<Il2CppObject*>(obj), pinned != 0) : 0;
}

void* nativeGcHandleTarget(uint32_t handle) {
    return handle ? il2cpp::api().gchandle_get_target(handle) : nullptr;
}

void nativeGcHandleFree(uint32_t handle) {
    if (handle) il2cpp::api().gchandle_free(handle);
}

void* nativeNewArray(void* elementClass, int32_t length) {
    if (!elementClass || length < 0) return nullptr;
    return il2cpp::api().array_new(static_cast<Il2CppClass*>(elementClass), static_cast<uintptr_t>(length));
}

const char* nativeClassName(void* cls) {
    return cls ? il2cpp::api().class_get_name(static_cast<Il2CppClass*>(cls)) : "";
}

void* nativeVirtualMethod(void* obj, void* method) {
    const Extra& e = extra();
    if (!obj || !method || !e.objectGetVirtualMethod) return method;
    const MethodInfo* m = e.objectGetVirtualMethod(static_cast<Il2CppObject*>(obj), static_cast<const MethodInfo*>(method));
    return const_cast<MethodInfo*>(m ? m : static_cast<const MethodInfo*>(method));
}

bool fileExists(const char* path) {
    struct stat st{};
    return stat(path, &st) == 0 && S_ISREG(st.st_mode);
}

bool textureExists(const char* path) {
    return path && (runtime::content::isVanillaTexturePath(path) || fileExists(path));
}

void noteModForMenu(const std::string& mod) {
    const std::string root = mods::rootOf(mod);
    std::string icon;
    if (!root.empty()) {
        for (const char* name : {"icon_small.png", "icon.png"}) {
            const std::string candidate = root + "/" + name;
            if (fileExists(candidate.c_str())) { icon = candidate; break; }
        }
    }
    runtime::setModMenuInfo(mod, mods::displayName(mod), icon);
}

std::vector<std::pair<std::string, std::string>> cultureTexts(const char* packed) {
    std::vector<std::pair<std::string, std::string>> out;
    if (!packed || !*packed) return out;
    const std::string all = packed;
    if (all.find('\x1F') == std::string::npos) {
        out.push_back({"", all});
        return out;
    }
    size_t start = 0;
    while (start <= all.size()) {
        size_t end = all.find('\x1E', start);
        if (end == std::string::npos) end = all.size();
        const std::string entry = all.substr(start, end - start);
        const size_t sep = entry.find('\x1F');
        if (sep != std::string::npos) out.push_back({entry.substr(0, sep), entry.substr(sep + 1)});
        start = end + 1;
    }
    return out;
}

int32_t nativeRegisterItem(const char* mod, const char* name, const char* texture,
                           const char* displayName, const char* tooltip) {
    if (!mod || !name || !texture || !*mod || !*name) return -1;
    if (*texture && !textureExists(texture)) return -2;
    runtime::ModItemDef d;
    d.mod = mod;
    d.name = name;
    d.texture = texture;
    d.names = cultureTexts(displayName);
    d.tooltips = cultureTexts(tooltip);
    const int type = runtime::registerModItem(std::move(d));
    if (type >= 0) {
        runtime::markManagedItem(type);
        noteModForMenu(mod);
    }
    return type;
}

void nativePrepareItem(void* item, int32_t type) {
    runtime::prepareModItem(static_cast<Il2CppObject*>(item), type);
}

void nativeFinishItem(void* item, int32_t type) {
    runtime::finishModItem(static_cast<Il2CppObject*>(item), type);
}

void nativeOnItemsInstalled(void (*callback)(int32_t first, int32_t last)) {
    runtime::addItemsInstalledHook(reinterpret_cast<runtime::ItemsInstalledHook>(callback));
}

void nativeOnContentReady(int32_t (*callback)()) {
    runtime::addContentReadyHook(callback);
}

int32_t nativeIsGameThread() {
    const int game = runtime::gameThreadId();
    return game != 0 && static_cast<int>(gettid()) == game ? 1 : 0;
}

void* nativeRequestModTexture(const char* file, const char* assetName) {
    if (!file || !assetName || !nativeIsGameThread()) return nullptr;
    static std::map<std::string, uint32_t> loaded;
    auto& a = il2cpp::api();
    auto it = loaded.find(assetName);
    if (it != loaded.end()) {
        if (Il2CppObject* asset = a.gchandle_get_target(it->second)) return asset;
    }
    int w = 0, h = 0;
    Il2CppObject* asset = runtime::content::loadTextureAsset(file, nullptr, 0, assetName, &w, &h);
    if (!asset) return nullptr;
    loaded[assetName] = a.gchandle_new(asset, false);
    return asset;
}

void* nativeRequestVanillaTexture(const char* name, int32_t* missingOnMobile) {
    if (missingOnMobile) *missingOnMobile = 0;
    if (!name || !nativeIsGameThread()) return nullptr;
    bool missing = false;
    Il2CppObject* asset = runtime::content::vanillaTextureAsset(name, &missing);
    if (missingOnMobile) *missingOnMobile = missing ? 1 : 0;
    return asset;
}

void* nativeTextureAssetValue(void* asset) {
    return runtime::content::textureAssetValue(static_cast<Il2CppObject*>(asset));
}

int32_t nativeModManifestField(const char* uid, int32_t field, char* out, int32_t size) {
    const mods::LoadedMod* mod = uid ? mods::find(uid) : nullptr;
    if (!mod) return -1;
    std::string value;
    switch (field) {
        case 0: value = mod->internalName; break;
        case 1: value = mod->displayName; break;
        case 2: value = mod->version; break;
        case 3:
            for (const std::string& dependency : mod->dependencies) {
                if (!value.empty()) value += '\n';
                value += dependency;
            }
            break;
        default: return -1;
    }
    if (out && size > 0) {
        const size_t n = std::min(value.size(), static_cast<size_t>(size - 1));
        std::memcpy(out, value.data(), n);
        out[n] = '\0';
    }
    return static_cast<int32_t>(value.size());
}

void* nativeLanguageText(const char* key, const char* packedTexts, int32_t replace) {
    if (!key || !*key) return nullptr;
    if (!packedTexts) return runtime::modLanguageText(key, nullptr, false);
    runtime::content::CultureNames names = cultureTexts(packedTexts);
    if (names.empty()) names.push_back({"", ""});
    return runtime::modLanguageText(key, &names, replace != 0);
}

uint32_t nativeGcHandleNewWeak(void* obj) {
    auto& a = il2cpp::api();
    return obj && a.gchandle_new_weakref ? a.gchandle_new_weakref(static_cast<Il2CppObject*>(obj), false) : 0;
}

void nativeOnItemData(int32_t (*save)(void*, const char*, void*), int32_t (*load)(void*, const char*)) {
    runtime::setManagedItemData(save, load);
}

void nativeWriteString(void* sink, const char* text) {
    if (sink) static_cast<std::string*>(sink)->assign(text ? text : "");
}

int32_t nativeNetInstall(void (*handler)(const uint8_t*, int32_t, int32_t)) {
    return runtime::installManagedNet(handler) ? 1 : 0;
}

int32_t nativeNetSend(const uint8_t* data, int32_t length, int32_t toClient, int32_t ignoreClient) {
    return runtime::sendManagedPacket(data, length, toClient, ignoreClient) ? 1 : 0;
}

int32_t nativeNetMode() { return runtime::gameNetMode(); }

void nativeNetHello(const char* hello, const char*) {
    runtime::setManagedHello(hello ? hello : "");
}

std::string jsonEscape(const std::string& text) {
    std::string out;
    for (char c : text) {
        if (c == '"' || c == '\\') {
            out += '\\';
            out += c;
        } else if (static_cast<unsigned char>(c) < 0x20) {
            char hex[8];
            std::snprintf(hex, sizeof hex, "\\u%04x", static_cast<unsigned char>(c));
            out += hex;
        } else {
            out += c;
        }
    }
    return out;
}

int32_t copyOut(const std::string& value, char* out, int32_t size) {
    if (out && size > 0) {
        const size_t n = std::min(value.size(), static_cast<size_t>(size - 1));
        std::memcpy(out, value.data(), n);
        out[n] = 0;
    }
    return static_cast<int32_t>(value.size());
}

int32_t nativePackInfo(char* out, int32_t size) {
    std::string json = "{\"mods\":[";
    bool first = true;
    for (size_t i = 0; i < mods::loadedCount(); ++i) {
        const mods::LoadedMod* m = mods::get(static_cast<uint16_t>(i));
        if (!m || !m->enabled) continue;
        if (!first) json += ',';
        first = false;
        json += "{\"uid\":\"" + jsonEscape(m->id) + "\",\"id\":\"" + jsonEscape(m->internalName) + "\",\"name\":\"" +
                jsonEscape(m->displayName) + "\",\"version\":\"" + jsonEscape(m->version) + "\"}";
    }
    json += "],\"textures\":[";
    first = true;
    for (const ModSpec& spec : config().enabledMods) {
        if (spec.entry != "@texture") continue;
        if (!first) json += ',';
        first = false;
        json += "\"" + jsonEscape(spec.id) + "\"";
    }
    json += "]}";
    return copyOut(json, out, size);
}

int32_t nativeServerSync(const char* method, const char* arg, char* out, int32_t size) {
    if (!method) return -1;
    const std::string name = method;
    std::string result;
    bool ok;
    if (name == "offer") {
        const std::string text = arg ? arg : "";
        ok = runtime::callServerSync("offer", "(Ljava/lang/String;)V", &text, nullptr);
    } else if (name == "state") {
        ok = runtime::callServerSync("state", "()Ljava/lang/String;", nullptr, &result);
    } else if (name == "apply" || name == "cancel") {
        ok = runtime::callServerSync(method, "()V", nullptr, nullptr);
    } else {
        return -1;
    }
    return ok ? copyOut(result, out, size) : -1;
}

struct ManagedDraw {
    int32_t (*pre)(void* args) = nullptr;
    void (*post)() = nullptr;
};

ManagedDraw g_managedDraw[2];

bool managedDrawPre(void* user, void* args) { return static_cast<ManagedDraw*>(user)->pre(args) != 0; }

void managedDrawPost(void* user) { static_cast<ManagedDraw*>(user)->post(); }

int32_t nativeItemDrawListen(int32_t kind, int32_t (*pre)(void* args), void (*post)(), uint8_t* active) {
    if (kind < 0 || kind > 1 || !pre || !post || !active || g_managedDraw[kind].pre) return 0;
    g_managedDraw[kind].pre = profile::wrap(pre, kind == 0 ? "ItemDraw.pre" : "ItemDrawInventory.pre");
    g_managedDraw[kind].post = profile::wrap(post, kind == 0 ? "ItemDraw.post" : "ItemDrawInventory.post");
    runtime::content::ItemDrawListener listener;
    listener.active = reinterpret_cast<const std::atomic<uint8_t>*>(active);
    listener.pre = managedDrawPre;
    listener.post = managedDrawPost;
    listener.user = &g_managedDraw[kind];
    if (runtime::content::addItemDrawListener(static_cast<runtime::content::ItemDrawKind>(kind), listener)) return 1;
    g_managedDraw[kind] = ManagedDraw{};
    return 0;
}

void nativeConsolePrint(int32_t kind, const char* text) {
    script::consolePrint(static_cast<char>(kind), text ? text : "");
}

void* nativeExpandChestItem(int32_t chest, int32_t slot) { return runtime::expandChestItem(chest, slot); }

int32_t nativeAllocateRarity() { return runtime::allocateModRarity(); }

int32_t nativeRarityCount() { return runtime::modRarityCount(); }

int32_t nativeRegisterPrefix(const char* mod, const char* name) {
    return mod && name ? runtime::registerModPrefix(mod, name) : -1;
}

int32_t nativePrefixCount() { return runtime::modPrefixCount(); }

int32_t nativeGrowEquipSets(const char* kind, int32_t from, int32_t to) { return runtime::growEquipSets(kind, from, to); }

void* nativeResizeArray(void* array, int32_t length) {
    if (!array || length < 0) return nullptr;
    return runtime::TypeTables::resizedCopy(static_cast<Il2CppArray*>(array), static_cast<uintptr_t>(length));
}

int32_t nativeRegisterBuff(const char* mod, const char* name, const char* texture, const char* displayName, const char* description) {
    if (!mod || !name || !texture || !*mod || !*name) return -1;
    if (!textureExists(texture)) return -2;
    runtime::ModBuffDef d;
    d.mod = mod;
    d.name = name;
    d.texture = texture;
    d.names = cultureTexts(displayName);
    d.descriptions = cultureTexts(description);
    const int type = runtime::registerModBuff(std::move(d));
    if (type >= 0) noteModForMenu(mod);
    return type;
}

void nativeSetBuffTexts(int32_t type, const char* displayName, const char* description) {
    runtime::setModBuffTexts(type, cultureTexts(displayName), cultureTexts(description));
}

void nativeOnBuffsInstalled(void (*callback)(int32_t first, int32_t last)) {
    runtime::setBuffsInstalledHook(reinterpret_cast<runtime::BuffsInstalledHook>(callback));
}

void* nativeBuffIconLook() { return runtime::buffIconLook(); }

int32_t nativeOnNaturalRegen(float (*callback)(void* player, float regen)) {
    return callback && runtime::setNaturalRegenCallback(profile::wrap(callback, "NaturalRegen")) ? 1 : 0;
}

int32_t nativeOnConsumeBait(int32_t (*callback)(void* player, void* bait)) {
    return callback && runtime::setConsumeBaitCallback(callback) ? 1 : 0;
}

int32_t nativeOnDyeReward(int32_t (*callback)(void* player, int32_t* pool, int32_t count, int32_t capacity)) {
    return callback && runtime::setDyeRewardCallback(callback) ? 1 : 0;
}

int32_t nativeOnResourceDraw(int32_t (*callback)(int32_t phase, void* spriteBatch, void* args)) {
    return callback && runtime::setResourceDrawCallback(reinterpret_cast<runtime::ResourceDrawCallback>(profile::wrap(callback, "ResourceDraw"))) ? 1 : 0;
}

void nativeResourceDrawActive(int32_t active) { runtime::setResourceDrawActive(active != 0); }

void* nativeAssetOfTexture(void* texture) { return runtime::assetOfTexture(texture); }

int32_t nativeRegisterProjectile(const char* mod, const char* name, const char* texture, const char* displayName) {
    if (!mod || !name || !texture || !*mod || !*name) return -1;
    if (*texture && !textureExists(texture)) return -2;
    runtime::ModProjectileDef d;
    d.mod = mod;
    d.name = name;
    d.texture = texture;
    d.names = cultureTexts(displayName);
    const int type = runtime::registerModProjectile(std::move(d));
    if (type >= 0) noteModForMenu(mod);
    return type;
}

void nativeOnProjectilesInstalled(void (*callback)(int32_t first, int32_t last)) {
    runtime::setProjectilesInstalledHook(reinterpret_cast<runtime::ProjectilesInstalledHook>(callback));
}

void nativeFinishProjectile(void* projectile, int32_t type) {
    runtime::finishModProjectile(static_cast<Il2CppObject*>(projectile), type);
}

void nativeSetProjectileFrames(int32_t type, int32_t frames) { runtime::setModProjectileFrames(type, frames); }

int32_t nativeRegisterNPC(const char* mod, const char* name, const char* texture, const char* displayName) {
    if (!mod || !name || !texture || !*mod || !*name) return -1;
    if (!textureExists(texture)) return -2;
    runtime::ModNpcDef def;
    def.mod = mod;
    def.name = name;
    def.texture = texture;
    def.names = cultureTexts(displayName);
    const int type = runtime::registerModNpc(std::move(def));
    if (type >= 0) noteModForMenu(mod);
    return type;
}

void nativeOnNPCsInstalled(void (*callback)(int32_t first, int32_t last)) {
    runtime::setNpcsInstalledHook(reinterpret_cast<runtime::NpcsInstalledHook>(callback));
}

void nativeFinishNPC(void* npc, int32_t type, void* spawnParams) {
    runtime::finishModNpc(static_cast<Il2CppObject*>(npc), type, spawnParams);
}

void nativeSetNPCFrames(int32_t type, int32_t frames) { runtime::setModNpcFrames(type, frames); }

int32_t nativeNPCCount() { return runtime::registeredNpcTypeCount(); }

int32_t nativeNPCEvents(int32_t type, uint32_t events, runtime::NpcEventCallback callback) { return runtime::setNpcEvents(type, events, profile::wrap(callback, "NPCEvents")) ? 1 : 0; }

void nativeNPCOriginal(void* npc, int32_t event, int32_t argument) { runtime::runNpcOriginal(static_cast<Il2CppObject*>(npc), event, argument); }

void* nativeNPCImmunityData(int32_t type) { return runtime::npcImmunityData(type); }

int32_t nativeWatchProjectileDamage() { return runtime::watchProjectileDamage() ? 1 : 0; }

void* nativeProjectileDamaging() { return runtime::projectileDamaging(); }

int32_t nativeBeginProjectileDrawSplit(void* texture, int32_t extras, int32_t sprite) {
    return runtime::beginProjectileDrawSplit(texture, extras != 0, sprite != 0) ? 1 : 0;
}

void nativeEndProjectileDrawSplit() { runtime::endProjectileDrawSplit(); }

int32_t nativeRegisterDust(const char* mod, const char* name, const char* texture, uint32_t hooks) {
    if (!mod || !name || !*mod || !*name) return -1;
    runtime::ModDustDef def;
    def.mod = mod;
    def.name = name;
    def.texture = texture ? texture : "";
    if (!def.texture.empty() && !fileExists(def.texture.c_str())) return -2;
    def.hooks = hooks;
    const int type = runtime::registerModDust(std::move(def));
    if (type >= 0) noteModForMenu(mod);
    return type;
}

void nativeSetDustUpdateType(int32_t type, int32_t updateType) { runtime::setModDustUpdateType(type, updateType); }

void nativeSetDustCallbacks(void (*spawn)(int32_t, void*), void (*update)(void**, int32_t, uint8_t*),
                            void (*midUpdate)(void**, int32_t, uint8_t*), void (*preDraw)(void**, int32_t, uint8_t*),
                            int32_t (*getAlpha)(void*, uint32_t*)) {
    runtime::DustNativeCallbacks callbacks;
    callbacks.spawn = profile::wrap(spawn, "Dust.spawn");
    callbacks.update = profile::wrap(update, "Dust.update");
    callbacks.midUpdate = profile::wrap(midUpdate, "Dust.midUpdate");
    callbacks.preDraw = profile::wrap(preDraw, "Dust.preDraw");
    callbacks.getAlpha = profile::wrap(getAlpha, "Dust.getAlpha");
    runtime::setDustNativeCallbacks(callbacks);
}

void nativeOnDustsInstalled(void (*callback)(int32_t first, int32_t last)) {
    runtime::addDustsInstalledHook(reinterpret_cast<runtime::DustsInstalledHook>(callback));
}

void* nativeDustTextureAsset(int32_t type) { return runtime::modDustTextureAsset(type); }

int32_t nativeReserveBossHead(const char* texture, const char* assetName, int32_t npcType) {
    if (!texture || !*texture || !assetName || !fileExists(texture)) return -1;
    return runtime::reserveBossHead(texture, assetName, npcType);
}

int32_t nativeBossHeadSlotOf(const char* texture) { return texture ? runtime::bossHeadSlotOf(texture) : -1; }

int32_t nativeStoreChestItem(int32_t chest, int32_t slot, void* item) {
    return runtime::storeChestItemData(chest, slot, static_cast<Il2CppObject*>(item)) ? 1 : 0;
}

struct CodeSite {
    const char* ns;
    const char* klass;
    const char* method;
    int32_t args;
    uint32_t mask;
    uint32_t pattern;
    uint32_t keep;
    uint32_t replacement;
    int32_t length;
    uint32_t* at;
    uint32_t original;
};

int32_t toggleSites(CodeSite* sites, size_t count, bool on) {
    int32_t done = 1;
    for (size_t s = 0; s < count; ++s) {
        CodeSite& site = sites[s];
        if (!site.at) {
            Il2CppClass* klass = il2cpp::findClass({site.ns, site.klass, {}});
            const MethodInfo* m = klass ? il2cpp::api().class_get_method_from_name(klass, site.method, site.args) : nullptr;
            auto* code = m ? static_cast<uint32_t*>(il2cpp::methodPointer(m)) : nullptr;
            for (int i = 0; code && i < site.length; ++i) {
                if ((code[i] & site.mask) == site.pattern) {
                    site.at = code + i;
                    site.original = code[i];
                    break;
                }
            }
            if (!site.at) {
                BL_ERROR("subworlds: the instruction to change in %s.%s was not found", site.klass, site.method);
                done = 0;
                continue;
            }
        }
        const uint32_t changed = site.replacement | (site.original & site.keep);
        if (!runtime::replaceInstruction(site.at, 0, on ? site.original : changed, on ? changed : site.original)) done = 0;
    }
    return done;
}

int32_t nativeHideUnderworld(int32_t hide) {
    static CodeSite sites[] = {
        {"Terraria", "Main", "DrawBackground", 0, 0xFFFFFC00u, 0x51052800u, 0x3FFu, 0x11100000u, 400, nullptr, 0},
        {"Terraria", "Main", "get_UnderworldLayer", 0, 0xFFFFFC00u, 0x51032000u, 0x3FFu, 0x11000000u, 40, nullptr, 0},
        {"", "GUIMap", "DrawMapFullscreenBackground", 3, 0xFFFFFC00u, 0x513A0000u, 0x3FFu, 0x11100000u, 400, nullptr, 0},
    };
    return toggleSites(sites, sizeof(sites) / sizeof(sites[0]), hide != 0);
}

int32_t nativeHideMapFrame(int32_t hide) {
    static uint32_t* at = nullptr;
    static uint32_t original = 0;
    if (!at) {
        Il2CppClass* map = il2cpp::findClass({"", "GUIMap", {}});
        Il2CppClass* batch = il2cpp::findClass({"Microsoft.Xna.Framework.Graphics", "SpriteBatch", {}});
        const MethodInfo* drawMap = map ? il2cpp::api().class_get_method_from_name(map, "DrawMap", 0) : nullptr;
        const MethodInfo* draw = batch ? il2cpp::findMethodBySignature(batch, il2cpp::parseSignature("void Draw(Texture2D texture, Rectangle destinationRectangle, Color color)")) : nullptr;
        auto* code = drawMap ? static_cast<uint32_t*>(il2cpp::methodPointer(drawMap)) : nullptr;
        const auto target = draw ? reinterpret_cast<uintptr_t>(il2cpp::methodPointer(draw)) : 0;
        for (int i = 0; code && target && i < 6000; ++i) {
            if ((code[i] & 0xFC000000u) != 0x94000000u) continue;
            const int32_t offset = static_cast<int32_t>(code[i] << 6) >> 6;
            if (reinterpret_cast<uintptr_t>(code + i) + static_cast<intptr_t>(offset) * 4 != target) continue;
            at = code + i;
            original = code[i];
            break;
        }
        if (!at) {
            BL_ERROR("subworlds: the map frame of GUIMap.DrawMap was not found");
            return 0;
        }
    }
    constexpr uint32_t nop = 0xD503201Fu;
    return runtime::replaceInstruction(at, 0, hide ? original : nop, hide ? nop : original) ? 1 : 0;
}

int32_t nativeHideOcean(int32_t hide) {
    static CodeSite sites[] = {
        {"Terraria", "WorldGen", "oceanDepths", 2, 0xFF00001Fu, 0x54000005u, 0u, 0xD503201Fu, 40, nullptr, 0},
        {"", "GUIMap", "DrawMapFullscreenBackground", 3, 0xFFFFFC1Fu, 0x7105F01Fu, 0x3FFu, 0x71000000u, 900, nullptr, 0},
        {"", "GUIMap", "DrawMapFullscreenBackground", 3, 0xFFFFFC00u, 0x5105F000u, 0x3FFu, 0x11100000u, 900, nullptr, 0},
    };
    return toggleSites(sites, sizeof(sites) / sizeof(sites[0]), hide != 0);
}

const NativeApi kNativeApi{
    sizeof(NativeApi),
    nativeLog,
    nativeFindClass,
    nativeFieldOffset,
    nativeFindMethod,
    nativeInstallHook,
    nativeMethodPointer,
    nativeNewObject,
    nativeFindNestedClass,
    nativeFindMethodBySignature,
    nativeCheckLayout,
    nativeCallGame,
    nativeLastCallError,
    nativeFindField,
    nativeStaticFieldAddress,
    nativeStaticFieldGet,
    nativeStaticFieldSet,
    nativeNewString,
    nativeClassValueSize,
    nativeIsInstance,
    nativeGcHandleNew,
    nativeGcHandleTarget,
    nativeGcHandleFree,
    nativeNewArray,
    nativeClassName,
    nativeVirtualMethod,
    nativeRegisterItem,
    nativePrepareItem,
    nativeFinishItem,
    nativeOnItemsInstalled,
    nativeOnContentReady,
    nativeIsGameThread,
    nativeRequestModTexture,
    nativeRequestVanillaTexture,
    nativeTextureAssetValue,
    nativeModManifestField,
    nativeLanguageText,
    nativeGcHandleNewWeak,
    nativeOnItemData,
    nativeWriteString,
    nativeNetInstall,
    nativeNetSend,
    nativeNetMode,
    nativeNetHello,
    nativeItemDrawListen,
    nativeConsolePrint,
    nativeExpandChestItem,
    nativeStoreChestItem,
    nativeAllocateRarity,
    nativeRarityCount,
    nativeRegisterPrefix,
    nativePrefixCount,
    nativeGrowEquipSets,
    nativeResizeArray,
    nativeRegisterBuff,
    nativeOnBuffsInstalled,
    nativeBuffIconLook,
    nativeOnNaturalRegen,
    nativeOnConsumeBait,
    nativeOnDyeReward,
    nativeOnResourceDraw,
    nativeResourceDrawActive,
    nativeAssetOfTexture,
    nativeRegisterProjectile,
    nativeOnProjectilesInstalled,
    nativeFinishProjectile,
    nativeSetProjectileFrames,
    nativeRegisterNPC,
    nativeOnNPCsInstalled,
    nativeFinishNPC,
    nativeSetNPCFrames,
    nativeNPCCount,
    nativeWatchProjectileDamage,
    nativeProjectileDamaging,
    nativeBeginProjectileDrawSplit,
    nativeEndProjectileDrawSplit,
    nativeNPCEvents,
    nativeNPCOriginal,
    nativeNPCImmunityData,
    nativeRegisterDust,
    nativeSetDustUpdateType,
    nativeSetDustCallbacks,
    nativeOnDustsInstalled,
    nativeDustTextureAsset,
    nativeReserveBossHead,
    nativeBossHeadSlotOf,
    nativeNewParameterObject,
    [](uint32_t flags, runtime::NpcLootCallback callback) -> int32_t { return runtime::installNpcLootHooks(flags, callback) ? 1 : 0; },
    [](int32_t type, int32_t variant, const char* texture, const char* asset) -> int32_t {
        if (!texture || !*texture || !asset || !fileExists(texture)) return 0;
        return runtime::setModNpcHead(type, variant, texture, asset);
    },
    [](int32_t type, int32_t variant) -> int32_t { return runtime::modNpcHeadSlot(type, variant); },
    [](int32_t type, uint32_t flags, runtime::NpcTownCallback callback) -> int32_t { return runtime::installNpcTownHooks(type, flags, profile::wrap(callback, "NPCTown")) ? 1 : 0; },
    []() -> int32_t { return runtime::installEmoteMenuIcon() ? 1 : 0; },
    [](void* texture) { runtime::setEmoteMenuIcon(texture); },
    [](void* texture, int32_t kind, runtime::DrawAdjustFn fn) -> int32_t { return runtime::beginDrawAdjust(texture, kind, profile::wrap(fn, "DrawAdjust")) ? 1 : 0; },
    []() { runtime::endDrawAdjust(); },
    [](runtime::ManagedNpcNeeds needs, runtime::ManagedNpcSave save, runtime::ManagedNpcLoad load) { runtime::setManagedNpcData(needs, save, load); },
    [](runtime::TownProfileCallback callback) -> int32_t { return runtime::installTownProfiles(profile::wrap(callback, "TownProfiles")) ? 1 : 0; },
    [](int32_t type, int32_t onlyExisting) -> int32_t { return runtime::setTownProfileCarrier(type, onlyExisting != 0) ? 1 : 0; },
    [](const char* mod, const char* name, const char* texture) -> int32_t {
        if (!mod || !name || !texture || !fileExists(texture)) return -2;
        return runtime::registerModTile({mod, name, texture});
    },
    runtime::setManagedTilesInstalledHook,
    [](int32_t type, uint32_t color, const char* name) -> int32_t {
        return runtime::addModTileMapEntry(type, color & 255, (color >> 8) & 255, (color >> 16) & 255, name ? name : "");
    },
    runtime::setModTileAnimation,
    [](int32_t type, uint32_t flags, runtime::TileEventCallback callback) -> int32_t { return runtime::installTileEvents(type, flags, profile::wrap(callback, "TileEvents")) ? 1 : 0; },
    runtime::scanTileEvents,
    runtime::readManagedTileDrawData,
    [](int32_t stage, runtime::SystemStageFn callback) -> int32_t {
        return runtime::installSystemStage(stage, profile::wrap(callback, "SystemStage")) ? 1 : 0;
    },
    profile::read,
    [](const char* path) -> int32_t {
        if (!path || !fileExists(path)) return 0;
        return runtime::content::registerMusic(path);
    },
    [](int32_t id, float volume) { runtime::content::setMusicVolume(id, volume); },
    [](int32_t id) -> int32_t { return runtime::content::musicState(id); },
    [](int32_t total) -> int32_t { return runtime::setWaterStyleCount(total) ? 1 : 0; },
    [](void* texture, runtime::DrawTintFn fn) -> int32_t { return runtime::beginDrawTint(texture, profile::wrap(fn, "DrawTint")) ? 1 : 0; },
    []() { runtime::endDrawTint(); },
    [](void* texture, runtime::DrawCaptureFn fn) -> int32_t { return runtime::beginDrawCapture(texture, profile::wrap(fn, "DrawCapture")) ? 1 : 0; },
    []() { runtime::endDrawCapture(); },
    [](const char* path) -> int32_t {
        if (!path || !fileExists(path)) return 0;
        return runtime::content::loadSound(path);
    },
    [](int32_t group, int32_t id, float left, float right, float rate, int32_t max, int32_t ignore) -> int32_t {
        return runtime::content::enqueueSound(group, id, left, right, rate, max, ignore != 0);
    },
    [](int32_t handle) { runtime::content::cancelSound(handle); },
    [](int32_t handle) -> int32_t { return runtime::content::playbackState(handle); },
    [](int32_t group) -> int32_t { return runtime::content::activeSound(group); },
    [](const char* mod, const char* name, const char* texture) -> int32_t {
        if (!mod || !name || !texture || !fileExists(texture)) return -2;
        runtime::ModWallDef d;
        d.mod = mod;
        d.name = name;
        d.texture = texture;
        return runtime::registerModWall(std::move(d));
    },
    runtime::setManagedWallsInstalledHook,
    [](int32_t type, uint32_t color, const char* name) -> int32_t {
        return runtime::addModWallMapEntry(type, color & 255, (color >> 8) & 255, (color >> 16) & 255, name ? name : "");
    },
    [](int32_t type, uint32_t flags, runtime::WallEventCallback callback) -> int32_t { return runtime::installWallEvents(type, flags, profile::wrap(callback, "WallEvents")) ? 1 : 0; },
    runtime::scanWallEvents,
    [](int32_t ground, void* trunk, int32_t topStyle) { runtime::setModTree(ground, static_cast<Il2CppObject*>(trunk), topStyle); },
    []() -> void* {
        static uint32_t handle = 0;
        if (handle) return il2cpp::api().gchandle_get_target(handle);
        int w = 0, h = 0;
        Il2CppObject* asset = runtime::content::loadTextureAsset("", bl_bunny_head_png, bl_bunny_head_png_len, "bunny:bunnyHead", &w, &h);
        if (asset) handle = il2cpp::api().gchandle_new(asset, false);
        return asset;
    },
    nativePackInfo,
    nativeServerSync,
    [](void (*listener)(int32_t from, const char* hello)) { runtime::setManagedHelloListener(listener); },
    runtime::setHostRules,
    graphics::screenShader,
    graphics::screenShaderStatus,
    graphics::screenSubmit,
    graphics::screenTexture,
    graphics::screenSaveNeutralLut,
    graphics::screenStats,
    [](const char* path) -> void* {
        if (!path || !fileExists(path)) return nullptr;
        return runtime::loadModFont(path);
    },
    []() -> int32_t { return runtime::isCoreReady() ? 1 : 0; },
    nativeNewDelegate,
    nativeParamClass,
    nativeSetBuffTexts,
    [](int32_t firstType, int32_t secondType, void* (*callback)(int32_t, int32_t, int32_t)) -> int32_t {
        return runtime::installTileTextureHook(firstType, secondType, callback) ? 1 : 0;
    },
    [](void* method, void* replacement, int32_t arg, int32_t pointerOffset, int32_t fieldOffset, int32_t fieldSize) -> void** {
        return gate::installHook(static_cast<const MethodInfo*>(method), replacement, arg, pointerOffset, fieldOffset, fieldSize);
    },
    [](int32_t type, const char* tooltip) {
        if (tooltip) runtime::setModItemTooltip(type, cultureTexts(tooltip));
    },
    []() -> void* {
        static uint32_t handle = 0;
        if (handle) return il2cpp::api().gchandle_get_target(handle);
        int w = 0, h = 0;
        Il2CppObject* asset = runtime::content::loadTextureAsset("", bl_planet_png, bl_planet_png_len, "bunny:planet", &w, &h);
        if (asset) handle = il2cpp::api().gchandle_new(asset, false);
        return asset;
    },
    nativeHideUnderworld,
    nativeHideOcean,
    nativeHideMapFrame,
    [](runtime::VisualItemCopyHook hook) { runtime::setVisualItemCopyHook(hook ? profile::wrap(hook, "VisualItemCopy") : nullptr); },
};

}

const void* nativeApiTable() { return &kNativeApi; }

}
