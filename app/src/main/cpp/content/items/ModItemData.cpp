#include "content/items/ModItemData.h"
#include "il2cpp/Resolver.h"
#include "script/api/Items.h"

#include <atomic>
#include <mutex>
#include <vector>

namespace bl::runtime {

namespace {

std::atomic<ManagedItemSave> g_save{nullptr};
std::atomic<ManagedItemLoad> g_load{nullptr};
std::mutex g_mx;
std::vector<bool> g_managed;

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
    if (!isManaged(typeOf(item))) return script::saveModItemData(item, data);
    ManagedItemSave save = g_save.load(std::memory_order_acquire);
    if (!save) return true;
    const std::string current = data;
    return save(item, current.c_str(), &data) != 0;
}

bool loadModItemData(Il2CppObject* item, const std::string& data) {
    if (!item) return false;
    if (!isManaged(typeOf(item))) return script::loadModItemData(item, data);
    ManagedItemLoad load = g_load.load(std::memory_order_acquire);
    return !load || load(item, data.c_str()) != 0;
}

}
