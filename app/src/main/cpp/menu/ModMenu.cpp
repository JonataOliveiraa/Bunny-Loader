#include "menu/ModMenu.h"
#include "content/buffs/ModBuffs.h"
#include "content/items/ModItems.h"
#include "content/npcs/ModNpcs.h"

#include <mutex>

namespace bl::runtime {

namespace {

struct ModInfo {
    std::string mod, name, icon;
};

std::mutex g_mx;
std::vector<ModInfo> g_mods;

ModInfo& infoOf(const std::string& mod) {
    for (ModInfo& m : g_mods) if (m.mod == mod) return m;
    g_mods.push_back({mod, {}, {}});
    return g_mods.back();
}

}

void setModMenuInfo(const std::string& mod, const std::string& name, const std::string& icon) {
    std::lock_guard<std::mutex> l(g_mx);
    ModInfo& m = infoOf(mod);
    if (m.name.empty()) m.name = name;
    if (m.icon.empty()) m.icon = icon;
}

std::vector<ModMenuFolder> modMenuFolders() {
    const std::vector<ModItemInfo> items = modItems();
    const std::vector<ModNpcInfo> npcs = modNpcs();
    const std::vector<ModBuffInfo> buffs = modBuffs();

    std::lock_guard<std::mutex> l(g_mx);
    std::vector<ModMenuFolder> out;
    for (const ModInfo& m : g_mods) {
        ModMenuFolder all;
        all.mod = m.mod;
        all.modName = m.name.empty() ? m.mod : m.name;
        all.modIcon = m.icon;

        ModMenuFolder itemsFolder = all;
        itemsFolder.name = "Itens";
        for (const ModItemInfo& i : items) {
            if (i.mod == m.mod) itemsFolder.types.push_back(i.type);
        }
        if (!itemsFolder.types.empty()) out.push_back(std::move(itemsFolder));

        ModMenuFolder npcsFolder = all;
        npcsFolder.name = "NPCs";
        npcsFolder.npc = true;
        for (const ModNpcInfo& n : npcs) {
            if (n.mod == m.mod) npcsFolder.types.push_back(n.type);
        }
        if (!npcsFolder.types.empty()) out.push_back(std::move(npcsFolder));

        ModMenuFolder buffsFolder = all;
        buffsFolder.name = "Buffs";
        buffsFolder.buff = true;
        for (const ModBuffInfo& b : buffs) {
            if (b.mod == m.mod) buffsFolder.types.push_back(b.type);
        }
        if (!buffsFolder.types.empty()) out.push_back(std::move(buffsFolder));
    }
    return out;
}

}
