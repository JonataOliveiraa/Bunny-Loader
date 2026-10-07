#include "content/items/ModItemSaveFormat.h"
#include <cassert>
#include <iostream>
#include <sstream>

using namespace bl::runtime::item_save;

int main() {
    int checks = 0;
    auto check = [&](bool value) {
        assert(value);
        ++checks;
    };

    SavedItem out;
    check(decode("inventory\t0\t1\t0\t0\tmod/Item", out));
    check(out.data.empty() && out.slot == 0 && out.stack == 1);
    check(decode("bank4\t39\t9999\tprefixmod/Prefix\t1\tmod/Item\t{\"owner\":\"A\"}", out));
    check(out.prefixKey == "prefixmod/Prefix" && out.prefix == 0 && out.favorited);
    const auto originalKey = out.key;

    for (const auto* line : {
        "", "inventory\t0\t1\t0\t0", "\t0\t1\t0\t0\tmod/Item", "inventory\t0\t1\t0\t0\t",
        "inventory\t-1\t1\t0\t0\tmod/Item", "inventory\t0\t0\t0\t0\tmod/Item",
        "inventory\t0\t-1\t0\t0\tmod/Item", "inventory\tx\t1\t0\t0\tmod/Item",
        "inventory\t1x\t1\t0\t0\tmod/Item", "inventory\t999999999999\t1\t0\t0\tmod/Item",
        "inventory\t0\t1\t-1\t0\tmod/Item", "inventory\t0\t1\t256\t0\tmod/Item",
        "inventory\t0\t1\tx\t0\tmod/Item", "inventory\t0\t1\t0\ty\tmod/Item",
        "inventory\t0\t1\t0\t0\tmod/Item\t{}\textra", "trash\t0\t1\t0\t0\tmod/Item"
    }) {
        check(!decode(line, out));
        check(out.key == originalKey);
    }

    std::vector<SavedItem> records;
    for (const auto* container : {"inventory", "armor", "dye", "misc", "miscdye", "bank", "bank2", "bank3", "bank4",
            "loadout0.armor", "loadout0.dye", "loadout1.armor", "loadout1.dye", "loadout2.armor", "loadout2.dye", "chest:100,-20"}) {
        records.push_back({container, 3, 42, 81, true, "missing-mod/Item", "", "{\"owner\":\"João 🎮\\tA\\nB\",\"blob\":\"" + std::string(12000, 'x') + "\"}"});
    }
    records.push_back({"buff", 2, 3600, 0, false, "mod/Buff", "", ""});
    records.push_back({"inventory", 4, 1, 0, false, "vanilla:1", "mod/Prefix", ""});

    std::stringstream file;
    check(write(file, records));
    std::vector<SavedItem> loaded;
    check(read(file, loaded));
    check(loaded.size() == records.size());

    for (size_t i = 0; i < loaded.size(); ++i) {
        check(loaded[i].container == records[i].container && loaded[i].slot == records[i].slot && loaded[i].key == records[i].key);
        check(loaded[i].data == records[i].data && loaded[i].prefixKey == records[i].prefixKey);
        check(loaded[i].stack == records[i].stack && loaded[i].prefix == records[i].prefix && loaded[i].favorited == records[i].favorited);
    }

    std::stringstream legacy("bunnyloader itens 1\r\ninventory\t5\t1\t0\t0\tmod/Old\r\n");
    check(read(legacy, loaded) && loaded.size() == 1 && loaded[0].key == "mod/Old" && loaded[0].data.empty());

    std::stringstream damaged("bunnyloader itens 2\ninvalid\ninventory\t7\t1\t0\t0\tmod/Good\t{}\n");
    check(read(damaged, loaded) && loaded.size() == 1 && loaded[0].slot == 7);

    std::stringstream unknown("bunnyloader itens 99\ninventory\t1\t1\t0\t0\tmod/Bad\t{}\n");
    check(!read(unknown, loaded) && loaded[0].slot == 7);

    std::stringstream empty;
    check(write(empty, {}) && read(empty, loaded) && loaded.empty());

    for (const auto& bad : {std::string("raw\ttab"), std::string("raw\nline"), std::string("raw\rline"), std::string("nul\0byte", 8)}) {
        records[0].data = bad;
        std::stringstream stream;
        check(!write(stream, records));
    }

    std::stringstream failed;
    failed.setstate(std::ios::badbit);
    check(!write(failed, {}));
    check(!read(failed, loaded));

    std::cout << "moditemdata format: " << checks << " native C++ checks passed\n";
}
