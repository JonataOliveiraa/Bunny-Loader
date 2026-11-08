#include "content/items/VisualItemClone.h"
#include "core/Log.h"
#include "hook/HookManager.h"
#include "il2cpp/Api.h"
#include "il2cpp/Resolver.h"
#include "il2cpp/Signature.h"
#include <cstdint>
#include <unordered_map>

namespace bl::runtime {
namespace {

using CloneFn = Il2CppObject* (*)(Il2CppObject*, const MethodInfo*);
using ClientCloneFn = Il2CppObject* (*)(Il2CppObject*, Il2CppObject*, const MethodInfo*);
using ItemCheckFn = void (*)(Il2CppObject*, const MethodInfo*);
using HeldItemFn = Il2CppObject* (*)(Il2CppObject*, const MethodInfo*);

struct Visual {
    Il2CppObject* item = nullptr;
    int32_t type = 0;
    uint8_t prefix = 0;
};

ClientCloneFn g_origClientClone = nullptr;
ItemCheckFn g_origItemCheck = nullptr;
const MethodInfo* g_clone = nullptr;
const MethodInfo* g_clientClone = nullptr;
const MethodInfo* g_heldItem = nullptr;
int32_t g_visualOffset = -1, g_typeOffset = -1, g_prefixOffset = -1;
int32_t g_animationOffset = -1, g_useStyleOffset = -1, g_holdStyleOffset = -1;
thread_local Il2CppObject* t_player = nullptr;
std::unordered_map<Il2CppObject*, Visual> g_visuals;

template <typename T>
T& at(Il2CppObject* object, int32_t offset) {
    return *reinterpret_cast<T*>(reinterpret_cast<uint8_t*>(object) + offset);
}

void refreshOnUseStart(Il2CppObject* self, int32_t before) {
    const int32_t animation = at<int32_t>(self, g_animationOffset);
    if (animation <= 0 || animation <= before) return;
    Il2CppObject* held = reinterpret_cast<HeldItemFn>(il2cpp::methodPointer(g_heldItem))(self, g_heldItem);
    Il2CppObject* visual = at<Il2CppObject*>(self, g_visualOffset);
    if (!held || !visual || held == visual || at<int32_t>(held, g_typeOffset) != at<int32_t>(visual, g_typeOffset)) return;
    if (at<int32_t>(held, g_useStyleOffset) == at<int32_t>(visual, g_useStyleOffset) &&
        at<int32_t>(held, g_holdStyleOffset) == at<int32_t>(visual, g_holdStyleOffset)) return;
    g_origClientClone(held, visual, g_clientClone);
}

void hkItemCheck(Il2CppObject* self, const MethodInfo* method) {
    Il2CppObject* outer = t_player;
    t_player = self;
    const int32_t before = g_heldItem ? at<int32_t>(self, g_animationOffset) : 0;
    g_origItemCheck(self, method);
    if (g_heldItem) refreshOnUseStart(self, before);
    t_player = outer;
}

Il2CppObject* hkClientClone(Il2CppObject* self, Il2CppObject* destination, const MethodInfo* method) {
    Il2CppObject* result = g_origClientClone(self, destination, method);
    Il2CppObject* player = t_player;
    if (!player || !self || !destination || at<Il2CppObject*>(player, g_visualOffset) != destination) return result;
    const int32_t type = at<int32_t>(self, g_typeOffset);
    const uint8_t prefix = at<uint8_t>(self, g_prefixOffset);
    Visual& visual = g_visuals[player];
    if (visual.item == destination && visual.type == type && visual.prefix == prefix) return result;
    Il2CppObject* full = reinterpret_cast<CloneFn>(il2cpp::methodPointer(g_clone))(self, g_clone);
    if (!full) return result;
    il2cpp::api().gc_wbarrier_set_field(player, reinterpret_cast<void**>(reinterpret_cast<uint8_t*>(player) + g_visualOffset), full);
    visual = {full, type, prefix};
    return full;
}

}

void installVisualItemClone() {
    Il2CppClass* item = il2cpp::findClass({"Terraria", "Item", {}});
    Il2CppClass* player = il2cpp::findClass({"Terraria", "Player", {}});
    if (!item || !player || !il2cpp::api().gc_wbarrier_set_field) {
        BL_ERROR("visual item clone: Item or Player not found");
        return;
    }
    g_visualOffset = il2cpp::fieldOffset(player, "lastVisualizedSelectedItem");
    g_typeOffset = il2cpp::fieldOffset(item, "type");
    g_prefixOffset = il2cpp::fieldOffset(item, "prefix");
    g_clone = il2cpp::findMethodBySignature(item, il2cpp::parseSignature("Item Clone()"));
    const MethodInfo* clientClone = il2cpp::findMethodBySignature(item, il2cpp::parseSignature("Item clientClone(Item cloneDestination)"));
    g_clientClone = clientClone;
    g_animationOffset = il2cpp::fieldOffset(player, "itemAnimation");
    g_useStyleOffset = il2cpp::fieldOffset(item, "useStyle");
    g_holdStyleOffset = il2cpp::fieldOffset(item, "holdStyle");
    const MethodInfo* heldItem = il2cpp::findMethodBySignature(player, il2cpp::parseSignature("Item get_HeldItem()"));
    if (heldItem && g_animationOffset >= 0 && g_useStyleOffset >= 0 && g_holdStyleOffset >= 0) g_heldItem = heldItem;
    else BL_ERROR("visual item clone: the use start refresh is off (members not found)");
    const MethodInfo* itemCheck = il2cpp::findMethodBySignature(player, il2cpp::parseSignature("void ItemCheck()"));
    if (g_visualOffset < 0 || g_typeOffset < 0 || g_prefixOffset < 0 || !g_clone || !clientClone || !itemCheck) {
        BL_ERROR("visual item clone: members not found");
        return;
    }
    if (!hook::install(itemCheck, hkItemCheck, &g_origItemCheck) || !hook::install(clientClone, hkClientClone, &g_origClientClone)) {
        BL_ERROR("visual item clone: hooks not installed");
    }
}

}
