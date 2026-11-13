#pragma once
#include <string>
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

std::vector<ModMenuFolder> modMenuFolders();

}
