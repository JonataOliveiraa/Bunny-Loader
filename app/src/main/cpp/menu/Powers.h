#pragma once

namespace bl::runtime {

enum class Power : int {
    Damage = 0,
    Speed = 1,
    Jump = 2,
    TimeStop = 3,
    God = 4,
    Mana = 5,
    InfiniteJump = 6,
    FastMining = 7,
    Vision = 8,
    Fly = 9,
    XRay = 10,
    Minions = 11,
    Rain = 12,
    Wind = 13,
    Bestiary = 14,
    NoSpawns = 15,
    MapTeleport = 16,
    ClearInventory = 17,
    RevealMap = 18,
    Hardmode = 19,
    Difficulty = 20,
    FastRespawn = 21,
    ClearEnemies = 22,
    StopInfection = 23,
    StopEvents = 24,
    BloodMoon = 25,
    Eclipse = 26,
    SlimeRain = 27,
    GoblinArmy = 28,
    FrostLegion = 29,
    Pirates = 30,
    Martians = 31,
    PumpkinMoon = 32,
    FrostMoon = 33,
    Party = 34,
    Sandstorm = 35,
    Count
};

void setPower(int id, int level);

bool setWorldPowerFromNet(int id, int level);

void revealMapForClient(int client);

constexpr int kSliderLevels = 101;

constexpr int kClockRequest = 100;
void setTimeOfDay(int request);

int worldHardmode();
int worldGameMode();
int worldInfectionStopped();

int worldClockMinute();
int worldRainPosition();
int worldWindPosition();

void tickPowers();

}
