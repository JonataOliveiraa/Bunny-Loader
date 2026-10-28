#include "content/items/ModItemData.h"
#include "content/items/ModItems.h"
#include "il2cpp/Api.h"
#include "il2cpp/Resolver.h"
#include "script/api/Items.h"

#include <atomic>
#include <mutex>
#include <string>
#include <unordered_map>
#include <vector>

namespace bl::runtime {

namespace {

std::atomic<ManagedItemSave> g_save{nullptr};
std::atomic<ManagedItemLoad> g_load{nullptr};
std::mutex g_mx;
std::vector<bool> g_managed;

struct UnloadedData {
    uint32_t handle = 0;
    int type = 0;
    std::string data;
};

std::mutex g_unloadedMx;
std::unordered_map<Il2CppObject*, UnloadedData> g_unloaded;
std::atomic<size_t> g_unloadedCount{0};

void pruneUnloaded() {
    auto& a = il2cpp::api();
    for (auto it = g_unloaded.begin(); it != g_unloaded.end();) {
        if (a.gchandle_get_target && a.gchandle_get_target(it->second.handle) == it->first) {
            ++it;
            continue;
        }
        if (a.gchandle_free && it->second.handle) a.gchandle_free(it->second.handle);
        it = g_unloaded.erase(it);
    }
}

bool unloadedSave(Il2CppObject* item, int type, std::string& data) {
    auto& a = il2cpp::api();
    std::lock_guard<std::mutex> l(g_unloadedMx);
    const auto it = g_unloaded.find(item);
    if (it == g_unloaded.end()) return true;
    const bool alive = a.gchandle_get_target && a.gchandle_get_target(it->second.handle) == item;
    if (alive && it->second.type == type) data = it->second.data;
    return true;
}

bool unloadedLoad(Il2CppObject* item, int type, const std::string& data) {
    auto& a = il2cpp::api();
    if (!a.gchandle_new_weakref) return false;
    std::lock_guard<std::mutex> l(g_unloadedMx);
    if (g_unloaded.size() >= 4096) pruneUnloaded();
    UnloadedData& entry = g_unloaded[item];
    if (entry.handle && a.gchandle_free) a.gchandle_free(entry.handle);
    entry.handle = data.empty() ? 0 : a.gchandle_new_weakref(item, false);
    entry.type = type;
    entry.data = data;
    if (!entry.handle) g_unloaded.erase(item);
    g_unloadedCount.store(g_unloaded.size(), std::memory_order_relaxed);
    return true;
}

bool nativeUnloaded(int type) { return isUnloadedType(type) && !script::itemDataEngineReady(); }

int typeOf(Il2CppObject* item) {
    static int32_t offset = -1;
    if (offset < 0) offset = il2cpp::fieldOffset(item->klass, "type");
    return offset < 0 ? 0 : *reinterpret_cast<int32_t*>(reinterpret_cast<char*>(item) + offset);
}

bool isManaged(int type) {
    std::lock_guard<std::mutex> l(g_mx);
    return type >= 0 && static_cast<size_t>(type) < g_managed.size() && g_managed[static_cast<size_t>(type)];
}

}

void setManagedItemData(ManagedItemSave save, ManagedItemLoad load) {
    g_save.store(save, std::memory_order_release);
    g_load.store(load, std::memory_order_release);
}

void markManagedItem(int type) {
    if (type < 0) return;
    std::lock_guard<std::mutex> l(g_mx);
    if (static_cast<size_t>(type) >= g_managed.size()) g_managed.resize(static_cast<size_t>(type) + 1, false);
    g_managed[static_cast<size_t>(type)] = true;
}

bool saveModItemData(Il2CppObject* item, std::string& data) {
    if (!item) return false;
    const int type = typeOf(item);
    if (nativeUnloaded(type)) return unloadedSave(item, type, data);
    if (!isManaged(type)) return script::saveModItemData(item, data);
    ManagedItemSave save = g_save.load(std::memory_order_acquire);
    if (!save) return true;
    const std::string current = data;
    return save(item, current.c_str(), &data) != 0;
}

bool loadModItemData(Il2CppObject* item, const std::string& data) {
    if (!item) return false;
    const int type = typeOf(item);
    if (nativeUnloaded(type)) return unloadedLoad(item, type, data);
    if (!isManaged(type)) return script::loadModItemData(item, data);
    ManagedItemLoad load = g_load.load(std::memory_order_acquire);
    return !load || load(item, data.c_str()) != 0;
}

void copyUnloadedItemData(Il2CppObject* from, Il2CppObject* to) {
    if (!from || !to || from == to || g_unloadedCount.load(std::memory_order_relaxed) == 0) return;
    const int type = typeOf(to);
    if (!nativeUnloaded(type)) return;
    std::string data;
    {
        auto& a = il2cpp::api();
        std::lock_guard<std::mutex> l(g_unloadedMx);
        const auto it = g_unloaded.find(from);
        if (it == g_unloaded.end() || it->second.type != type || !a.gchandle_get_target ||
            a.gchandle_get_target(it->second.handle) != from) return;
        data = it->second.data;
    }
    unloadedLoad(to, type, data);
}

}
