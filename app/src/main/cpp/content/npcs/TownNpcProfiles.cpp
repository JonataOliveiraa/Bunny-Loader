#include "content/npcs/TownNpcProfiles.h"
#include "core/Log.h"
#include "hook/HookManager.h"
#include "il2cpp/Api.h"
#include "il2cpp/Resolver.h"

#include <map>
#include <mutex>

namespace bl::runtime {

namespace {

enum Kind : int32_t { kRoll = 0, kName = 1, kTexture = 2, kHead = 3 };
constexpr const char* kMethods[] = {"RollVariation", "GetNameForVariant", "GetTextureNPCShouldUse", "GetHeadTextureIndex"};
constexpr int32_t kArgs[] = {0, 1, 1, 1};

struct Carrier {
    int32_t type = 0;
    Il2CppObject* original = nullptr;
    const MethodInfo* methods[4] = {};
};

std::mutex g_mx;
std::map<Il2CppObject*, Carrier> g_carriers;
TownProfileCallback g_callback = nullptr;
bool g_installed = false;

Il2CppClass* g_carrierClass = nullptr;
Il2CppClass* g_profilesClass = nullptr;
FieldInfo* g_instance = nullptr;
int32_t g_dictionary = -1;
int32_t g_givenName = -1;
FieldInfo* g_npcTextures = nullptr;
const MethodInfo* g_defaultHead = nullptr;

using RollFn = int32_t (*)(Il2CppObject*, const MethodInfo*);
using ObjectFn = Il2CppObject* (*)(Il2CppObject*, Il2CppObject*, const MethodInfo*);
using HeadFn = int32_t (*)(Il2CppObject*, Il2CppObject*, const MethodInfo*);
RollFn g_origRoll = nullptr;
ObjectFn g_origName = nullptr;
ObjectFn g_origTexture = nullptr;
HeadFn g_origHead = nullptr;

bool find(Il2CppObject* self, Carrier& out) {
    std::lock_guard<std::mutex> l(g_mx);
    auto it = g_carriers.find(self);
    if (it == g_carriers.end()) return false;
    out = it->second;
    return true;
}

bool managed(int32_t kind, const Carrier& c, Il2CppObject* npc, intptr_t& result) {
    return g_callback && g_callback(kind, c.type, npc, &result) != 0;
}

template <typename T>
T readStatic(FieldInfo* f) {
    T v{};
    il2cpp::api().field_static_get_value(f, &v);
    return v;
}

int32_t hkRoll(Il2CppObject* self, const MethodInfo* m) {
    Carrier c;
    if (!find(self, c)) return g_origRoll(self, m);
    intptr_t result = 0;
    if (managed(kRoll, c, nullptr, result)) return static_cast<int32_t>(result);
    if (c.original && c.methods[kRoll]) return reinterpret_cast<RollFn>(il2cpp::methodPointer(c.methods[kRoll]))(c.original, c.methods[kRoll]);
    return 0;
}

Il2CppObject* hkName(Il2CppObject* self, Il2CppObject* npc, const MethodInfo* m) {
    Carrier c;
    if (!find(self, c)) return g_origName(self, npc, m);
    intptr_t result = 0;
    if (managed(kName, c, npc, result) && result) return reinterpret_cast<Il2CppObject*>(result);
    if (c.original && c.methods[kName]) return reinterpret_cast<ObjectFn>(il2cpp::methodPointer(c.methods[kName]))(c.original, npc, c.methods[kName]);
    return npc ? *reinterpret_cast<Il2CppObject**>(reinterpret_cast<uint8_t*>(npc) + g_givenName) : nullptr;
}

Il2CppObject* hkTexture(Il2CppObject* self, Il2CppObject* npc, const MethodInfo* m) {
    Carrier c;
    if (!find(self, c)) return g_origTexture(self, npc, m);
    intptr_t result = 0;
    if (managed(kTexture, c, npc, result) && result) return reinterpret_cast<Il2CppObject*>(result);
    if (c.original && c.methods[kTexture]) return reinterpret_cast<ObjectFn>(il2cpp::methodPointer(c.methods[kTexture]))(c.original, npc, c.methods[kTexture]);
    Il2CppArray* textures = readStatic<Il2CppArray*>(g_npcTextures);
    if (!textures || static_cast<uintptr_t>(c.type) >= textures->length) return nullptr;
    return static_cast<Il2CppObject**>(arrayData(textures))[c.type];
}

int32_t hkHead(Il2CppObject* self, Il2CppObject* npc, const MethodInfo* m) {
    Carrier c;
    if (!find(self, c)) return g_origHead(self, npc, m);
    intptr_t result = 0;
    if (managed(kHead, c, npc, result)) return static_cast<int32_t>(result);
    if (c.original && c.methods[kHead]) return reinterpret_cast<HeadFn>(il2cpp::methodPointer(c.methods[kHead]))(c.original, npc, c.methods[kHead]);
    return reinterpret_cast<int32_t (*)(int32_t, const MethodInfo*)>(il2cpp::methodPointer(g_defaultHead))(c.type, g_defaultHead);
}

Il2CppObject* dictionary() {
    il2cpp::api().runtime_class_init(g_profilesClass);
    Il2CppObject* instance = readStatic<Il2CppObject*>(g_instance);
    return instance ? *reinterpret_cast<Il2CppObject**>(reinterpret_cast<uint8_t*>(instance) + g_dictionary) : nullptr;
}

} // namespace

bool installTownProfiles(TownProfileCallback callback) {
    g_callback = callback;
    if (g_installed) return true;
    auto& a = il2cpp::api();
    g_carrierClass = il2cpp::findClass({"Terraria.GameContent", "Profiles", "VariantNPCProfile"});
    g_profilesClass = il2cpp::findClass({"Terraria.GameContent", "TownNPCProfiles", {}});
    Il2CppClass* npc = il2cpp::findClass({"Terraria", "NPC", {}});
    Il2CppClass* textures = il2cpp::findClass({"Terraria.GameContent", "TextureAssets", {}});
    if (!g_carrierClass || !g_profilesClass || !npc || !textures) return false;
    g_instance = il2cpp::findField(g_profilesClass, "Instance");
    g_dictionary = il2cpp::fieldOffset(g_profilesClass, "_townNPCProfiles");
    g_givenName = il2cpp::fieldOffset(npc, "_givenName");
    g_npcTextures = il2cpp::findField(textures, "Npc");
    g_defaultHead = a.class_get_method_from_name(npc, "TypeToDefaultHeadIndex", 1);
    const MethodInfo* methods[4];
    for (int k = 0; k < 4; ++k) methods[k] = a.class_get_method_from_name(g_carrierClass, kMethods[k], kArgs[k]);
    if (!g_instance || g_dictionary < 0 || g_givenName < 0 || !g_npcTextures || !g_defaultHead || !methods[0] || !methods[1] ||
        !methods[2] || !methods[3]) {
        BL_ERROR("town NPC profiles: game members not found");
        return false;
    }
    if (!hook::install(methods[kRoll], hkRoll, &g_origRoll) || !hook::install(methods[kName], hkName, &g_origName) ||
        !hook::install(methods[kTexture], hkTexture, &g_origTexture) || !hook::install(methods[kHead], hkHead, &g_origHead)) {
        BL_ERROR("town NPC profiles: hook failed");
        return false;
    }
    g_installed = true;
    return true;
}

bool setTownProfileCarrier(int32_t type, bool onlyExisting) {
    if (!g_installed) return false;
    auto& a = il2cpp::api();
    Il2CppObject* dict = dictionary();
    if (!dict) return false;
    Il2CppClass* dictClass = a.object_get_class(dict);
    const MethodInfo* tryGet = a.class_get_method_from_name(dictClass, "TryGetValue", 2);
    const MethodInfo* setItem = a.class_get_method_from_name(dictClass, "set_Item", 2);
    if (!tryGet || !setItem) return false;
    Il2CppObject* original = nullptr;
    int32_t key = type;
    void* getArgs[2] = {&key, &original};
    Il2CppObject* exc = nullptr;
    a.runtime_invoke(tryGet, dict, getArgs, &exc);
    if (exc || (onlyExisting && !original)) return false;
    {
        std::lock_guard<std::mutex> l(g_mx);
        if (original && g_carriers.count(original)) return true;
    }
    Carrier carrier;
    carrier.type = type;
    carrier.original = original;
    if (original) {
        a.gchandle_new(original, false);
        Il2CppClass* originalClass = a.object_get_class(original);
        for (int k = 0; k < 4; ++k) carrier.methods[k] = a.class_get_method_from_name(originalClass, kMethods[k], kArgs[k]);
    }
    Il2CppObject* object = a.object_new(g_carrierClass);
    if (!object) return false;
    a.gchandle_new(object, false);
    {
        std::lock_guard<std::mutex> l(g_mx);
        g_carriers[object] = carrier;
    }
    void* setArgs[2] = {&key, object};
    a.runtime_invoke(setItem, dict, setArgs, &exc);
    if (exc) {
        std::lock_guard<std::mutex> l(g_mx);
        g_carriers.erase(object);
        return false;
    }
    return true;
}

} // namespace bl::runtime
