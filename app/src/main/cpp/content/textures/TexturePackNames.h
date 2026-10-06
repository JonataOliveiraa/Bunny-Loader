#pragma once
#include <climits>
#include <string>
#include <string_view>

namespace bl::runtime::textures {

struct Target { std::string table; int index = -1; };

// Somente arquivos diretamente em Images/: NPCs do Bestiario e variantes
// guardadas em Misc/ nao podem substituir o sprite de um NPC por acidente.
inline Target targetFor(std::string_view file) {
    if (file.size() < 5 || file.substr(file.size() - 4) != ".png") return {};
    file.remove_suffix(4);
    struct Mapping { std::string_view prefix; const char* table; };
    static constexpr Mapping mappings[] = {
        {"Item_", "Item"}, {"NPC_", "Npc"}, {"Projectile_", "Projectile"},
        {"Tiles_", "Tile"}, {"Wall_", "Wall"}, {"Buff_", "Buff"}, {"Gore_", "Gore"},
        {"Armor_Head_", "ArmorHead"}, {"Armor_Body_", "ArmorBody"},
        {"Armor_Arm_", "ArmorArm"}, {"Armor_Legs_", "ArmorLeg"},
        {"Female_Body_", "FemaleBody"}, {"Armor/Armor_", "ArmorBodyComposite"}
    };
    for (const auto& m : mappings) {
        if (file.substr(0, m.prefix.size()) != m.prefix) continue;
        const auto digits = file.substr(m.prefix.size());
        if (digits.empty()) continue;
        int index = 0;
        bool valid = true;
        for (char c : digits) {
            if (c < '0' || c > '9' || index > (INT_MAX - (c - '0')) / 10) { valid = false; break; }
            index = index * 10 + c - '0';
        }
        if (valid) return {m.table, index};
    }
    return {};
}

} // namespace bl::runtime::textures
