#include "menu/Powers.h"
#include "content/common/ModNet.h"
#include "menu/NetRequests.h"
#include "menu/Cheats.h"
#include "core/Log.h"
#include "hook/HookManager.h"
#include "il2cpp/Api.h"
#include "il2cpp/Resolver.h"
#include "il2cpp/Signature.h"
#include "content/common/GameRefs.h"
#include "menu/MapReveal.h"
#include <algorithm>
#include <atomic>
#include <chrono>
#include <cmath>
#include <cstdio>
#include <cstdlib>
#include <initializer_list>
#include <iterator>
#include <string>
#include <vector>

namespace bl::runtime {

namespace {

constexpr int kPowerCount = static_cast<int>(Power::Count);

constexpr int kMaxLevel[] = {3, 2, 2, 1, 1, 1, 1, 1, 1, 2, 1, 1, kSliderLevels, kSliderLevels, 1,
                             kSliderLevels, 1, 1, 1, 2, 4, 1, 1, 2, 1,
                             1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1};
static_assert(std::size(kMaxLevel) == kPowerCount, "um nivel maximo por poder");

constexpr float kDamage[] = {1.f, 2.f, 5.f, 10.f};
constexpr float kRunSpeed[] = {1.f, 2.f, 3.f};
constexpr float kJumpBoost[] = {0.f, 5.f, 10.f};
constexpr float kToolSpeed = 0.25f;

constexpr float kFlySpeed[] = {0.f, 10.f, 22.f};
constexpr float kTeleportDistance = 160.f;
constexpr float kWorldMargin = 640.f;
constexpr int kGraceStillFrames = 10;

constexpr int32_t kUnlimitedMinions = 999;

float sliderPosition(int level) { return static_cast<float>(level - 1) / 100.f; }

constexpr float kStormRain = 0.75f;
constexpr float kStormWind = 0.6f;
constexpr int32_t kRainTimeFloor = 3600;
constexpr float kMaxWind = 0.8f;
float windOf(int level) { return (sliderPosition(level) * 2.f - 1.f) * kMaxWind; }

float spawnMultiplierOf(int level) {
    const float t = sliderPosition(level);
    if (t <= 0.f) return 0.f;
    return t < 0.5f ? 0.1f + (t / 0.5f) * 0.9f : 1.f + ((t - 0.5f) / 0.5f) * 9.f;
}

constexpr int32_t kFirstNpcNetId = -128;
constexpr int32_t kLastNpcNetId = 4096;

struct TimeOfDay { int32_t time; bool day; };
constexpr TimeOfDay kTimesOfDay[] = {
    {0, true},
    {27000, true},
    {0, false},
    {16200, false},
};

constexpr int kInventorySlots = 50;

constexpr auto kRevealBudget = std::chrono::milliseconds(12);
constexpr int kSectionsPerFrame = 10;
constexpr int kSectionWaitCheck = 30;
constexpr int kSectionStallChecks = 10;

constexpr auto kMapHoldTime = std::chrono::milliseconds(1000);
constexpr int kMapHoldSlopDiv = 40;
constexpr int kTeleportSearchTiles = 150;
constexpr int kWorldMarginTiles = 41;

constexpr int32_t kNetClient = 1;

constexpr int kMsgWorldData = 7;
constexpr int kMsgTimeSync = 18;
constexpr int kWorldResyncFrames = 60;

bool isEventPower(Power p) { return p >= Power::BloodMoon && p <= Power::Sandstorm; }

bool isWorldPower(Power p) {
    switch (p) {
    case Power::TimeStop: case Power::Rain: case Power::Wind: case Power::NoSpawns:
    case Power::Hardmode: case Power::Difficulty: case Power::ClearEnemies:
    case Power::StopInfection: case Power::StopEvents:
        return true;
    default:
        return isEventPower(p);
    }
}

bool isWorldCommand(Power p) {
    switch (p) {
    case Power::Hardmode: case Power::Difficulty: case Power::ClearEnemies:
    case Power::StopInfection: case Power::StopEvents:
        return true;
    default:
        return isEventPower(p);
    }
}

std::atomic<int> g_level[kPowerCount];

int levelOf(Power p) { return g_level[static_cast<int>(p)].load(std::memory_order_relaxed); }

bool usesResetHook(Power p) {
    switch (p) {
    case Power::TimeStop: case Power::XRay: case Power::Rain: case Power::Wind:
    case Power::Bestiary: case Power::NoSpawns: case Power::MapTeleport:
    case Power::ClearInventory: case Power::RevealMap: case Power::Hardmode:
    case Power::Difficulty: case Power::FastRespawn: case Power::ClearEnemies:
    case Power::StopInfection: case Power::StopEvents:
        return false;
    default:
        return !isEventPower(p);
    }
}

struct Vec2 { float x, y; };

struct PlayerFields {
    int32_t whoAmI, meleeDamage, magicDamage, rangedDamage, minionDamage, moveSpeed,
        jumpSpeedBoost, noFallDmg, creativeGodMode, statLife, statLifeMax2, statMana,
        statManaMax2, manaCost, breath, breathMax, hasJumpOption_Cloud,
        canJumpAgain_Cloud, pickSpeed, nightVision, findTreasure, detectCreature,
        dangerSense, maxMinions, maxTurrets, dead, respawnTimer;
} P;

struct FlyFields {
    int32_t position, velocity, width, height, dead, controlLeft, controlRight,
        controlUp, controlDown, controlJump, fallStart, fallStart2;
} F;

struct MainStatics {
    FieldInfo *time, *raining, *rainTime, *maxRaining, *cloudAlpha, *windCurrent,
        *windTarget, *leftWorld, *rightWorld, *topWorld, *bottomWorld, *bestiaryTracker;
} M;

int32_t g_npcImmune = -1;
int32_t g_projHostile = -1;
int32_t g_projFriendly = -1;

const MethodInfo* g_getMyPlayer = nullptr;
const MethodInfo* g_reset = nullptr;
const MethodInfo* g_playerUpdate = nullptr;
const MethodInfo* g_npcUpdate = nullptr;
const MethodInfo* g_projUpdate = nullptr;
const MethodInfo* g_revengeUpdate = nullptr;

struct LightEngine {
    const char* name;
    const MethodInfo* present = nullptr;
    int32_t activeMap = -1;
};
LightEngine g_engines[] = {{"LightingEngine"}, {"LegacyLighting"}};
int32_t g_mapColors = -1;

struct BestiaryRefs {
    int32_t kills = -1, sights = -1, chats = -1;
    FieldInfo* creditIds = nullptr;
    const MethodInfo *killsNeeded = nullptr, *getKills = nullptr, *setKills = nullptr,
        *setSeen = nullptr, *setChatted = nullptr;
} B;

struct WorldRefs {
    FieldInfo *hardMode = nullptr, *netMode = nullptr, *maxTilesX = nullptr, *maxTilesY = nullptr,
        *npc = nullptr, *player = nullptr;
    const MethodInfo *getGameMode = nullptr, *setGameMode = nullptr, *skipToTime = nullptr,
        *startHardmode = nullptr, *spawnNpc = nullptr, *getSpawnRate = nullptr, *getMap = nullptr, *setRefreshMap = nullptr,
        *updateLighting = nullptr, *turnToAir = nullptr, *teleport = nullptr,
        *solidCollision = nullptr, *getMapFullscreen = nullptr, *getTouchCount = nullptr,
        *getTouch = nullptr, *getDeviceWidth = nullptr, *getDeviceHeight = nullptr,
        *getMapPos = nullptr,
        *getMapScale = nullptr, *getUiMouse = nullptr;
    FieldInfo *maxSectionsX = nullptr, *maxSectionsY = nullptr;
    const MethodInfo *sendSection = nullptr, *getSectionManager = nullptr, *sectionLoaded = nullptr;
    int32_t playerActive = -1;
    int32_t npcActive = -1, npcTown = -1, npcFriendly = -1, npcDamage = -1;
    int32_t inventory = -1, favorited = -1;
    int32_t uiMouseX = -1, uiMouseY = -1;
} W;

struct EventRefs {
    FieldInfo *dayTime = nullptr, *bloodMoon = nullptr, *eclipse = nullptr, *raining = nullptr,
        *slimeRain = nullptr, *pumpkinMoon = nullptr, *snowMoon = nullptr,
        *invasionType = nullptr, *invasionSize = nullptr, *manualParty = nullptr,
        *genuineParty = nullptr, *manualLanterns = nullptr, *genuineLanterns = nullptr,
        *sandstorm = nullptr;
    const MethodInfo *startSlimeRain = nullptr, *stopSlimeRain = nullptr, *stopRain = nullptr,
        *startInvasion = nullptr, *startPumpkinMoon = nullptr, *startSnowMoon = nullptr,
        *stopMoonEvent = nullptr, *toggleParty = nullptr, *startSandstorm = nullptr,
        *stopSandstorm = nullptr, *stopOldOnesArmy = nullptr;
    int32_t statLifeMax = -1;
} E;

enum class State { NotTried, Ok, Failed };
State g_refs = State::NotTried;
bool g_flyRefs = false, g_weatherRefs = false, g_lightRefs = false, g_bestiaryRefs = false;
bool g_timeRefs = false, g_hardmodeRefs = false, g_spawnRefs = false, g_mapRefs = false,
    g_inventoryRefs = false, g_teleportRefs = false, g_sectionRefs = false, g_eventRefs = false;
State g_spawnHook = State::NotTried;
State g_playerHook = State::NotTried;
State g_timeHooks = State::NotTried;
State g_playerUpdateHook = State::NotTried;
State g_revengeHook = State::NotTried;
State g_lightHooks = State::NotTried;

std::atomic<int> g_localPlayer{-1};

struct FieldName { int32_t* dst; const char* name; };

template <size_t N>
bool resolveOffsets(Il2CppClass* cls, const FieldName (&fields)[N]) {
    bool ok = true;
    for (const auto& c : fields) {
        *c.dst = il2cpp::fieldOffset(cls, c.name);
        if (*c.dst < 0) ok = false;
    }
    return ok;
}

const MethodInfo* methodBySignature(Il2CppClass* cls, const char* signature) {
    return cls ? il2cpp::findMethodBySignature(cls, il2cpp::parseSignature(signature)) : nullptr;
}

bool resolveFlyRefs(Il2CppClass* player, Il2CppClass* main) {
    const FieldName fields[] = {
        {&F.position, "position"}, {&F.velocity, "velocity"}, {&F.width, "width"},
        {&F.height, "height"}, {&F.dead, "dead"}, {&F.controlLeft, "controlLeft"},
        {&F.controlRight, "controlRight"}, {&F.controlUp, "controlUp"},
        {&F.controlDown, "controlDown"}, {&F.controlJump, "controlJump"},
        {&F.fallStart, "fallStart"}, {&F.fallStart2, "fallStart2"},
    };
    bool ok = resolveOffsets(player, fields);
    M.leftWorld = il2cpp::findField(main, "leftWorld");
    M.rightWorld = il2cpp::findField(main, "rightWorld");
    M.topWorld = il2cpp::findField(main, "topWorld");
    M.bottomWorld = il2cpp::findField(main, "bottomWorld");
    return ok && M.leftWorld && M.rightWorld && M.topWorld && M.bottomWorld && g_playerUpdate;
}

bool resolveWeatherRefs(Il2CppClass* main) {
    M.raining = il2cpp::findField(main, "raining");
    M.rainTime = il2cpp::findField(main, "rainTime");
    M.maxRaining = il2cpp::findField(main, "maxRaining");
    M.cloudAlpha = il2cpp::findField(main, "cloudAlpha");
    M.windCurrent = il2cpp::findField(main, "windSpeedCurrent");
    M.windTarget = il2cpp::findField(main, "windSpeedTarget");
    return M.raining && M.rainTime && M.maxRaining && M.cloudAlpha && M.windCurrent &&
           M.windTarget;
}

bool resolveLightRefs() {
    using namespace il2cpp;
    Il2CppClass* map = findClass({"Terraria.Graphics.Light", "LightMap", {}});
    g_mapColors = map ? fieldOffset(map, "_colors") : -1;
    bool any = false;
    for (auto& e : g_engines) {
        Il2CppClass* cls = findClass({"Terraria.Graphics.Light", e.name, {}});
        if (!cls) continue;
        e.present = api().class_get_method_from_name(cls, "Present", 0);
        e.activeMap = fieldOffset(cls, "_activeLightMap");
        if (e.present && e.activeMap >= 0) any = true;
    }
    return g_mapColors >= 0 && any;
}

bool resolveBestiaryRefs(Il2CppClass* main) {
    using namespace il2cpp;
    constexpr const char* kNs = "Terraria.GameContent.Bestiary";
    Il2CppClass* tracker = findClass({kNs, "BestiaryUnlocksTracker", {}});
    Il2CppClass* kills = findClass({kNs, "NPCKillsTracker", {}});
    Il2CppClass* sights = findClass({kNs, "NPCWasNearPlayerTracker", {}});
    Il2CppClass* chats = findClass({kNs, "NPCWasChatWithTracker", {}});
    Il2CppClass* enemy = findClass({kNs, "CommonEnemyUICollectionInfoProvider", {}});
    Il2CppClass* samples = findClass({"Terraria.ID", "ContentSamples", {}});
    if (!tracker || !kills || !sights || !chats || !enemy || !samples) return false;

    const FieldName fields[] = {
        {&B.kills, "Kills"}, {&B.sights, "Sights"}, {&B.chats, "Chats"},
    };
    bool ok = resolveOffsets(tracker, fields);
    M.bestiaryTracker = findField(main, "BestiaryTracker");
    B.creditIds = findField(samples, "NpcBestiaryCreditIdsByNpcNetIds");
    B.killsNeeded = methodBySignature(enemy, "int GetKillCountNeeded(string persistentId)");
    B.getKills = methodBySignature(kills, "int GetKillCount(string persistentId)");
    B.setKills = methodBySignature(kills,
                                   "void SetKillCountDirectly(string persistentId, int killCount)");
    B.setSeen = methodBySignature(sights, "void SetWasSeenDirectly(string persistentId)");
    B.setChatted = methodBySignature(chats, "void SetWasChatWithDirectly(string persistentId)");
    return ok && M.bestiaryTracker && B.creditIds && B.killsNeeded && B.getKills &&
           B.setKills && B.setSeen && B.setChatted;
}

bool allFound(const char* group, std::initializer_list<const void*> refs) {
    for (const void* r : refs) {
        if (!r) {
            BL_ERROR("poderes: refs de %s faltando; esse poder fica desligado", group);
            return false;
        }
    }
    return true;
}

void resolveWorldRefs(Il2CppClass* main, Il2CppClass* player) {
    using namespace il2cpp;
    auto& a = api();
    Il2CppClass* npc = findClass({"Terraria", "NPC", {}});
    Il2CppClass* item = findClass({"Terraria", "Item", {}});
    Il2CppClass* worldGen = findClass({"Terraria", "WorldGen", {}});
    Il2CppClass* worldMap = findClass({"Terraria.Map", "WorldMap", {}});
    Il2CppClass* collision = findClass({"Terraria", "Collision", {}});

    W.netMode = findField(main, "netMode");
    W.skipToTime = methodBySignature(main, "void SkipToTime(int timeToSet, bool setIsDayTime)");
    g_timeRefs = allFound("hora", {W.skipToTime});

    W.hardMode = findField(main, "hardMode");
    W.getGameMode = a.class_get_method_from_name(main, "get_GameMode", 0);
    W.setGameMode = a.class_get_method_from_name(main, "set_GameMode", 1);
    W.startHardmode = worldGen ? a.class_get_method_from_name(worldGen, "StartHardmode", 0) : nullptr;
    g_hardmodeRefs = allFound("hardmode/dificuldade",
                              {W.hardMode, W.netMode, W.getGameMode, W.setGameMode, W.startHardmode});

    W.npc = findField(main, "npc");
    W.spawnNpc = npc ? a.class_get_method_from_name(npc, "SpawnNPC", 0) : nullptr;
    Il2CppClass* spawner = npc ? findNested(npc, "Spawner") : nullptr;
    W.getSpawnRate = spawner ? methodBySignature(spawner,
        "void GetSpawnRate(Player player, out int spawnRate, out int maxSpawns)") : nullptr;
    if (!W.getSpawnRate) BL_ERROR("poderes: sem NPC.Spawner.GetSpawnRate (a taxa fica so liga/desliga)");
    if (npc) {
        W.npcActive = fieldOffset(npc, "active");
        W.npcTown = fieldOffset(npc, "townNPC");
        W.npcFriendly = fieldOffset(npc, "friendly");
        W.npcDamage = fieldOffset(npc, "damage");
    }
    g_spawnRefs = allFound("spawn", {W.npc, W.spawnNpc, W.netMode}) && W.npcActive >= 0 &&
                  W.npcTown >= 0 && W.npcFriendly >= 0 && W.npcDamage >= 0;

    W.maxTilesX = findField(main, "maxTilesX");
    W.maxTilesY = findField(main, "maxTilesY");
    W.getMap = a.class_get_method_from_name(main, "get_Map", 0);
    W.setRefreshMap = a.class_get_method_from_name(main, "set_refreshMap", 1);
    W.updateLighting = methodBySignature(worldMap, "bool UpdateLighting(int x, int y, byte light)");
    g_mapRefs = allFound("mapa", {W.maxTilesX, W.maxTilesY, W.getMap, W.setRefreshMap,
                                  W.updateLighting});

    Il2CppClass* netMessage = findClass({"Terraria", "NetMessage", {}});
    Il2CppClass* sections = findClass({"Terraria", "WorldSections", {}});
    W.maxSectionsX = findField(main, "maxSectionsX");
    W.maxSectionsY = findField(main, "maxSectionsY");
    W.sendSection = methodBySignature(netMessage, "void SendSection(int whoAmi, int sectionX, int sectionY)");
    W.getSectionManager = a.class_get_method_from_name(main, "get_sectionManager", 0);
    W.sectionLoaded = methodBySignature(sections, "bool SectionLoaded(int x, int y)");
    W.playerActive = fieldOffset(player, "active");
    g_sectionRefs = allFound("revelar no multijogador",
                             {W.maxSectionsX, W.maxSectionsY, W.sendSection, W.getSectionManager,
                              W.sectionLoaded}) && W.playerActive >= 0;

    W.player = findField(main, "player");
    W.inventory = fieldOffset(player, "inventory");
    W.favorited = item ? fieldOffset(item, "favorited") : -1;
    W.turnToAir = methodBySignature(item, "void TurnToAir()");
    g_inventoryRefs = allFound("mochila", {W.player, W.turnToAir}) && W.inventory >= 0 &&
                      W.favorited >= 0;

    W.teleport = methodBySignature(player, "void Teleport(Vector2 newPos, int Style, int extraInfo)");
    W.solidCollision = methodBySignature(collision,
                                         "bool SolidCollision(Vector2 Position, int Width, int Height)");
    W.getMapFullscreen = a.class_get_method_from_name(main, "get_mapFullscreen", 0);
    Il2CppClass* input = findClass({"UnityEngine", "Input", {}});
    Il2CppClass* screen = findClass({"UnityEngine", "Screen", {}});
    W.getTouchCount = input ? a.class_get_method_from_name(input, "get_touchCount", 0) : nullptr;
    W.getTouch = methodBySignature(input, "Touch GetTouch(int index)");
    W.getDeviceWidth = screen ? a.class_get_method_from_name(screen, "get_width", 0) : nullptr;
    W.getDeviceHeight = screen ? a.class_get_method_from_name(screen, "get_height", 0) : nullptr;
    W.getMapPos = a.class_get_method_from_name(main, "get_mapFullscreenPos", 0);
    W.getMapScale = a.class_get_method_from_name(main, "get_mapFullscreenScale", 0);
    Il2CppClass* runner = findClass({"", "XNAUnityRunner", {}});
    Il2CppClass* mouseState = findClass({"", "XNAUnityRunner", "MouseStateBackup"});
    W.getUiMouse = runner ? a.class_get_method_from_name(runner, "get__uiMouseState", 0) : nullptr;
    W.uiMouseX = mouseState ? fieldOffset(mouseState, "_mouseX") : -1;
    W.uiMouseY = mouseState ? fieldOffset(mouseState, "_mouseY") : -1;
    g_teleportRefs = allFound("teleporte no mapa",
                              {W.player, W.teleport, W.solidCollision, W.getMapFullscreen,
                               W.getTouchCount, W.getTouch, W.getDeviceWidth, W.getDeviceHeight,
                               W.getMapPos, W.getMapScale, W.getUiMouse, W.maxTilesX,
                               W.maxTilesY}) &&
                      W.uiMouseX >= 0 && W.uiMouseY >= 0;
}

void resolveEventRefs(Il2CppClass* main, Il2CppClass* player) {
    using namespace il2cpp;
    constexpr const char* kNs = "Terraria.GameContent.Events";
    Il2CppClass* party = findClass({kNs, "BirthdayParty", {}});
    Il2CppClass* lanterns = findClass({kNs, "LanternNight", {}});
    Il2CppClass* sandstorm = findClass({kNs, "Sandstorm", {}});
    Il2CppClass* oldOnes = findClass({kNs, "DD2Event", {}});
    const auto staticField = [](Il2CppClass* cls, const char* name) {
        return cls ? findField(cls, name) : nullptr;
    };

    E.dayTime = findField(main, "dayTime");
    E.bloodMoon = findField(main, "bloodMoon");
    E.eclipse = findField(main, "eclipse");
    E.raining = findField(main, "raining");
    E.slimeRain = findField(main, "slimeRain");
    E.pumpkinMoon = findField(main, "pumpkinMoon");
    E.snowMoon = findField(main, "snowMoon");
    E.invasionType = findField(main, "invasionType");
    E.invasionSize = findField(main, "invasionSize");
    E.manualParty = staticField(party, "ManualParty");
    E.genuineParty = staticField(party, "GenuineParty");
    E.manualLanterns = staticField(lanterns, "ManualLanterns");
    E.genuineLanterns = staticField(lanterns, "GenuineLanterns");
    E.sandstorm = staticField(sandstorm, "Happening");
    E.startSlimeRain = methodBySignature(main, "void StartSlimeRain(bool announce)");
    E.stopSlimeRain = methodBySignature(main, "void StopSlimeRain(bool announce)");
    E.stopRain = methodBySignature(main, "void StopRain(bool instant)");
    E.startInvasion = methodBySignature(main, "void StartInvasion(int type)");
    E.startPumpkinMoon = methodBySignature(main, "void startPumpkinMoon()");
    E.startSnowMoon = methodBySignature(main, "void startSnowMoon()");
    E.stopMoonEvent = methodBySignature(main, "void stopMoonEvent()");
    E.toggleParty = methodBySignature(party, "void ToggleManualParty()");
    E.startSandstorm = methodBySignature(sandstorm, "void StartSandstorm()");
    E.stopSandstorm = methodBySignature(sandstorm, "void StopSandstorm()");
    E.stopOldOnesArmy = methodBySignature(oldOnes, "void StopInvasion(bool win)");
    E.statLifeMax = fieldOffset(player, "statLifeMax");
    g_eventRefs = allFound("eventos",
        {E.dayTime, E.bloodMoon, E.eclipse, E.raining, E.slimeRain, E.pumpkinMoon, E.snowMoon,
         E.invasionType, E.invasionSize, E.manualParty, E.genuineParty, E.manualLanterns,
         E.genuineLanterns, E.sandstorm, E.startSlimeRain, E.stopSlimeRain, E.stopRain,
         E.startInvasion, E.startPumpkinMoon, E.startSnowMoon, E.stopMoonEvent, E.toggleParty,
         E.startSandstorm, E.stopSandstorm, E.stopOldOnesArmy, W.skipToTime, W.player}) &&
        E.statLifeMax >= 0 && W.playerActive >= 0;
}

bool resolveRefs() {
    using namespace il2cpp;
    auto& a = api();
    Il2CppClass* player = findClass({"Terraria", "Player", {}});
    Il2CppClass* main = findClass({"Terraria", "Main", {}});
    Il2CppClass* npc = findClass({"Terraria", "NPC", {}});
    Il2CppClass* proj = findClass({"Terraria", "Projectile", {}});
    if (!player || !main || !npc || !proj) {
        BL_ERROR("poderes: Player/Main/NPC/Projectile nao resolvidos");
        return false;
    }

    const FieldName fields[] = {
        {&P.whoAmI, "whoAmI"}, {&P.meleeDamage, "meleeDamage"},
        {&P.magicDamage, "magicDamage"}, {&P.rangedDamage, "rangedDamage"},
        {&P.minionDamage, "minionDamage"}, {&P.moveSpeed, "moveSpeed"},
        {&P.jumpSpeedBoost, "jumpSpeedBoost"}, {&P.noFallDmg, "noFallDmg"},
        {&P.creativeGodMode, "creativeGodMode"}, {&P.statLife, "statLife"},
        {&P.statLifeMax2, "statLifeMax2"}, {&P.statMana, "statMana"},
        {&P.statManaMax2, "statManaMax2"}, {&P.manaCost, "manaCost"},
        {&P.breath, "breath"}, {&P.breathMax, "breathMax"},
        {&P.hasJumpOption_Cloud, "hasJumpOption_Cloud"},
        {&P.canJumpAgain_Cloud, "canJumpAgain_Cloud"}, {&P.pickSpeed, "pickSpeed"},
        {&P.nightVision, "nightVision"}, {&P.findTreasure, "findTreasure"},
        {&P.detectCreature, "detectCreature"}, {&P.dangerSense, "dangerSense"},
        {&P.maxMinions, "maxMinions"}, {&P.maxTurrets, "maxTurrets"},
        {&P.dead, "dead"}, {&P.respawnTimer, "respawnTimer"},
    };
    bool ok = resolveOffsets(player, fields);
    g_npcImmune = fieldOffset(npc, "immune");
    g_projHostile = fieldOffset(proj, "hostile");
    g_projFriendly = fieldOffset(proj, "friendly");

    g_getMyPlayer = a.class_get_method_from_name(main, "get_myPlayer", 0);
    M.time = findField(main, "time");
    g_reset = a.class_get_method_from_name(player, "ResetEffects", 0);
    g_npcUpdate = a.class_get_method_from_name(npc, "UpdateNPC", 1);
    g_projUpdate = a.class_get_method_from_name(proj, "Update", 1);
    g_playerUpdate = a.class_get_method_from_name(player, "Update", 1);
    Il2CppClass* revenge = findClass({"Terraria.GameContent", "CoinLossRevengeSystem", {}});
    g_revengeUpdate = methodBySignature(revenge, "void Update()");
    if (!g_playerUpdate || !g_revengeUpdate) {
        BL_ERROR("poderes: sem Player.Update/CoinLossRevengeSystem.Update; o Imortal enche a mana");
    }

    if (!ok || g_npcImmune < 0 || g_projHostile < 0 || g_projFriendly < 0 ||
        !g_getMyPlayer || !M.time || !g_reset || !g_npcUpdate || !g_projUpdate) {
        BL_ERROR("poderes: refs faltando; os superpoderes ficam desligados");
        return false;
    }

    if (!(g_flyRefs = resolveFlyRefs(player, main)))
        BL_ERROR("poderes: refs do voo faltando; Voar fica desligado");
    if (!(g_weatherRefs = resolveWeatherRefs(main)))
        BL_ERROR("poderes: refs de chuva/vento faltando; Chuva e Vento ficam desligados");
    if (!(g_lightRefs = resolveLightRefs()))
        BL_ERROR("poderes: refs de iluminacao faltando; Raio-X fica desligado");
    if (!(g_bestiaryRefs = resolveBestiaryRefs(main)))
        BL_ERROR("poderes: refs do bestiario faltando; Bestiario fica desligado");
    resolveWorldRefs(main, player);
    resolveEventRefs(main, player);
    return true;
}

int unboxInt(Il2CppObject* o) {
    return o ? *reinterpret_cast<int*>(reinterpret_cast<char*>(o) + sizeof(Il2CppObject)) : 0;
}

bool unboxBool(Il2CppObject* o) {
    return o && *(reinterpret_cast<uint8_t*>(o) + sizeof(Il2CppObject)) != 0;
}

template <typename T>
T readStatic(FieldInfo* f) {
    T v{};
    il2cpp::api().field_static_get_value(f, &v);
    return v;
}

template <typename T>
void writeStatic(FieldInfo* f, T v) {
    il2cpp::api().field_static_set_value(f, &v);
}

template <typename R>
R callStatic(const MethodInfo* m) {
    return reinterpret_cast<R (*)(const MethodInfo*)>(il2cpp::methodPointer(m))(m);
}

Il2CppObject* localPlayer() {
    const int i = g_localPlayer.load(std::memory_order_relaxed);
    auto* players = readStatic<Il2CppArray*>(W.player);
    if (i < 0 || !players || static_cast<uintptr_t>(i) >= players->length) return nullptr;
    return static_cast<Il2CppObject**>(arrayData(players))[i];
}

using ResetFn = void (*)(Il2CppObject*, const MethodInfo*);
ResetFn g_origReset = nullptr;

std::atomic<bool> g_anyPlayerPower{false};

void applyPowers(Il2CppObject* p) {
    int n;
    if ((n = levelOf(Power::Damage)) > 0) {
        const float k = kDamage[n];
        field<float>(p, P.meleeDamage) *= k;
        field<float>(p, P.magicDamage) *= k;
        field<float>(p, P.rangedDamage) *= k;
        field<float>(p, P.minionDamage) *= k;
    }
    if ((n = levelOf(Power::Speed)) > 0) {
        field<float>(p, P.moveSpeed) *= kRunSpeed[n];
    }
    if ((n = levelOf(Power::Jump)) > 0) {
        field<float>(p, P.jumpSpeedBoost) += kJumpBoost[n];
        field<uint8_t>(p, P.noFallDmg) = 1;
    }
    if (levelOf(Power::God) > 0) {
        field<uint8_t>(p, P.creativeGodMode) = 1;
        int32_t& lifeRef = field<int32_t>(p, P.statLife);
        if (lifeRef < field<int32_t>(p, P.statLifeMax2)) lifeRef = field<int32_t>(p, P.statLifeMax2);
        int32_t& breathRef = field<int32_t>(p, P.breath);
        if (breathRef < field<int32_t>(p, P.breathMax)) breathRef = field<int32_t>(p, P.breathMax);
    }
    if (levelOf(Power::Mana) > 0) {
        field<float>(p, P.manaCost) = 0.f;
        int32_t& mana = field<int32_t>(p, P.statMana);
        if (mana < field<int32_t>(p, P.statManaMax2)) mana = field<int32_t>(p, P.statManaMax2);
    }
    if (levelOf(Power::InfiniteJump) > 0) {
        field<uint8_t>(p, P.hasJumpOption_Cloud) = 1;
        field<uint8_t>(p, P.canJumpAgain_Cloud) = 1;
    }
    if (levelOf(Power::FastMining) > 0) {
        field<float>(p, P.pickSpeed) *= kToolSpeed;
    }
    if (levelOf(Power::Vision) > 0) {
        field<uint8_t>(p, P.nightVision) = 1;
        field<uint8_t>(p, P.findTreasure) = 1;
        field<uint8_t>(p, P.detectCreature) = 1;
        field<uint8_t>(p, P.dangerSense) = 1;
    }
    if (levelOf(Power::Fly) > 0) {
        field<uint8_t>(p, P.noFallDmg) = 1;
    }
    if (levelOf(Power::Minions) > 0) {
        field<int32_t>(p, P.maxMinions) = kUnlimitedMinions;
        field<int32_t>(p, P.maxTurrets) = kUnlimitedMinions;
    }
}

bool g_fallGrace = false;
bool g_graceFell = false;
int g_graceStill = 0;
bool g_wasFlying = false;

void holdFallGrace(Il2CppObject* p) {
    field<uint8_t>(p, P.noFallDmg) = 1;
    const float vy = field<Vec2>(p, F.velocity).y;
    if (vy > 0.f) {
        g_graceFell = true;
        g_graceStill = 0;
    } else if (vy == 0.f && (g_graceFell || ++g_graceStill >= kGraceStillFrames)) {
        g_fallGrace = false;
    }
}

void hkResetEffects(Il2CppObject* self, const MethodInfo* m) {
    g_origReset(self, m);
    if (!g_anyPlayerPower.load(std::memory_order_relaxed)) return;
    if (field<int32_t>(self, P.whoAmI) != g_localPlayer.load(std::memory_order_relaxed)) return;
    applyPowers(self);
    if (g_fallGrace) holdFallGrace(self);
}

using PlayerUpdateFn = void (*)(Il2CppObject*, int32_t, const MethodInfo*);
PlayerUpdateFn g_origPlayerUpdate = nullptr;

void flyUpdate(Il2CppObject* self, int32_t i, const MethodInfo* m, int n) {
    const Vec2 before = field<Vec2>(self, F.position);
    g_origPlayerUpdate(self, i, m);
    if (field<uint8_t>(self, F.dead)) return;

    Vec2& pos = field<Vec2>(self, F.position);
    const float movedX = pos.x - before.x, movedY = pos.y - before.y;
    if (movedX * movedX + movedY * movedY > kTeleportDistance * kTeleportDistance) return;

    float dx = 0.f, dy = 0.f;
    if (field<uint8_t>(self, F.controlLeft)) dx -= 1.f;
    if (field<uint8_t>(self, F.controlRight)) dx += 1.f;
    if (field<uint8_t>(self, F.controlUp) || field<uint8_t>(self, F.controlJump)) dy -= 1.f;
    if (field<uint8_t>(self, F.controlDown)) dy += 1.f;

    Vec2 next{before.x + dx * kFlySpeed[n], before.y + dy * kFlySpeed[n]};
    const float left = readStatic<float>(M.leftWorld) + kWorldMargin;
    const float top = readStatic<float>(M.topWorld) + kWorldMargin;
    const float right = readStatic<float>(M.rightWorld) - kWorldMargin -
                        static_cast<float>(field<int32_t>(self, F.width));
    const float bottom = readStatic<float>(M.bottomWorld) - kWorldMargin -
                         static_cast<float>(field<int32_t>(self, F.height));
    next.x = std::max(left, std::min(next.x, right));
    next.y = std::max(top, std::min(next.y, bottom));

    pos = next;
    field<Vec2>(self, F.velocity) = Vec2{0.f, 0.f};
    const int32_t tileY = static_cast<int32_t>(next.y / 16.f);
    field<int32_t>(self, F.fallStart) = tileY;
    field<int32_t>(self, F.fallStart2) = tileY;
}

int32_t g_manaBeforeRefill = -1;

bool godKeepsMana() { return levelOf(Power::God) > 0 && levelOf(Power::Mana) == 0; }

void hkPlayerUpdate(Il2CppObject* self, int32_t i, const MethodInfo* m) {
    const bool local = i == g_localPlayer.load(std::memory_order_relaxed) && inWorld();
    const int fly = local && g_flyRefs ? levelOf(Power::Fly) : 0;
    if (fly > 0) flyUpdate(self, i, m, fly);
    else g_origPlayerUpdate(self, i, m);
    if (local) g_manaBeforeRefill = godKeepsMana() ? field<int32_t>(self, P.statMana) : -1;
}

using RevengeUpdateFn = void (*)(Il2CppObject*, const MethodInfo*);
RevengeUpdateFn g_origRevengeUpdate = nullptr;

void hkRevengeUpdate(Il2CppObject* self, const MethodInfo* m) {
    if (g_manaBeforeRefill >= 0) {
        if (Il2CppObject* p = godKeepsMana() ? localPlayer() : nullptr) {
            field<int32_t>(p, P.statMana) = std::min(g_manaBeforeRefill, field<int32_t>(p, P.statManaMax2));
        }
        g_manaBeforeRefill = -1;
    }
    g_origRevengeUpdate(self, m);
}

using PresentFn = void (*)(Il2CppObject*, const MethodInfo*);
PresentFn g_origPresent[std::size(g_engines)] = {};

void whitenActiveMap(Il2CppObject* engine, int32_t activeMapOffset) {
    auto* map = field<Il2CppObject*>(engine, activeMapOffset);
    if (!map) return;
    auto* colors = field<Il2CppArray*>(map, g_mapColors);
    if (!colors) return;
    std::fill_n(static_cast<float*>(arrayData(colors)), colors->length * 3, 1.f);
}

template <int E>
void hkPresent(Il2CppObject* self, const MethodInfo* m) {
    g_origPresent[E](self, m);
    if (levelOf(Power::XRay) > 0) whitenActiveMap(self, g_engines[E].activeMap);
}

using UpdateFn = void (*)(Il2CppObject*, int32_t, const MethodInfo*);
UpdateFn g_origNpcUpdate = nullptr;
UpdateFn g_origProjUpdate = nullptr;

void hkUpdateNPC(Il2CppObject* self, int32_t i, const MethodInfo* m) {
    if (levelOf(Power::TimeStop) == 0) { g_origNpcUpdate(self, i, m); return; }
    const int localIndex = g_localPlayer.load(std::memory_order_relaxed);
    auto* immune = field<Il2CppArray*>(self, g_npcImmune);
    if (immune && localIndex >= 0 && static_cast<uintptr_t>(localIndex) < immune->length) {
        int32_t& t = static_cast<int32_t*>(arrayData(immune))[localIndex];
        if (t > 0) --t;
    }
}

void hkProjUpdate(Il2CppObject* self, int32_t i, const MethodInfo* m) {
    if (levelOf(Power::TimeStop) != 0 && field<uint8_t>(self, g_projHostile) &&
        !field<uint8_t>(self, g_projFriendly)) {
        return;
    }
    g_origProjUpdate(self, i, m);
}

double g_frozenTime = 0;
bool g_haveFrozenTime = false;

void holdClock() {
    if (levelOf(Power::TimeStop) == 0) { g_haveFrozenTime = false; return; }
    if (!inWorld()) { g_haveFrozenTime = false; return; }
    if (!g_haveFrozenTime) {
        g_frozenTime = readStatic<double>(M.time);
        g_haveFrozenTime = true;
    } else {
        writeStatic(M.time, g_frozenTime);
    }
}

int g_rainApplied = 0;
int g_windApplied = 0;

void holdWeather() {
    const bool world = inWorld();
    const int rain = world ? levelOf(Power::Rain) : 0;
    const int wind = world ? levelOf(Power::Wind) : 0;

    const float strength = rain > 0 ? sliderPosition(rain) : 0.f;
    if (strength > 0.f) {
        writeStatic<bool>(M.raining, true);
        if (readStatic<int32_t>(M.rainTime) < kRainTimeFloor) writeStatic(M.rainTime, kRainTimeFloor);
        writeStatic(M.maxRaining, strength);
        writeStatic(M.cloudAlpha, strength);
    } else if (rain > 0 || (g_rainApplied > 0 && world)) {
        writeStatic<int32_t>(M.rainTime, 0);
        writeStatic<bool>(M.raining, false);
        writeStatic(M.maxRaining, 0.f);
    }
    g_rainApplied = rain;

    float target = NAN;
    if (wind > 0) {
        target = windOf(wind);
    } else if (strength >= kStormRain) {
        const float now = readStatic<float>(M.windTarget);
        if (std::fabs(now) < kStormWind) target = (now < 0.f ? -1.f : 1.f) * kStormWind;
    }
    g_windApplied = wind;
    if (!std::isnan(target)) {
        writeStatic(M.windTarget, target);
        writeStatic(M.windCurrent, target);
    }
}

bool unlockEntry(Il2CppObject* kills, Il2CppObject* sights, Il2CppObject* chats,
                 Il2CppString* id) {
    auto& a = il2cpp::api();
    Il2CppObject* exc = nullptr;
    void* idArg[] = {id};
    int32_t need = unboxInt(a.runtime_invoke(B.killsNeeded, nullptr, idArg, &exc));
    if (exc) return false;
    const int32_t have = unboxInt(a.runtime_invoke(B.getKills, kills, idArg, &exc));
    if (exc) return false;
    if (have < need) {
        void* setArgs[] = {id, &need};
        a.runtime_invoke(B.setKills, kills, setArgs, &exc);
        if (exc) return false;
    }
    a.runtime_invoke(B.setSeen, sights, idArg, &exc);
    if (exc) return false;
    a.runtime_invoke(B.setChatted, chats, idArg, &exc);
    return !exc;
}

void unlockBestiary() {
    using Clock = std::chrono::steady_clock;
    const auto start = Clock::now();
    auto& a = il2cpp::api();

    auto* tracker = readStatic<Il2CppObject*>(M.bestiaryTracker);
    auto* credits = readStatic<Il2CppObject*>(B.creditIds);
    if (!tracker || !credits) {
        BL_ERROR("poderes: bestiario indisponivel (tracker=%p creditos=%p)",
                 (void*)tracker, (void*)credits);
        return;
    }
    auto* kills = field<Il2CppObject*>(tracker, B.kills);
    auto* sights = field<Il2CppObject*>(tracker, B.sights);
    auto* chats = field<Il2CppObject*>(tracker, B.chats);
    Il2CppClass* dict = a.object_get_class(credits);
    const MethodInfo* tryGet = a.class_get_method_from_name(dict, "TryGetValue", 2);
    const MethodInfo* getCount = a.class_get_method_from_name(dict, "get_Count", 0);
    if (!kills || !sights || !chats || !tryGet || !getCount) {
        BL_ERROR("poderes: bestiario incompleto (mortes=%p visto=%p conversa=%p)",
                 (void*)kills, (void*)sights, (void*)chats);
        return;
    }

    Il2CppObject* exc = nullptr;
    const int total = unboxInt(a.runtime_invoke(getCount, credits, nullptr, &exc));
    if (exc) { BL_ERROR("poderes: bestiario: Count lancou excecao"); return; }

    int found = 0, failed = 0;
    for (int32_t netId = kFirstNpcNetId; netId < kLastNpcNetId && found < total; ++netId) {
        Il2CppString* id = nullptr;
        void* args[] = {&netId, &id};
        exc = nullptr;
        if (!unboxBool(a.runtime_invoke(tryGet, credits, args, &exc)) || exc) continue;
        ++found;
        if (id && !unlockEntry(kills, sights, chats, id)) ++failed;
    }
    const auto ms = std::chrono::duration_cast<std::chrono::milliseconds>(
        Clock::now() - start).count();
    if (failed > 0) {
        BL_ERROR("poderes: bestiario: %d de %d entradas falharam", failed, found);
    } else {
        BL_INFO("poderes: bestiario desbloqueado (%d de %d NPCs) em %lld ms", found, total,
                static_cast<long long>(ms));
    }
}

State installHook(const char* name, const MethodInfo* target, void* fn, void** orig) {
    if (hook::install(target, fn, orig)) {
        BL_INFO("poderes: hook em %s", name);
        return State::Ok;
    }
    BL_ERROR("poderes: falha ao hookar %s", name);
    return State::Failed;
}

template <typename Fn>
State installHook(const char* name, const MethodInfo* target, Fn fn, Fn* orig) {
    return installHook(name, target, reinterpret_cast<void*>(fn), reinterpret_cast<void**>(orig));
}

State installLightHooks() {
    void* const hooks[] = {reinterpret_cast<void*>(&hkPresent<0>),
                           reinterpret_cast<void*>(&hkPresent<1>)};
    bool any = false;
    for (size_t e = 0; e < std::size(g_engines); ++e) {
        if (!g_engines[e].present || g_engines[e].activeMap < 0) continue;
        char name[48];
        snprintf(name, sizeof(name), "%s.Present", g_engines[e].name);
        if (installHook(name, g_engines[e].present, hooks[e],
                        reinterpret_cast<void**>(&g_origPresent[e])) == State::Ok) {
            any = true;
        }
    }
    return any ? State::Ok : State::Failed;
}

std::atomic<int> g_timeRequest{-1};
std::atomic<int> g_stateHardmode{-1};
std::atomic<int> g_stateGameMode{-1};
std::atomic<int> g_stateClock{-1};
std::atomic<int> g_stateRain{-1};
std::atomic<int> g_stateWind{-1};
std::atomic<int> g_stateInfection{-1};

struct InfectionRefs {
    State state = State::NotTried;
    Il2CppObject* power = nullptr;
    int32_t enabled = -1;
    const MethodInfo* onJoin = nullptr;
} I;

Il2CppObject* biomeSpreadPower() {
    if (I.state != State::NotTried) return I.power;
    I.state = State::Failed;
    using namespace il2cpp;
    auto& a = api();
    Il2CppClass* manager = findClass({"Terraria.GameContent.Creative", "CreativePowerManager", {}});
    const MethodInfo* instance = manager ? a.class_get_method_from_name(manager, "get_Instance", 0) : nullptr;
    const int32_t byName = manager ? fieldOffset(manager, "_powersByName") : -1;
    Il2CppObject* exc = nullptr;
    Il2CppObject* self = instance && byName >= 0 ? a.runtime_invoke(instance, nullptr, nullptr, &exc) : nullptr;
    auto* dict = self && !exc ? field<Il2CppObject*>(self, byName) : nullptr;
    const MethodInfo* tryGet = dict ? a.class_get_method_from_name(a.object_get_class(dict), "TryGetValue", 2) : nullptr;
    if (!tryGet) {
        BL_ERROR("poderes: sem o CreativePowerManager; Parar infeccao fica desligado");
        return nullptr;
    }
    Il2CppObject* power = nullptr;
    void* args[] = {a.string_new("biomespread_setfrozen"), &power};
    exc = nullptr;
    const bool found = unboxBool(a.runtime_invoke(tryGet, dict, args, &exc));
    if (!found || exc || !power) {
        BL_ERROR("poderes: sem o poder biomespread_setfrozen da Jornada; Parar infeccao fica desligado");
        return nullptr;
    }
    Il2CppClass* cls = a.object_get_class(power);
    I.enabled = fieldOffset(cls, "<Enabled>k__BackingField");
    I.onJoin = a.class_get_method_from_name(cls, "OnPlayerJoining", 1);
    if (I.enabled < 0) return nullptr;
    I.power = power;
    I.state = State::Ok;
    return power;
}

void applyInfection(int command) {
    if (readStatic<int32_t>(W.netMode) == kNetClient) return;
    Il2CppObject* power = biomeSpreadPower();
    if (!power) return;
    field<uint8_t>(power, I.enabled) = command == 1 ? 1 : 0;
    BL_INFO("poderes: infeccao %s", command == 1 ? "parada" : "solta");

    auto* players = readStatic<Il2CppArray*>(W.player);
    if (!isNetHost() || !I.onJoin || !players || W.playerActive < 0) return;
    auto** all = static_cast<Il2CppObject**>(arrayData(players));
    const int local = g_localPlayer.load(std::memory_order_relaxed);
    for (int32_t i = 0; i < 255 && static_cast<uintptr_t>(i) < players->length; ++i) {
        if (i == local || !all[i] || !field<uint8_t>(all[i], W.playerActive)) continue;
        void* args[] = {&i};
        Il2CppObject* exc = nullptr;
        il2cpp::api().runtime_invoke(I.onJoin, power, args, &exc);
        if (exc) BL_ERROR("poderes: OnPlayerJoining(%d) lancou excecao", i);
    }
}

State g_stateRefs = State::NotTried;
FieldInfo* g_stateHardMode = nullptr;
const MethodInfo* g_stateGetGameMode = nullptr;
FieldInfo *g_stateTime = nullptr, *g_stateDayTime = nullptr, *g_stateRaining = nullptr,
    *g_stateMaxRaining = nullptr, *g_stateWindTarget = nullptr;

void updateWorldState() {
    if (!inWorld()) {
        for (auto* v : {&g_stateHardmode, &g_stateGameMode, &g_stateClock, &g_stateRain, &g_stateWind,
                        &g_stateInfection}) {
            v->store(-1, std::memory_order_relaxed);
        }
        return;
    }
    if (g_stateRefs == State::NotTried) {
        Il2CppClass* main = il2cpp::findClass({"Terraria", "Main", {}});
        g_stateHardMode = main ? il2cpp::findField(main, "hardMode") : nullptr;
        g_stateGetGameMode =
            main ? il2cpp::api().class_get_method_from_name(main, "get_GameMode", 0) : nullptr;
        if (main) {
            g_stateTime = il2cpp::findField(main, "time");
            g_stateDayTime = il2cpp::findField(main, "dayTime");
            g_stateRaining = il2cpp::findField(main, "raining");
            g_stateMaxRaining = il2cpp::findField(main, "maxRaining");
            g_stateWindTarget = il2cpp::findField(main, "windSpeedTarget");
        }
        g_stateRefs = g_stateHardMode && g_stateGetGameMode && g_stateTime && g_stateDayTime &&
                      g_stateRaining && g_stateMaxRaining && g_stateWindTarget
                          ? State::Ok : State::Failed;
        if (g_stateRefs == State::Failed) BL_ERROR("poderes: estado do mundo sem refs");
    }
    if (g_stateRefs != State::Ok) return;
    g_stateHardmode.store(readStatic<bool>(g_stateHardMode) ? 1 : 0, std::memory_order_relaxed);
    g_stateGameMode.store(callStatic<int32_t>(g_stateGetGameMode), std::memory_order_relaxed);

    const int32_t t = static_cast<int32_t>(readStatic<double>(g_stateTime));
    const int32_t s = readStatic<bool>(g_stateDayTime) ? t + 16200 : (t + 70200) % 86400;
    g_stateClock.store(std::clamp(s / 60, 0, 1439), std::memory_order_relaxed);
    const float rain = readStatic<bool>(g_stateRaining) ? readStatic<float>(g_stateMaxRaining) : 0.f;
    g_stateRain.store(std::clamp(static_cast<int>(std::lround(rain * 100.f)), 0, 100),
                      std::memory_order_relaxed);
    const float wind = readStatic<float>(g_stateWindTarget) / kMaxWind;
    g_stateWind.store(std::clamp(static_cast<int>(std::lround((wind + 1.f) * 50.f)), 0, 100),
                      std::memory_order_relaxed);
    Il2CppObject* spread = biomeSpreadPower();
    g_stateInfection.store(spread ? field<uint8_t>(spread, I.enabled) : -1, std::memory_order_relaxed);
}

void broadcastWorld(bool time) {
    if (!isNetHost()) return;
    sendData(kMsgWorldData, 0);
    if (time) sendData(kMsgTimeSync, 0);
}

TimeOfDay clockToGameTime(int minute) {
    constexpr int32_t kDawn = 16200, kDusk = 70200, kDay = 86400;
    const int32_t s = minute * 60;
    if (s >= kDawn && s < kDusk) return {s - kDawn, true};
    return {(s - kDusk + kDay) % kDay, false};
}

void applyTimeOfDay(int request) {
    TimeOfDay t = request >= kClockRequest ? clockToGameTime(request - kClockRequest)
                                           : kTimesOfDay[request];
    Il2CppObject* exc = nullptr;
    void* args[] = {&t.time, &t.day};
    il2cpp::api().runtime_invoke(W.skipToTime, nullptr, args, &exc);
    if (exc) { BL_ERROR("poderes: SkipToTime lancou excecao"); return; }
    g_haveFrozenTime = false;
    broadcastWorld(true);
    BL_INFO("poderes: hora -> %d (%s)", t.time, t.day ? "dia" : "noite");
}

void applyHardmode(int command) {
    if (readStatic<int32_t>(W.netMode) == kNetClient) {
        BL_INFO("poderes: hardmode so no mundo local");
        return;
    }
    const bool now = readStatic<bool>(W.hardMode);
    if (command == 1 && !now) {
        Il2CppObject* exc = nullptr;
        il2cpp::api().runtime_invoke(W.startHardmode, nullptr, nullptr, &exc);
        if (exc) BL_ERROR("poderes: StartHardmode lancou excecao");
        else BL_INFO("poderes: hardmode ligado");
    } else if (command == 2 && now) {
        writeStatic<bool>(W.hardMode, false);
        BL_INFO("poderes: hardmode desligado");
    }
    broadcastWorld(false);
}

void applyDifficulty(int level) {
    if (readStatic<int32_t>(W.netMode) == kNetClient) return;
    int32_t mode = level - 1;
    Il2CppObject* exc = nullptr;
    void* args[] = {&mode};
    il2cpp::api().runtime_invoke(W.setGameMode, nullptr, args, &exc);
    if (exc) BL_ERROR("poderes: set_GameMode lancou excecao");
    else BL_INFO("poderes: modo de jogo -> %d", mode);
    broadcastWorld(false);
}

using StaticVoidFn = void (*)(const MethodInfo*);
StaticVoidFn g_origSpawnNpc = nullptr;

void hkSpawnNPC(const MethodInfo* m) {
    const int level = levelOf(Power::NoSpawns);
    if (level > 0 && spawnMultiplierOf(level) <= 0.f) return;
    g_origSpawnNpc(m);
}

using SpawnRateFn = void (*)(Il2CppObject*, Il2CppObject*, int32_t*, int32_t*, const MethodInfo*);
SpawnRateFn g_origSpawnRate = nullptr;
State g_spawnRateHook = State::NotTried;

void hkGetSpawnRate(Il2CppObject* self, Il2CppObject* player, int32_t* rate, int32_t* max,
                    const MethodInfo* m) {
    g_origSpawnRate(self, player, rate, max, m);
    const int level = levelOf(Power::NoSpawns);
    const float mult = level > 0 ? spawnMultiplierOf(level) : 1.f;
    if (mult <= 0.f || mult == 1.f || !rate || !max) return;
    *rate = std::max<int32_t>(1, static_cast<int32_t>(*rate / mult));
    *max = std::max<int32_t>(1, static_cast<int32_t>(*max * mult));
}

void clearHostiles() {
    if (readStatic<int32_t>(W.netMode) == kNetClient) return;
    auto* npcs = readStatic<Il2CppArray*>(W.npc);
    if (!npcs) return;
    auto** all = static_cast<Il2CppObject**>(arrayData(npcs));
    for (uintptr_t i = 0; i < npcs->length; ++i) {
        Il2CppObject* n = all[i];
        if (!n || !field<uint8_t>(n, W.npcActive)) continue;
        if (field<uint8_t>(n, W.npcTown) || field<uint8_t>(n, W.npcFriendly) ||
            field<int32_t>(n, W.npcDamage) <= 0) {
            continue;
        }
        field<uint8_t>(n, W.npcActive) = 0;
        if (isNetHost()) sendData(23, static_cast<int>(i));
    }
}

constexpr int32_t kInvasionLife = 200;

bool invokeStatic(const MethodInfo* method, void** args, const char* name) {
    Il2CppObject* exc = nullptr;
    il2cpp::api().runtime_invoke(method, nullptr, args, &exc);
    if (exc) BL_ERROR("poderes: %s lancou excecao", name);
    return !exc;
}

void ensureDayTime(bool day) {
    if (readStatic<bool>(E.dayTime) != day) applyTimeOfDay(day ? 0 : 2);
}

void startInvasion(int32_t type) {
    auto* players = readStatic<Il2CppArray*>(W.player);
    auto** all = players ? static_cast<Il2CppObject**>(arrayData(players)) : nullptr;
    Il2CppObject* lifted = nullptr;
    for (uintptr_t i = 0; all && i < players->length && i < 255; ++i) {
        Il2CppObject* p = all[i];
        if (!p || !field<uint8_t>(p, W.playerActive)) continue;
        if (field<int32_t>(p, E.statLifeMax) >= kInvasionLife) { lifted = nullptr; break; }
        if (!lifted) lifted = p;
    }
    const int32_t oldMax = lifted ? field<int32_t>(lifted, E.statLifeMax) : 0;
    if (lifted) field<int32_t>(lifted, E.statLifeMax) = kInvasionLife;
    void* args[] = {&type};
    invokeStatic(E.startInvasion, args, "Main.StartInvasion");
    if (lifted) field<int32_t>(lifted, E.statLifeMax) = oldMax;
    if (readStatic<int32_t>(E.invasionType) != type) BL_INFO("poderes: invasao %d recusada pelo jogo", type);
}

void startEvent(Power p) {
    if (readStatic<int32_t>(W.netMode) == kNetClient) return;
    bool yes = true;
    void* flag[] = {&yes};
    switch (p) {
    case Power::BloodMoon:
        ensureDayTime(false);
        writeStatic<bool>(E.bloodMoon, true);
        break;
    case Power::Eclipse:
        ensureDayTime(true);
        writeStatic<bool>(E.eclipse, true);
        break;
    case Power::SlimeRain:
        if (readStatic<bool>(E.raining)) invokeStatic(E.stopRain, flag, "Main.StopRain");
        invokeStatic(E.startSlimeRain, flag, "Main.StartSlimeRain");
        break;
    case Power::GoblinArmy: startInvasion(1); break;
    case Power::FrostLegion: startInvasion(2); break;
    case Power::Pirates: startInvasion(3); break;
    case Power::Martians: startInvasion(4); break;
    case Power::PumpkinMoon:
        ensureDayTime(false);
        invokeStatic(E.startPumpkinMoon, nullptr, "Main.startPumpkinMoon");
        break;
    case Power::FrostMoon:
        ensureDayTime(false);
        invokeStatic(E.startSnowMoon, nullptr, "Main.startSnowMoon");
        break;
    case Power::Party:
        if (!readStatic<bool>(E.manualParty) && !readStatic<bool>(E.genuineParty)) {
            invokeStatic(E.toggleParty, nullptr, "BirthdayParty.ToggleManualParty");
        }
        break;
    case Power::Sandstorm:
        if (!readStatic<bool>(E.sandstorm)) invokeStatic(E.startSandstorm, nullptr, "Sandstorm.StartSandstorm");
        break;
    default:
        return;
    }
    broadcastWorld(false);
    BL_INFO("poderes: evento %d comecado", static_cast<int>(p));
}

void stopEvents() {
    if (readStatic<int32_t>(W.netMode) == kNetClient) return;
    bool announce = true, win = false;
    void* announceArg[] = {&announce};
    void* winArg[] = {&win};
    writeStatic<bool>(E.bloodMoon, false);
    writeStatic<bool>(E.eclipse, false);
    if (readStatic<bool>(E.slimeRain)) invokeStatic(E.stopSlimeRain, announceArg, "Main.StopSlimeRain");
    writeStatic<int32_t>(E.invasionType, 0);
    writeStatic<int32_t>(E.invasionSize, 0);
    invokeStatic(E.stopMoonEvent, nullptr, "Main.stopMoonEvent");
    invokeStatic(E.stopOldOnesArmy, winArg, "DD2Event.StopInvasion");
    writeStatic<bool>(E.manualParty, false);
    writeStatic<bool>(E.genuineParty, false);
    writeStatic<bool>(E.manualLanterns, false);
    writeStatic<bool>(E.genuineLanterns, false);
    if (readStatic<bool>(E.sandstorm)) invokeStatic(E.stopSandstorm, nullptr, "Sandstorm.StopSandstorm");
    broadcastWorld(false);
    BL_INFO("poderes: eventos parados");
}

void clearInventory() {
    Il2CppObject* p = localPlayer();
    auto* inv = p ? field<Il2CppArray*>(p, W.inventory) : nullptr;
    if (!inv) return;
    auto** items = static_cast<Il2CppObject**>(arrayData(inv));
    const int n = std::min<int>(kInventorySlots, static_cast<int>(inv->length));
    int cleared = 0;
    for (int i = 0; i < n; ++i) {
        Il2CppObject* it = items[i];
        if (!it || field<uint8_t>(it, W.favorited)) continue;
        Il2CppObject* exc = nullptr;
        il2cpp::api().runtime_invoke(W.turnToAir, it, nullptr, &exc);
        if (!exc) ++cleared;
    }
    BL_INFO("poderes: mochila limpa (%d espacos)", cleared);
}

int g_revealX = -1;
int g_revealFrames = 0;
std::chrono::steady_clock::time_point g_revealStart;

void startReveal() {
    if (g_revealX >= 0) return;
    g_revealX = 0;
    g_revealFrames = 0;
    g_revealStart = std::chrono::steady_clock::now();
}

void revealStep() {
    using Clock = std::chrono::steady_clock;
    Il2CppObject* map = callStatic<Il2CppObject*>(W.getMap);
    if (!map) { g_revealX = -1; return; }
    const int32_t maxX = readStatic<int32_t>(W.maxTilesX);
    const int32_t maxY = readStatic<int32_t>(W.maxTilesY);
    bool threw = false;
    ++g_revealFrames;
    g_revealX = revealMapColumns(map, W.updateLighting, g_revealX, maxX, maxY,
                                 Clock::now() + kRevealBudget, &threw);
    if (threw) {
        BL_ERROR("poderes: revelar o mapa parou na coluna %d de %d (UpdateLighting lancou)",
                 g_revealX, maxX);
    } else if (g_revealX < maxX) {
        return;
    } else {
        const auto ms = std::chrono::duration_cast<std::chrono::milliseconds>(
            Clock::now() - g_revealStart).count();
        BL_INFO("poderes: mapa revelado (%dx%d) em %d quadros, %lld ms", maxX, maxY,
                g_revealFrames, static_cast<long long>(ms));
    }
    g_revealX = -1;
    reinterpret_cast<void (*)(bool, const MethodInfo*)>(il2cpp::methodPointer(W.setRefreshMap))(
        true, W.setRefreshMap);
}

struct SectionStream {
    int client;
    int next;
};
std::vector<SectionStream> g_streams;

int g_revealWait = -1;
int g_waitLoaded = -1;
int g_waitStall = 0;

void streamSections() {
    const int sx = readStatic<int32_t>(W.maxSectionsX);
    const int sy = readStatic<int32_t>(W.maxSectionsY);
    auto* players = readStatic<Il2CppArray*>(W.player);
    const int total = sx * sy;
    for (size_t k = 0; k < g_streams.size();) {
        SectionStream& s = g_streams[k];
        auto* p = players && static_cast<uintptr_t>(s.client) < players->length
            ? static_cast<Il2CppObject**>(arrayData(players))[s.client] : nullptr;
        for (int n = 0; p && field<uint8_t>(p, W.playerActive) && n < kSectionsPerFrame && s.next < total; ++n, ++s.next) {
            int32_t client = s.client, x = s.next % sx, y = s.next / sx;
            void* args[3] = {&client, &x, &y};
            Il2CppObject* exc = nullptr;
            il2cpp::api().runtime_invoke(W.sendSection, nullptr, args, &exc);
        }
        if (!p || !field<uint8_t>(p, W.playerActive) || s.next >= total) {
            BL_INFO("poderes: mundo mandado ao jogador %d para revelar o mapa (%d partes)", s.client, total);
            g_streams.erase(g_streams.begin() + static_cast<std::ptrdiff_t>(k));
            continue;
        }
        ++k;
    }
}

int loadedSections() {
    Il2CppObject* manager = callStatic<Il2CppObject*>(W.getSectionManager);
    if (!manager) return -1;
    using Fn = bool (*)(Il2CppObject*, int32_t, int32_t, const MethodInfo*);
    const auto loaded = reinterpret_cast<Fn>(il2cpp::methodPointer(W.sectionLoaded));
    const int sx = readStatic<int32_t>(W.maxSectionsX);
    const int sy = readStatic<int32_t>(W.maxSectionsY);
    int count = 0;
    for (int y = 0; y < sy; ++y) {
        for (int x = 0; x < sx; ++x) count += loaded(manager, x, y, W.sectionLoaded) ? 1 : 0;
    }
    return count;
}

void requestRevealFromServer() {
    if (g_revealWait >= 0 || g_revealX >= 0) return;
    if (!sendToServer("reveal 0")) {
        startReveal();
        return;
    }
    g_revealWait = 0;
    g_waitLoaded = -1;
    g_waitStall = 0;
}

void waitForSections() {
    if (++g_revealWait % kSectionWaitCheck != 0) return;
    const int loaded = loadedSections();
    const int total = readStatic<int32_t>(W.maxSectionsX) * readStatic<int32_t>(W.maxSectionsY);
    if (loaded > g_waitLoaded) g_waitStall = 0;
    else ++g_waitStall;
    g_waitLoaded = loaded;
    if (loaded >= 0 && loaded < total && g_waitStall < kSectionStallChecks) return;

    if (loaded < total) {
        BL_INFO("poderes: o servidor mandou %d de %d partes do mundo; revelando o que chegou", loaded, total);
    }
    g_revealWait = -1;
    startReveal();
}

struct MapHold {
    bool down = false, fired = false;
    int32_t x = 0, y = 0;
    std::chrono::steady_clock::time_point since;
} g_hold;

struct UnityTouch {
    int32_t fingerId;
    Vec2 position;
    uint8_t rest[0x44 - 12];
};
static_assert(sizeof(UnityTouch) == 0x44, "layout do UnityEngine.Touch");

bool touchPoint(int32_t* x, int32_t* y) {
    if (callStatic<int32_t>(W.getTouchCount) != 1) return false;
    int32_t index = 0;
    const UnityTouch t = reinterpret_cast<UnityTouch (*)(int32_t, const MethodInfo*)>(
        il2cpp::methodPointer(W.getTouch))(index, W.getTouch);
    *x = static_cast<int32_t>(t.position.x);
    *y = static_cast<int32_t>(callStatic<int32_t>(W.getDeviceHeight) - t.position.y);
    return true;
}

bool solidAt(Vec2 pos, int32_t w, int32_t h) {
    void* args[] = {&pos, &w, &h};
    Il2CppObject* exc = nullptr;
    Il2CppObject* r = il2cpp::api().runtime_invoke(W.solidCollision, nullptr, args, &exc);
    return exc || unboxBool(r);
}

float uiToDevice(int32_t devX, int32_t devY) {
    Il2CppObject* ui = callStatic<Il2CppObject*>(W.getUiMouse);
    if (!ui) return 0.f;
    const int32_t sum = field<int32_t>(ui, W.uiMouseX) + field<int32_t>(ui, W.uiMouseY);
    return sum > 0 ? static_cast<float>(devX + devY) / static_cast<float>(sum) : 0.f;
}

void teleportToMapPoint(int32_t mx, int32_t my) {
    Il2CppObject* p = localPlayer();
    if (!p) return;
    const Vec2 center = callStatic<Vec2>(W.getMapPos);
    const float k = uiToDevice(mx, my);
    const float scale = callStatic<float>(W.getMapScale) * k;
    const int32_t sw = callStatic<int32_t>(W.getDeviceWidth);
    const int32_t sh = callStatic<int32_t>(W.getDeviceHeight);
    if (scale <= 0.f) {
        BL_ERROR("poderes: teleporte sem escala do mapa (interface x%.3f)", k);
        return;
    }
    const float tx = center.x + (static_cast<float>(mx) - sw * 0.5f) / scale;
    const float ty = center.y + (static_cast<float>(my) - sh * 0.5f) / scale;

    const auto& e = game().entity;
    const int32_t w = field<int32_t>(p, e.width), h = field<int32_t>(p, e.height);
    const float maxX = static_cast<float>(readStatic<int32_t>(W.maxTilesX) - kWorldMarginTiles);
    const float maxY = static_cast<float>(readStatic<int32_t>(W.maxTilesY) - kWorldMarginTiles);
    const float clampedX = std::max<float>(kWorldMarginTiles, std::min(tx, maxX));
    const float clampedY = std::max<float>(kWorldMarginTiles, std::min(ty, maxY));
    Vec2 target{clampedX * 16.f - w * 0.5f, (clampedY + 1.f) * 16.f - h};

    Vec2 spot = target;
    bool found = !solidAt(spot, w, h);
    for (int i = 1; !found && i <= kTeleportSearchTiles; ++i) {
        spot = Vec2{target.x, target.y - i * 16.f};
        if (spot.y >= kWorldMarginTiles * 16.f && !solidAt(spot, w, h)) { found = true; break; }
        spot = Vec2{target.x, target.y + i * 16.f};
        if (spot.y <= maxY * 16.f && !solidAt(spot, w, h)) { found = true; break; }
    }
    if (!found) spot = target;

    int32_t style = 0, extra = 0;
    void* args[] = {&spot, &style, &extra};
    Il2CppObject* exc = nullptr;
    il2cpp::api().runtime_invoke(W.teleport, p, args, &exc);
    if (exc) { BL_ERROR("poderes: Player.Teleport lancou excecao"); return; }
    BL_INFO("poderes: teleporte: toque (%d,%d) tela %dx%d mapa (%.1f,%.1f) x%.2f (interface "
            "x%.3f) -> tile (%.1f,%.1f) -> (%.0f,%.0f)%s", mx, my, sw, sh, center.x, center.y,
            scale, k, tx, ty, spot.x, spot.y, found ? "" : " (sem ar por perto)");
}

void watchMapHold() {
    using Clock = std::chrono::steady_clock;
    int32_t mx = 0, my = 0;
    if (!callStatic<bool>(W.getMapFullscreen) || !touchPoint(&mx, &my)) {
        g_hold.down = false;
        return;
    }
    const int32_t slop = std::max(4, callStatic<int32_t>(W.getDeviceHeight) / kMapHoldSlopDiv);
    const auto now = Clock::now();
    if (!g_hold.down || std::abs(mx - g_hold.x) > slop || std::abs(my - g_hold.y) > slop) {
        g_hold = MapHold{true, false, mx, my, now};
        return;
    }
    if (g_hold.fired || now - g_hold.since < kMapHoldTime) return;
    g_hold.fired = true;
    teleportToMapPoint(g_hold.x, g_hold.y);
}

void tickWorldPowers() {
    const int time = g_timeRequest.exchange(-1, std::memory_order_relaxed);
    const bool clearInv = g_level[static_cast<int>(Power::ClearInventory)].exchange(0) > 0;
    const bool reveal = g_level[static_cast<int>(Power::RevealMap)].exchange(0) > 0;
    const int hardmode = g_level[static_cast<int>(Power::Hardmode)].exchange(0);
    const int difficulty = g_level[static_cast<int>(Power::Difficulty)].exchange(0);
    const bool clearEnemies = g_level[static_cast<int>(Power::ClearEnemies)].exchange(0) > 0;
    const int infection = g_level[static_cast<int>(Power::StopInfection)].exchange(0);
    const bool stopAll = g_level[static_cast<int>(Power::StopEvents)].exchange(0) > 0;
    bool events[kPowerCount] = {};
    for (int i = 0; i < kPowerCount; ++i) {
        if (isEventPower(static_cast<Power>(i))) events[i] = g_level[i].exchange(0) > 0;
    }
    if (!inWorld()) {
        g_revealX = -1;
        g_revealWait = -1;
        g_streams.clear();
        g_hold.down = false;
        return;
    }
    if (time >= 0 && g_timeRefs) applyTimeOfDay(time);
    if (clearInv && g_inventoryRefs) clearInventory();
    if (reveal && g_mapRefs) {
        if (isNetClientOnly() && g_sectionRefs) requestRevealFromServer();
        else startReveal();
    }
    if (g_revealWait >= 0) waitForSections();
    if (g_revealX >= 0) revealStep();
    if (!g_streams.empty()) {
        if (g_sectionRefs) streamSections();
        else g_streams.clear();
    }
    if (hardmode > 0 && g_hardmodeRefs) applyHardmode(hardmode);
    if (difficulty > 0 && g_hardmodeRefs) applyDifficulty(difficulty);

    const int spawns = levelOf(Power::NoSpawns);
    if (spawns > 0 && g_spawnRefs && g_spawnHook == State::NotTried) {
        g_spawnHook = installHook("NPC.SpawnNPC", W.spawnNpc, &hkSpawnNPC, &g_origSpawnNpc);
    }
    if (spawns > 0 && W.getSpawnRate && g_spawnRateHook == State::NotTried) {
        g_spawnRateHook = installHook("NPC.Spawner.GetSpawnRate", W.getSpawnRate, &hkGetSpawnRate,
                                      &g_origSpawnRate);
    }
    if (clearEnemies && g_spawnRefs) clearHostiles();
    if (infection > 0) applyInfection(infection);
    if (stopAll && g_eventRefs) stopEvents();
    for (int i = 0; i < kPowerCount; ++i) {
        if (events[i] && g_eventRefs) startEvent(static_cast<Power>(i));
    }
    if (levelOf(Power::MapTeleport) > 0 && g_teleportRefs) watchMapHold();
    else g_hold.down = false;
}

int g_sentLevel[kPowerCount];
bool g_sentInit = false;
int g_heldLevel[kPowerCount];
int g_resyncFrame = 0;

void forwardWorldPowers() {
    if (!g_sentInit) {
        for (int& v : g_sentLevel) v = -1;
        g_sentInit = true;
    }
    if (!inWorld()) {
        for (int& v : g_sentLevel) v = -1;
        return;
    }
    const int time = g_timeRequest.exchange(-1, std::memory_order_relaxed);
    if (time >= 0) sendToServer("time " + std::to_string(time));
    for (int i = 0; i < kPowerCount; ++i) {
        if (!isWorldCommand(static_cast<Power>(i))) continue;
        const int v = g_level[i].exchange(0);
        if (v > 0) sendToServer("power " + std::to_string(i) + " " + std::to_string(v));
    }
    for (Power p : {Power::TimeStop, Power::Rain, Power::Wind, Power::NoSpawns}) {
        const int i = static_cast<int>(p);
        const int v = levelOf(p);
        if (v == g_sentLevel[i]) continue;
        if (!(v == 0 && g_sentLevel[i] < 0)) {
            sendToServer("power " + std::to_string(i) + " " + std::to_string(v));
        }
        g_sentLevel[i] = v;
    }
}

void resyncWorldFromHost() {
    if (!isNetHost() || !inWorld()) return;
    bool changed = false, holding = false;
    for (Power p : {Power::TimeStop, Power::Rain, Power::Wind}) {
        const int i = static_cast<int>(p);
        const int v = levelOf(p);
        if (v != g_heldLevel[i]) changed = true;
        if (v > 0) holding = true;
        g_heldLevel[i] = v;
    }
    if (changed || (holding && ++g_resyncFrame >= kWorldResyncFrames)) {
        g_resyncFrame = 0;
        broadcastWorld(true);
    }
}

}

void revealMapForClient(int client) {
    if (!isNetHost() || client < 0 || client >= 255) return;
    for (const SectionStream& s : g_streams) {
        if (s.client == client) return;
    }
    g_streams.push_back({client, 0});
}

bool setWorldPowerFromNet(int id, int level) {
    if (id < 0 || id >= kPowerCount || !isWorldPower(static_cast<Power>(id))) return false;
    setPower(id, level);
    return true;
}

void setPower(int id, int level) {
    if (id < 0 || id >= kPowerCount) return;
    if (level < 0) level = 0;
    if (level > kMaxLevel[id]) level = kMaxLevel[id];
    g_level[id].store(level, std::memory_order_relaxed);
}

void setTimeOfDay(int request) {
    const bool button = request >= 0 && request < static_cast<int>(std::size(kTimesOfDay));
    const bool clock = request >= kClockRequest && request < kClockRequest + 1440;
    if (!button && !clock) return;
    g_timeRequest.store(request, std::memory_order_relaxed);
}

int worldHardmode() { return g_stateHardmode.load(std::memory_order_relaxed); }
int worldGameMode() { return g_stateGameMode.load(std::memory_order_relaxed); }
int worldInfectionStopped() { return g_stateInfection.load(std::memory_order_relaxed); }
int worldClockMinute() { return g_stateClock.load(std::memory_order_relaxed); }
int worldRainPosition() { return g_stateRain.load(std::memory_order_relaxed); }
int worldWindPosition() { return g_stateWind.load(std::memory_order_relaxed); }

void tickPowers() {
    if (cheatsBlocked()) {
        for (int i = 0; i < kPowerCount; ++i) g_level[i].store(0, std::memory_order_relaxed);
        g_timeRequest.store(-1, std::memory_order_relaxed);
    }
    updateWorldState();
    if (isNetClientOnly()) forwardWorldPowers();
    else resyncWorldFromHost();
    bool any = false, resetHook = false;
    for (int i = 0; i < kPowerCount; ++i) {
        if (g_level[i].load(std::memory_order_relaxed) == 0) continue;
        any = true;
        if (usesResetHook(static_cast<Power>(i))) resetHook = true;
    }
    const bool flying = levelOf(Power::Fly) > 0 && inWorld();
    if (g_wasFlying && !flying) {
        g_fallGrace = inWorld() && g_flyRefs;
        g_graceFell = false;
        g_graceStill = 0;
    }
    g_wasFlying = flying;
    if (!inWorld()) g_fallGrace = false;
    if (g_fallGrace) resetHook = true;
    g_anyPlayerPower.store(resetHook, std::memory_order_relaxed);
    const bool pending = g_timeRequest.load(std::memory_order_relaxed) >= 0 || g_revealX >= 0 ||
                         g_revealWait >= 0 || !g_streams.empty();
    if (!any && !pending && !g_fallGrace && g_rainApplied == 0 && g_windApplied == 0) {
        g_haveFrozenTime = false;
        return;
    }

    if (g_refs == State::NotTried) g_refs = resolveRefs() ? State::Ok : State::Failed;
    if (g_refs != State::Ok) return;

    Il2CppObject* exc = nullptr;
    const int localIndex = unboxInt(il2cpp::api().runtime_invoke(g_getMyPlayer, nullptr, nullptr, &exc));
    g_localPlayer.store(exc ? -1 : localIndex, std::memory_order_relaxed);

    if (resetHook && g_playerHook == State::NotTried) {
        g_playerHook = installHook("Player.ResetEffects", g_reset, &hkResetEffects, &g_origReset);
    }
    if (levelOf(Power::TimeStop) > 0 && g_timeHooks == State::NotTried) {
        g_timeHooks = installHook("NPC.UpdateNPC", g_npcUpdate, &hkUpdateNPC, &g_origNpcUpdate);
        if (g_timeHooks == State::Ok) {
            g_timeHooks = installHook("Projectile.Update", g_projUpdate, &hkProjUpdate,
                                      &g_origProjUpdate);
        }
    }
    const bool keepMana = godKeepsMana() && g_revengeUpdate;
    if (((levelOf(Power::Fly) > 0 && g_flyRefs) || keepMana) && g_playerUpdate &&
        g_playerUpdateHook == State::NotTried) {
        g_playerUpdateHook = installHook("Player.Update", g_playerUpdate, &hkPlayerUpdate,
                                         &g_origPlayerUpdate);
    }
    if (keepMana && g_playerUpdateHook == State::Ok && g_revengeHook == State::NotTried) {
        g_revengeHook = installHook("CoinLossRevengeSystem.Update", g_revengeUpdate,
                                    &hkRevengeUpdate, &g_origRevengeUpdate);
    }
    if (levelOf(Power::XRay) > 0 && g_lightRefs && g_lightHooks == State::NotTried) {
        g_lightHooks = installLightHooks();
    }
    holdClock();
    if (g_weatherRefs) holdWeather();

    if (levelOf(Power::FastRespawn) > 0 && inWorld()) {
        Il2CppObject* p = localPlayer();
        if (p && field<uint8_t>(p, P.dead) && field<int32_t>(p, P.respawnTimer) > 0) {
            field<int32_t>(p, P.respawnTimer) = 0;
        }
    }

    if (g_level[static_cast<int>(Power::Bestiary)].exchange(0) > 0 && inWorld() &&
        g_bestiaryRefs) {
        unlockBestiary();
    }
    tickWorldPowers();
}

}
