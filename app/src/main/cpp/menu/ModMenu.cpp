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
std::vector<ModInfo> g_mods;              // na ordem em que apareceram
std::vector<ModMenuFolder> g_custom;      // as pastas que os mods criaram
std::unordered_set<int> g_hidden[3];      // por MenuKind: fora do menu

ModInfo& infoOf(const std::string& mod) {
    for (ModInfo& m : g_mods) if (m.mod == mod) return m;
    g_mods.push_back({mod, {}, {}});
    return g_mods.back();
}

} // namespace

void setModMenuInfo(const std::string& mod, const std::string& name, const std::string& icon) {
    std::lock_guard<std::mutex> l(g_mx);
    ModInfo& m = infoOf(mod);
    if (m.name.empty()) m.name = name;
    if (m.icon.empty()) m.icon = icon;
}

int addModMenuFolder(const std::string& mod, const std::string& name, const std::string& icon, bool npc) {
    std::lock_guard<std::mutex> l(g_mx);
    infoOf(mod);
    for (size_t i = 0; i < g_custom.size(); ++i) {
        ModMenuFolder& f = g_custom[i];
        if (f.mod == mod && f.name == name && f.npc == npc) {
            if (f.icon.empty()) f.icon = icon;
            return static_cast<int>(i);
        }
    }
    ModMenuFolder f;
    f.mod = mod;
    f.name = name;
    f.icon = icon;
    f.npc = npc;
    g_custom.push_back(std::move(f));
    return static_cast<int>(g_custom.size()) - 1;
}

bool addToModMenuFolder(int folder, int type) {
    std::lock_guard<std::mutex> l(g_mx);
    if (folder < 0 || static_cast<size_t>(folder) >= g_custom.size()) return false;
    std::vector<int>& t = g_custom[static_cast<size_t>(folder)].types;
    for (int x : t) if (x == type) return true;
    t.push_back(type);
    return true;
}

void hideFromModMenu(MenuKind kind, int type) {
    std::lock_guard<std::mutex> l(g_mx);
    g_hidden[static_cast<int>(kind)].insert(type);
}

std::unordered_set<int> hiddenFromModMenu(MenuKind kind) {
    std::lock_guard<std::mutex> l(g_mx);
    return g_hidden[static_cast<int>(kind)];
}

std::vector<ModMenuFolder> modMenuFolders() {
    // Fora da trava: cada uma tem a sua.
    const std::vector<ModItemInfo> items = modItems();
    const std::vector<ModNpcInfo> npcs = modNpcs();
    const std::vector<ModBuffInfo> buffs = modBuffs();

    std::lock_guard<std::mutex> l(g_mx);
    const auto shown = [](MenuKind kind, int type) {
        return g_hidden[static_cast<int>(kind)].count(type) == 0;
    };
    std::vector<ModMenuFolder> out;
    for (const ModInfo& m : g_mods) {
        ModMenuFolder all;
        all.mod = m.mod;
        all.modName = m.name.empty() ? m.mod : m.name;
        all.modIcon = m.icon;

        ModMenuFolder itemsFolder = all;
        itemsFolder.name = "Itens";
        for (const ModItemInfo& i : items) {
            if (i.mod == m.mod && shown(MenuKind::Item, i.type)) itemsFolder.types.push_back(i.type);
        }
        if (!itemsFolder.types.empty()) out.push_back(std::move(itemsFolder));

        ModMenuFolder npcsFolder = all;
        npcsFolder.name = "NPCs";
        npcsFolder.npc = true;
        for (const ModNpcInfo& n : npcs) {
            if (n.mod == m.mod && shown(MenuKind::Npc, n.type)) npcsFolder.types.push_back(n.type);
        }
        if (!npcsFolder.types.empty()) out.push_back(std::move(npcsFolder));

        ModMenuFolder buffsFolder = all;
        buffsFolder.name = "Buffs";
        buffsFolder.buff = true;
        for (const ModBuffInfo& b : buffs) {
            if (b.mod == m.mod && shown(MenuKind::Buff, b.type)) buffsFolder.types.push_back(b.type);
        }
        if (!buffsFolder.types.empty()) out.push_back(std::move(buffsFolder));

        for (const ModMenuFolder& f : g_custom) {
            if (f.mod != m.mod) continue;
            ModMenuFolder c = f;
            c.types.clear();
            const MenuKind kind = f.buff ? MenuKind::Buff : f.npc ? MenuKind::Npc : MenuKind::Item;
            for (int t : f.types) if (shown(kind, t)) c.types.push_back(t);
            if (c.types.empty()) continue;
            c.modName = all.modName;
            c.modIcon = all.modIcon;
            out.push_back(std::move(c));
        }
    }
    return out;
}

} // namespace bl::runtime
