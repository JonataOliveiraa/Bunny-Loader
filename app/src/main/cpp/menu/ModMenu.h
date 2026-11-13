#pragma once
#include <cstdint>
#include <string>
#include <unordered_set>
#include <vector>

namespace bl::runtime {

struct ModMenuFolder {
    std::string mod;
    std::string modName;
    std::string modIcon;
    std::string name;
    std::string icon;
    bool npc = false;
    bool buff = false;
    std::vector<int> types;
};

void setModMenuInfo(const std::string& mod, const std::string& name, const std::string& icon);

int addModMenuFolder(const std::string& mod, const std::string& name, const std::string& icon, bool npc);

bool addToModMenuFolder(int folder, int type);

std::vector<ModMenuFolder> modMenuFolders();

enum class MenuKind : uint8_t { Item, Npc, Buff };

void hideFromModMenu(MenuKind kind, int type);

std::unordered_set<int> hiddenFromModMenu(MenuKind kind);

}
