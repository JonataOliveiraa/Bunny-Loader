#include "content/items/ModEquips.h"
#include "content/common/TypeTables.h"

#include <map>
#include <memory>
#include <mutex>
#include <string>

namespace bl::runtime {

int32_t growEquipSets(const char* kind, int32_t from, int32_t to) {
    if (!kind || from <= 0 || to < from) return -1;
    static std::mutex mx;
    static std::map<std::string, std::unique_ptr<TypeTables>> tables;
    static std::map<std::string, std::string> names;
    std::lock_guard<std::mutex> lock(mx);
    const std::string& name = names[kind] = kind;
    auto& t = tables[name];
    if (!t) {
        t = std::make_unique<TypeTables>("equipamento", from,
                                         std::vector<TableClass>{{"Terraria.ID", "ArmorIDs", name.c_str(), "Sets"}},
                                         TypeTables::Fill::Mode);
    }
    return t->grow(from, to);
}

}
