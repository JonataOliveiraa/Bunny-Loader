#pragma once
#include <cstdint>
#include <string>
#include <vector>

namespace bl::runtime {

enum NpcClass : uint8_t { kNpcOther = 0, kNpcBoss = 1, kNpcMonster = 2, kNpcTown = 3, kNpcCritter = 4 };

enum BuffClass : uint8_t {
    kBuffOther = 0, kBuffGood = 1, kBuffDebuff = 2, kBuffFood = 3, kBuffFlask = 4,
    kBuffPet = 5, kBuffSummon = 6,
};

void buildMenuCatalogExtras();

const std::vector<uint8_t>& npcClasses();
const std::vector<std::u16string>& buffNames();
const std::vector<uint8_t>& buffClasses();

std::vector<std::u16string> readBuffNames();
void replaceBuffNames(std::vector<std::u16string>& names);

}
