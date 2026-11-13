#pragma once
#include <cstdint>
#include <mutex>
#include <string>
#include <vector>

namespace bl::runtime {

void installCheats();

void giveItem(int type, int stack, int player = -1);

void requestGive(int type, int stack);

void spawnNpcs(int type, int count, int player = -1);

bool namesReady();
const std::vector<std::u16string>& itemNames();
const std::vector<std::u16string>& npcNames();
std::mutex& namesLock();
int namesGeneration();

const std::vector<int>& npcFrames();
void requestSpawn(int type, int count = 1);
void requestBuff(int type, int seconds);

enum ItemClass : uint8_t {
    kClassOther = 0,
    kClassMelee = 1,
    kClassRanged = 2,
    kClassMagic = 3,
    kClassSummon = 4,
    kClassAmmo = 5,
    kClassTool = 6,
    kClassAccessory = 7,
    kClassArmor = 8,
    kClassPotion = 9,
    kClassBlock = 10,
    kClassNone = 255,
};

bool inWorld();

const std::vector<uint8_t>& itemClasses();
const std::vector<uint8_t>& itemSubClasses();

}
