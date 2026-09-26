#include "menu/Powers.h"
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

namespace bl::runtime {

namespace {

constexpr int kPowerCount = static_cast<int>(Power::Count);

// Quantos niveis cada poder tem, na ordem do enum. Liga/desliga = 1.
constexpr int kMaxLevel[] = {3, 2, 2, 1, 1, 1, 1, 1, 1, 2, 1, 1, 3, 3, 1, 2, 1, 1, 1, 2, 4, 1};
static_assert(std::size(kMaxLevel) == kPowerCount, "um nivel maximo por poder");

// Indice 0 = desligado.
constexpr float kDamage[] = {1.f, 2.f, 5.f, 10.f};
constexpr float kRunSpeed[] = {1.f, 2.f, 3.f};
// Somado ao jumpSpeed, que parte de 5,01: +5 e o dobro, +10 o triplo.
constexpr float kJumpBoost[] = {0.f, 5.f, 10.f};
// pickSpeed multiplica o tempo de uso da ferramenta: menor e mais rapido.
constexpr float kToolSpeed = 0.25f;

// Pixels por quadro do voo. A 60 quadros, 10 px sao ~37 blocos por segundo; a
// corrida das Botas de Hermes fica perto de 6.
constexpr float kFlySpeed[] = {0.f, 10.f, 22.f};
// Mais que isto num quadro so nao e voo: e teleporte (espelho, cama, renascer),
// e o voo deixa acontecer.
constexpr float kTeleportDistance = 160.f;
// Distancia minima da borda do mundo, a mesma que o Player.Ghost respeita.
// Mais perto o jogo le bloco fora da matriz.
constexpr float kWorldMargin = 640.f;
// Quadros parado no chao que encerram a carencia de queda de quem desligou o
// voo sem estar no ar.
constexpr int kGraceStillFrames = 10;

// Teto de lacaios e sentinelas. O ResetEffects parte de 1 e os acessorios somam.
constexpr int32_t kUnlimitedMinions = 999;

// Main.maxRaining por nivel. O ChangeRain do jogo sorteia de 0,2 a 0,9. "Chuva"
// fica abaixo de 0,5, onde comeca a tempestade (_maxRain), para os dois niveis
// nao virarem o mesmo num dia de vento.
constexpr float kRainStrength[] = {0.f, 0.2f, 0.45f, 0.9f};
constexpr int kStorm = 3;
// Tempestade e nuvem >= 0,5 E |vento| >= 0,4 (Main.UpdateWindyDayState). Sem o
// poder do vento ligado, a tempestade garante este minimo.
constexpr float kStormWind = 0.6f;
// O UpdateTime para a chuva quando rainTime zera; mantido acima disto, nunca.
constexpr int32_t kRainTimeFloor = 3600;
// |Main.windSpeedTarget| por nivel: calmo, brisa, ventania. O slider da Jornada
// vai de -0,8 a 0,8. O empurrao no jogador (Player.HorizontalMovement) so olha o
// sinal e um limiar, entao passar de 0,8 so mudaria o desenho.
constexpr float kWindStrength[] = {0.f, 0.f, 0.25f, 0.8f};

// Ids de NPC percorridos no bestiario. Os negativos (variantes) descem a -65.
constexpr int32_t kFirstNpcNetId = -128;
constexpr int32_t kLastNpcNetId = 4096;

// Os botoes de hora da Jornada: Main.SkipToTime(tempo, dia). O relogio do jogo
// conta a partir do amanhecer (4:30) de dia e do anoitecer (19:30) de noite.
struct TimeOfDay { int32_t time; bool day; };
constexpr TimeOfDay kTimesOfDay[] = {
    {0, true},        // amanhecer, 4:30
    {27000, true},    // meio-dia
    {0, false},       // anoitecer, 19:30
    {16200, false},   // meia-noite
};

// Mochila: os 50 espacos de cima (barra rapida + 40). Moeda e municao vem
// depois (50..57) e ficam.
constexpr int kInventorySlots = 50;

// Revelar o mapa: tempo por quadro. O mapa inteiro de um mundo grande sao 20
// milhoes de tiles; de uma vez so, o jogo travava segundos.
constexpr auto kRevealBudget = std::chrono::milliseconds(12);

// Teleporte no mapa: segurar parado este tempo. "Parado" e dentro de 1/40 da
// altura da tela — o dedo treme, e arrastar o mapa anda muito mais que isso.
// 2,5 s ficava comprido na mao; 2 s ainda nao confunde com arrastar.
constexpr auto kMapHoldTime = std::chrono::milliseconds(2000);
constexpr int kMapHoldSlopDiv = 40;
// Bloco livre mais perto do toque: sobe ate isto de tiles, depois desce.
constexpr int kTeleportSearchTiles = 150;
// Borda do mundo que o teleporte respeita, em tiles (a mesma margem do voo).
constexpr int kWorldMarginTiles = 41;

constexpr int32_t kNetClient = 1;      // Main.netMode de quem entrou num servidor

// Mensagens do jogo usadas para mandar o mundo aos clientes (MessageID).
constexpr int kMsgWorldData = 7;
constexpr int kMsgTimeSync = 18;
// Com poder de mundo segurando algo (tempo parado, chuva, vento), o servidor
// reenvia o mundo nesse intervalo, em quadros: o cliente simula o relogio e o
// clima sozinho entre uma sincronizacao e outra.
constexpr int kWorldResyncFrames = 60;

/**
 * Os poderes que mexem no MUNDO, nao no jogador. No cliente eles valem na
 * tela dele, mas quem manda no mundo e o servidor: o menu do cliente tambem
 * pede a ele (ver forwardWorldPowers).
 */
bool isWorldPower(Power p) {
    switch (p) {
    case Power::TimeStop: case Power::Rain: case Power::Wind: case Power::NoSpawns:
    case Power::Hardmode: case Power::Difficulty:
        return true;
    default:
        return false;
    }
}

std::atomic<int> g_level[kPowerCount];

int levelOf(Power p) { return g_level[static_cast<int>(p)].load(std::memory_order_relaxed); }

/** Poderes que vivem no hook do ResetEffects. */
bool usesResetHook(Power p) {
    switch (p) {
    case Power::TimeStop: case Power::XRay: case Power::Rain: case Power::Wind:
    case Power::Bestiary: case Power::NoSpawns: case Power::MapTeleport:
    case Power::ClearInventory: case Power::RevealMap: case Power::Hardmode:
    case Power::Difficulty: case Power::FastRespawn:
        return false;
    default:
        return true;
    }
}

// ------------------------------ refs ------------------------------

struct Vec2 { float x, y; };

struct PlayerFields {
    int32_t whoAmI, meleeDamage, magicDamage, rangedDamage, minionDamage, moveSpeed,
        jumpSpeedBoost, noFallDmg, creativeGodMode, statLife, statLifeMax2, statMana,
        statManaMax2, manaCost, breath, breathMax, hasJumpOption_Cloud,
        canJumpAgain_Cloud, pickSpeed, nightVision, findTreasure, detectCreature,
        dangerSense, maxMinions, maxTurrets, dead, respawnTimer;
} P;

// So o voo usa. Separado para um campo faltando desligar o voo, e nao tudo.
struct FlyFields {
    int32_t position, velocity, width, height, dead, controlLeft, controlRight,
        controlUp, controlDown, controlJump, fallStart, fallStart2;
} F;

// Estaticos do Main.
struct MainStatics {
    FieldInfo *time, *raining, *rainTime, *maxRaining, *cloudAlpha, *windCurrent,
        *windTarget, *leftWorld, *rightWorld, *topWorld, *bottomWorld, *bestiaryTracker;
} M;

int32_t g_npcImmune = -1;       // NPC.immune (int[], um por jogador)
int32_t g_projHostile = -1;     // Projectile.hostile
int32_t g_projFriendly = -1;    // Projectile.friendly

const MethodInfo* g_getMyPlayer = nullptr;
const MethodInfo* g_reset = nullptr;
const MethodInfo* g_playerUpdate = nullptr;
const MethodInfo* g_npcUpdate = nullptr;
const MethodInfo* g_projUpdate = nullptr;

/**
 * Os dois motores de luz. O jogo usa um de cada vez, conforme o modo de luz
 * das opcoes: LightingEngine para Cor/Branco, LegacyLighting para
 * Retro/Psicodelico. Os dois calculam num mapa de trabalho e o Present troca
 * pelo ativo, que e o que o desenho le.
 */
struct LightEngine {
    const char* name;
    const MethodInfo* present = nullptr;
    int32_t activeMap = -1;     // _activeLightMap
};
LightEngine g_engines[] = {{"LightingEngine"}, {"LegacyLighting"}};
int32_t g_mapColors = -1;       // LightMap._colors (Vector3[])

struct BestiaryRefs {
    int32_t kills = -1, sights = -1, chats = -1;   // BestiaryUnlocksTracker
    FieldInfo* creditIds = nullptr;   // ContentSamples.NpcBestiaryCreditIdsByNpcNetIds
    const MethodInfo *killsNeeded = nullptr, *getKills = nullptr, *setKills = nullptr,
        *setSeen = nullptr, *setChatted = nullptr;
} B;

/** Mundo: hora, hardmode, dificuldade, spawn, mapa, mochila, teleporte. */
struct WorldRefs {
    FieldInfo *hardMode = nullptr, *netMode = nullptr, *maxTilesX = nullptr, *maxTilesY = nullptr,
        *npc = nullptr, *player = nullptr;
    const MethodInfo *getGameMode = nullptr, *setGameMode = nullptr, *skipToTime = nullptr,
        *startHardmode = nullptr, *spawnNpc = nullptr, *getMap = nullptr, *setRefreshMap = nullptr,
        *updateLighting = nullptr, *turnToAir = nullptr, *teleport = nullptr,
        *solidCollision = nullptr, *getMapFullscreen = nullptr, *getTouchCount = nullptr,
        *getTouch = nullptr, *getDeviceWidth = nullptr, *getDeviceHeight = nullptr,
        *getMapPos = nullptr,
        *getMapScale = nullptr, *getUiMouse = nullptr;
    int32_t npcActive = -1, npcTown = -1, npcFriendly = -1, npcDamage = -1;
    int32_t inventory = -1, favorited = -1;
    int32_t uiMouseX = -1, uiMouseY = -1;   // XNAUnityRunner.MouseStateBackup
} W;

enum class State { NotTried, Ok, Failed };
State g_refs = State::NotTried;
// Refs de cada grupo opcional: faltar uma desliga so aquele poder.
bool g_flyRefs = false, g_weatherRefs = false, g_lightRefs = false, g_bestiaryRefs = false;
bool g_timeRefs = false, g_hardmodeRefs = false, g_spawnRefs = false, g_mapRefs = false,
    g_inventoryRefs = false, g_teleportRefs = false;
State g_spawnHook = State::NotTried;
State g_playerHook = State::NotTried;
State g_timeHooks = State::NotTried;
State g_flyHook = State::NotTried;
State g_lightHooks = State::NotTried;

// O jogador deste aparelho. -1 fora do mundo. Escrito no DoUpdate, lido nos
// hooks — todos na thread do jogo, o atomico e so para deixar isso explicito.
std::atomic<int> g_localPlayer{-1};

struct FieldName { int32_t* dst; const char* name; };

/** Resolve os offsets; loga e devolve false se faltar algum. */
template <size_t N>
bool resolveOffsets(Il2CppClass* cls, const FieldName (&fields)[N]) {
    bool ok = true;
    for (const auto& c : fields) {
        *c.dst = il2cpp::fieldOffset(cls, c.name);   // loga o que faltar
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
    g_playerUpdate = il2cpp::api().class_get_method_from_name(player, "Update", 1);
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
    // GetKillCount tem dois overloads de um parametro (NPC e string): pelo nome
    // so, sairia qualquer um.
    B.killsNeeded = methodBySignature(enemy, "int GetKillCountNeeded(string persistentId)");
    B.getKills = methodBySignature(kills, "int GetKillCount(string persistentId)");
    B.setKills = methodBySignature(kills,
                                   "void SetKillCountDirectly(string persistentId, int killCount)");
    B.setSeen = methodBySignature(sights, "void SetWasSeenDirectly(string persistentId)");
    B.setChatted = methodBySignature(chats, "void SetWasChatWithDirectly(string persistentId)");
    return ok && M.bestiaryTracker && B.creditIds && B.killsNeeded && B.getKills &&
           B.setKills && B.setSeen && B.setChatted;
}

/** Um grupo opcional: loga o que faltou e diz se o grupo inteiro esta la. */
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

    W.player = findField(main, "player");
    W.inventory = fieldOffset(player, "inventory");
    W.favorited = item ? fieldOffset(item, "favorited") : -1;
    W.turnToAir = methodBySignature(item, "void TurnToAir(bool fullReset)");
    g_inventoryRefs = allFound("mochila", {W.player, W.turnToAir}) && W.inventory >= 0 &&
                      W.favorited >= 0;

    W.teleport = methodBySignature(player, "void Teleport(Vector2 newPos, int Style, int extraInfo)");
    W.solidCollision = methodBySignature(collision,
                                         "bool SolidCollision(Vector2 Position, int Width, int Height)");
    W.getMapFullscreen = a.class_get_method_from_name(main, "get_mapFullscreen", 0);
    // O mapa grande do celular nao passa o dedo para mouseX/mouseLeft (medido:
    // ficam parados enquanto se segura ou arrasta). O toque vem da Unity.
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

/**
 * Getter estatico sem argumento, chamado direto pelo ponteiro: os do mapa e do
 * toque rodam todo quadro, e o runtime_invoke encaixotaria o retorno (lixo
 * para o GC a cada quadro).
 */
template <typename R>
R callStatic(const MethodInfo* m) {
    return reinterpret_cast<R (*)(const MethodInfo*)>(il2cpp::methodPointer(m))(m);
}

/** O jogador deste aparelho, ou null. */
Il2CppObject* localPlayer() {
    const int i = g_localPlayer.load(std::memory_order_relaxed);
    auto* players = readStatic<Il2CppArray*>(W.player);
    if (i < 0 || !players || static_cast<uintptr_t>(i) >= players->length) return nullptr;
    return static_cast<Il2CppObject**>(arrayData(players))[i];
}

// ------------------------------ jogador ------------------------------

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
        // O Update multiplica maxRunSpeed e runAcceleration por moveSpeed
        // depois daqui — e o mesmo caminho das Botas de Hermes e da pocao.
        field<float>(p, P.moveSpeed) *= kRunSpeed[n];
    }
    if ((n = levelOf(Power::Jump)) > 0) {
        field<float>(p, P.jumpSpeedBoost) += kJumpBoost[n];
        // Pulo triplo cai de uma altura que mata. Sem isto o poder e armadilha.
        field<uint8_t>(p, P.noFallDmg) = 1;
    }
    if (levelOf(Power::God) > 0) {
        // O modo deus da Jornada: Hurt e KillMe olham este campo e desistem.
        // Nao cobre veneno (sai direto da vida), dai encher a vida tambem.
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
        // O pulo da Nuvem na Garrafa, recarregado todo quadro: cada toque no
        // ar e mais um pulo.
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
        // O voo zera a queda a cada quadro; isto cobre o quadro em que ele
        // desliga no ar.
        field<uint8_t>(p, P.noFallDmg) = 1;
    }
    if (levelOf(Power::Minions) > 0) {
        // Os acessorios somam depois daqui (UpdateEquips), por cima do teto.
        field<int32_t>(p, P.maxMinions) = kUnlimitedMinions;
        field<int32_t>(p, P.maxTurrets) = kUnlimitedMinions;
    }
}

/**
 * Desligar o voo no alto: o jogo mede a queda a partir de onde ela comeca, e
 * ela comeca ali — cair do teto do mundo mataria. Ate pousar, sem dano de
 * queda. Thread do jogo: ligada no tickPowers, encerrada aqui.
 */
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
        // Caiu e parou (pousou), ou nunca caiu: desligou o voo no chao.
        g_fallGrace = false;
    }
}

void hkResetEffects(Il2CppObject* self, const MethodInfo* m) {
    g_origReset(self, m);
    if (!g_anyPlayerPower.load(std::memory_order_relaxed)) return;
    // ResetEffects roda para todo jogador ativo; o poder e so do deste aparelho.
    if (field<int32_t>(self, P.whoAmI) != g_localPlayer.load(std::memory_order_relaxed)) return;
    applyPowers(self);
    if (g_fallGrace) holdFallGrace(self);
}

// ------------------------------ voo ------------------------------

using PlayerUpdateFn = void (*)(Il2CppObject*, int32_t, const MethodInfo*);
PlayerUpdateFn g_origPlayerUpdate = nullptr;

/**
 * Voo que atravessa parede: o Update roda inteiro (controles, animacao, uso de
 * item, gravidade, colisao) e depois a posicao volta para onde estava, mais o
 * que o direcional pediu. Tudo o que o Update moveu — gravidade, colisao,
 * empurrao de vento, agua — some junto.
 *
 * Os controles sao os do proprio quadro: o Update os copia do
 * PlayerInput.Triggers no comeco, e no celular e o joystick que os liga.
 */
void hkPlayerUpdate(Il2CppObject* self, int32_t i, const MethodInfo* m) {
    const int n = levelOf(Power::Fly);
    if (n == 0 || i != g_localPlayer.load(std::memory_order_relaxed) || !inWorld()) {
        g_origPlayerUpdate(self, i, m);
        return;
    }
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
    // Voando, a queda nunca comeca. Desligar no alto e com a carencia
    // (holdFallGrace).
    const int32_t tileY = static_cast<int32_t>(next.y / 16.f);
    field<int32_t>(self, F.fallStart) = tileY;
    field<int32_t>(self, F.fallStart2) = tileY;
}

// ------------------------------ raio-x ------------------------------

using PresentFn = void (*)(Il2CppObject*, const MethodInfo*);
PresentFn g_origPresent[std::size(g_engines)] = {};

/**
 * Branco no mapa de luz inteiro. No celular o desenho de bloco, parede,
 * liquido, fundo e o preto do escuro (Main.DrawBlack) le o mapa direto
 * (Lighting.GetLightMap), sem passar pelo Lighting.GetColor — hookar so o
 * GetColor clarearia NPC e item e deixaria o mundo escuro.
 *
 * Roda depois do Present, que acabou de trocar o mapa ativo. O de trabalho,
 * o proximo a ser calculado, e reescrito inteiro pelo scan, entao desligar nao
 * deixa luz sobrando.
 */
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

// ------------------------------ tempo ------------------------------

using UpdateFn = void (*)(Il2CppObject*, int32_t, const MethodInfo*);
UpdateFn g_origNpcUpdate = nullptr;
UpdateFn g_origProjUpdate = nullptr;

/**
 * NPC parado nao anda, nao anima e nao ataca: o UpdateNPC inteiro fica de fora.
 *
 * Menos a imunidade. E no UpdateNPC que ela desconta, e sem descontar o
 * primeiro golpe deixava o NPC imune ao jogador ate o tempo voltar — bater
 * num inimigo parado nao fazia nada depois da primeira vez.
 */
void hkUpdateNPC(Il2CppObject* self, int32_t i, const MethodInfo* m) {
    if (levelOf(Power::TimeStop) == 0) { g_origNpcUpdate(self, i, m); return; }
    const int localIndex = g_localPlayer.load(std::memory_order_relaxed);
    auto* immune = field<Il2CppArray*>(self, g_npcImmune);
    if (immune && localIndex >= 0 && static_cast<uintptr_t>(localIndex) < immune->length) {
        int32_t& t = static_cast<int32_t*>(arrayData(immune))[localIndex];
        if (t > 0) --t;
    }
}

/** So o projetil inimigo para; o do jogador segue e acerta quem esta parado. */
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
    // No menu a hora parada nao vale: o proximo mundo tem o relogio dele.
    if (!inWorld()) { g_haveFrozenTime = false; return; }
    if (!g_haveFrozenTime) {
        g_frozenTime = readStatic<double>(M.time);
        g_haveFrozenTime = true;
    } else {
        writeStatic(M.time, g_frozenTime);
    }
}

// ------------------------------ clima ------------------------------

// O que foi aplicado no quadro anterior: desligar precisa de um quadro a mais
// para desfazer a chuva e esquecer a direcao do vento.
int g_rainApplied = 0;
int g_windApplied = 0;
float g_windSign = 1.f;

/**
 * Chuva e vento sao estaticos do Main, escritos a cada quadro antes do
 * DoUpdate: o UpdateTime nao consegue parar a chuva nem o sorteio do vento
 * mudar a direcao enquanto o poder estiver ligado.
 *
 * Ligar a chuva e o StartRain(instant) que o slider da Jornada usa, sem o
 * sorteio de chuva de moedas; a nuvem ja entra no tom certo. Desligar e o
 * StopRain sem `instant`: o DoDraw clareia o ceu aos poucos.
 */
void holdWeather() {
    const bool world = inWorld();
    const int rain = world ? levelOf(Power::Rain) : 0;
    const int wind = world ? levelOf(Power::Wind) : 0;

    if (rain > 0) {
        writeStatic<bool>(M.raining, true);
        if (readStatic<int32_t>(M.rainTime) < kRainTimeFloor) writeStatic(M.rainTime, kRainTimeFloor);
        writeStatic(M.maxRaining, kRainStrength[rain]);
        writeStatic(M.cloudAlpha, kRainStrength[rain]);
    } else if (g_rainApplied > 0 && world) {
        writeStatic<int32_t>(M.rainTime, 0);
        writeStatic<bool>(M.raining, false);
        writeStatic(M.maxRaining, 0.f);
    }
    g_rainApplied = rain;

    // A direcao e a do vento de quando o poder ligou; o poder so muda a forca.
    float target = NAN;
    if (wind > 0) {
        if (g_windApplied == 0) g_windSign = readStatic<float>(M.windTarget) < 0.f ? -1.f : 1.f;
        target = g_windSign * kWindStrength[wind];
    } else if (rain == kStorm) {
        const float now = readStatic<float>(M.windTarget);
        if (std::fabs(now) < kStormWind) target = (now < 0.f ? -1.f : 1.f) * kStormWind;
    }
    g_windApplied = wind;
    if (!std::isnan(target)) {
        // Os dois, como o slider da Jornada: so o alvo, o atual chegaria nele
        // em segundos.
        writeStatic(M.windTarget, target);
        writeStatic(M.windCurrent, target);
    }
}

// ------------------------------ bestiario ------------------------------

/**
 * Uma entrada inteira: mortes suficientes para os drops com chance (o numero
 * que o proprio jogo pede, o do estandarte), visto e conversado — criatura,
 * inimigo e morador olham trackers diferentes, e aqui vao os tres.
 *
 * Nunca diminui a contagem de quem ja matou mais.
 */
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

/**
 * Percorre os NPCs pelo dicionario que o jogo usa para creditar o bestiario
 * (ContentSamples.NpcBestiaryCreditIdsByNpcNetIds: variante credita a entrada
 * da original). Vale para o mundo aberto e sai no save dele, como uma morte
 * normal.
 */
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

// ------------------------------ instalacao ------------------------------

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

/** Os dois Present. Basta um: o jogo so usa o do modo de luz escolhido. */
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


// ------------------------------ hora e mundo ------------------------------

std::atomic<int> g_timeRequest{-1};
std::atomic<int> g_stateHardmode{-1};
std::atomic<int> g_stateGameMode{-1};

// O estado do mundo sai todo quadro, com poder ligado ou nao: o menu mostra
// hardmode e dificuldade assim que abre. Refs proprias, e so duas.
State g_stateRefs = State::NotTried;
FieldInfo* g_stateHardMode = nullptr;
const MethodInfo* g_stateGetGameMode = nullptr;

void updateWorldState() {
    if (!inWorld()) {
        g_stateHardmode.store(-1, std::memory_order_relaxed);
        g_stateGameMode.store(-1, std::memory_order_relaxed);
        return;
    }
    if (g_stateRefs == State::NotTried) {
        Il2CppClass* main = il2cpp::findClass({"Terraria", "Main", {}});
        g_stateHardMode = main ? il2cpp::findField(main, "hardMode") : nullptr;
        g_stateGetGameMode =
            main ? il2cpp::api().class_get_method_from_name(main, "get_GameMode", 0) : nullptr;
        g_stateRefs = g_stateHardMode && g_stateGetGameMode ? State::Ok : State::Failed;
        if (g_stateRefs == State::Failed) BL_ERROR("poderes: estado do mundo sem refs");
    }
    if (g_stateRefs != State::Ok) return;
    g_stateHardmode.store(readStatic<bool>(g_stateHardMode) ? 1 : 0, std::memory_order_relaxed);
    g_stateGameMode.store(callStatic<int32_t>(g_stateGetGameMode), std::memory_order_relaxed);
}

/** O botao de hora: o mesmo SkipToTime dos botoes da Jornada. */
/**
 * Mundo mudou no servidor: manda o estado aos clientes. A 7 (WorldData) leva
 * hora, dia/noite, chuva, vento, hardmode e modo de jogo; a 18 e a da hora,
 * com a altura do sol e da lua. Sem isto o cliente so ve na proxima rodada
 * de sincronizacao, e o relogio dele segue andando por conta propria.
 */
void broadcastWorld(bool time) {
    if (!isNetHost()) return;
    sendData(kMsgWorldData, 0);
    if (time) sendData(kMsgTimeSync, 0);
}

void applyTimeOfDay(int which) {
    TimeOfDay t = kTimesOfDay[which];
    Il2CppObject* exc = nullptr;
    void* args[] = {&t.time, &t.day};
    il2cpp::api().runtime_invoke(W.skipToTime, nullptr, args, &exc);
    if (exc) { BL_ERROR("poderes: SkipToTime lancou excecao"); return; }
    // Com o tempo parado, e a hora nova que fica parada.
    g_haveFrozenTime = false;
    broadcastWorld(true);
    BL_INFO("poderes: hora -> %d (%s)", t.time, t.day ? "dia" : "noite");
}

/**
 * 1 liga do jeito do jogo — WorldGen.StartHardmode, o mesmo de matar a Parede
 * de Carne: mensagem, e o sagrado e a corrupcao abrindo o V no mapa. 2 so
 * desliga a flag; o que o hardmode espalhou fica.
 */
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

/**
 * Main.GameMode e o do arquivo do mundo (ActiveWorldFileData.GameMode): vale
 * na hora — expertMode e masterMode sao lidos dele — e sai no save. Os quatro
 * da criacao de mundo: Classico, Expert, Mestre e Jornada.
 */
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

// ------------------------------ spawn ------------------------------

using StaticVoidFn = void (*)(const MethodInfo*);
StaticVoidFn g_origSpawnNpc = nullptr;

/** NPC.SpawnNPC e o spawn natural inteiro; chefe invocado e evento nao passam por ele. */
void hkSpawnNPC(const MethodInfo* m) {
    if (levelOf(Power::NoSpawns) > 0) return;
    g_origSpawnNpc(m);
}

/**
 * Nivel 2: some com o que ja existe — tudo que machuca e nao e morador nem
 * amigo, chefe e invocado inclusive. Sem morte: nada de drop nem estandarte.
 */
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
    }
}

// ------------------------------ mochila ------------------------------

/** Esvazia os 50 espacos de cima; favorito fica, como no "depositar tudo". */
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
        bool fullReset = false;
        void* args[] = {&fullReset};
        Il2CppObject* exc = nullptr;
        il2cpp::api().runtime_invoke(W.turnToAir, it, args, &exc);
        if (!exc) ++cleared;
    }
    BL_INFO("poderes: mochila limpa (%d espacos)", cleared);
}

// ------------------------------ mapa ------------------------------

int g_revealX = -1;      // proxima coluna a revelar; -1 = parado
int g_revealFrames = 0;
std::chrono::steady_clock::time_point g_revealStart;

void startReveal() {
    if (g_revealX >= 0) return;
    g_revealX = 0;
    g_revealFrames = 0;
    g_revealStart = std::chrono::steady_clock::now();
}

/** Um pedaco por quadro (kRevealBudget); no fim, o mapa redesenha. */
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

// ------------------------------ teleporte no mapa ------------------------------

struct MapHold {
    bool down = false, fired = false;
    int32_t x = 0, y = 0;
    std::chrono::steady_clock::time_point since;
} g_hold;

/** UnityEngine.Touch (0x44 bytes: volta pela memoria de retorno, x8, como no C#). */
struct UnityTouch {
    int32_t fingerId;
    Vec2 position;   // pixels do aparelho, a partir do canto de BAIXO
    uint8_t rest[0x44 - 12];
};
static_assert(sizeof(UnityTouch) == 0x44, "layout do UnityEngine.Touch");

/**
 * O dedo, em pixels do aparelho a partir do canto de cima. false sem
 * exatamente um dedo na tela — dois e pinca de zoom.
 */
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

/**
 * Pixels do aparelho por pixel da interface do celular: o mapa grande
 * (GUIMap.DrawMap) desenha e le o dedo nas coordenadas da interface, e
 * mapFullscreenScale e em pixels DELA. O fator vem da configuracao de escala
 * da interface; em vez de reproduzir a conta, e medido no proprio toque: o
 * mouse da interface (XNAUnityRunner._uiMouseState) e o dedo da Unity sao o
 * mesmo ponto, e a relacao e linear, sem deslocamento (MuMu: 1250,470 no
 * aparelho = 796,299 na interface, 1,571 nos dois eixos).
 */
float uiToDevice(int32_t devX, int32_t devY) {
    Il2CppObject* ui = callStatic<Il2CppObject*>(W.getUiMouse);
    if (!ui) return 0.f;
    const int32_t sum = field<int32_t>(ui, W.uiMouseX) + field<int32_t>(ui, W.uiMouseY);
    return sum > 0 ? static_cast<float>(devX + devY) / static_cast<float>(sum) : 0.f;
}

/**
 * O tile sob o dedo no mapa grande: mapFullscreenPos (em tiles) fica no centro
 * da tela, e cada tile ocupa mapFullscreenScale pixels de interface.
 */
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
    // Os pes no tile tocado.
    Vec2 target{clampedX * 16.f - w * 0.5f, (clampedY + 1.f) * 16.f - h};

    // Dentro de bloco o jogador fica preso: procura ar, primeiro subindo.
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

/** Mapa grande aberto e o dedo parado no mesmo lugar por kMapHoldTime: vai. */
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
    g_hold.fired = true;   // um teleporte por toque: segurar mais nao repete
    teleportToMapPoint(g_hold.x, g_hold.y);
}

/** O que precisa do mundo aberto: comandos do menu e os poderes de mundo. */
void tickWorldPowers() {
    const int time = g_timeRequest.exchange(-1, std::memory_order_relaxed);
    const bool clearInv = g_level[static_cast<int>(Power::ClearInventory)].exchange(0) > 0;
    const bool reveal = g_level[static_cast<int>(Power::RevealMap)].exchange(0) > 0;
    const int hardmode = g_level[static_cast<int>(Power::Hardmode)].exchange(0);
    const int difficulty = g_level[static_cast<int>(Power::Difficulty)].exchange(0);
    if (!inWorld()) {
        g_revealX = -1;
        g_hold.down = false;
        return;
    }
    if (time >= 0 && g_timeRefs) applyTimeOfDay(time);
    if (clearInv && g_inventoryRefs) clearInventory();
    if (reveal && g_mapRefs) startReveal();
    if (g_revealX >= 0) revealStep();
    if (hardmode > 0 && g_hardmodeRefs) applyHardmode(hardmode);
    if (difficulty > 0 && g_hardmodeRefs) applyDifficulty(difficulty);

    const int spawns = levelOf(Power::NoSpawns);
    if (spawns > 0 && g_spawnRefs && g_spawnHook == State::NotTried) {
        g_spawnHook = installHook("NPC.SpawnNPC", W.spawnNpc, &hkSpawnNPC, &g_origSpawnNpc);
    }
    if (spawns == 2 && g_spawnRefs) clearHostiles();
    if (levelOf(Power::MapTeleport) > 0 && g_teleportRefs) watchMapHold();
    else g_hold.down = false;
}

// O nivel de cada poder de mundo que o servidor ja conhece (-1 = nunca mandado).
int g_sentLevel[kPowerCount];
bool g_sentInit = false;
int g_heldLevel[kPowerCount];   // no servidor: o nivel do quadro anterior
int g_resyncFrame = 0;

/**
 * Cliente: o que mexe no mundo vira pedido ao servidor. Estado (tempo parado,
 * chuva, vento, sem inimigos) vai quando muda; comando (hora, hardmode,
 * dificuldade) vai e e consumido aqui, porque aplicado no cliente nao faz nada.
 */
void forwardWorldPowers() {
    if (!g_sentInit) {
        for (int& v : g_sentLevel) v = -1;
        g_sentInit = true;
    }
    if (!inWorld()) {
        // Ao entrar de novo num servidor, o que estiver ligado vai de novo.
        for (int& v : g_sentLevel) v = -1;
        return;
    }
    const int time = g_timeRequest.exchange(-1, std::memory_order_relaxed);
    if (time >= 0) sendToServer("time " + std::to_string(time));
    for (Power p : {Power::Hardmode, Power::Difficulty}) {
        const int v = g_level[static_cast<int>(p)].exchange(0);
        if (v > 0) sendToServer("power " + std::to_string(static_cast<int>(p)) + " " + std::to_string(v));
    }
    for (Power p : {Power::TimeStop, Power::Rain, Power::Wind, Power::NoSpawns}) {
        const int i = static_cast<int>(p);
        const int v = levelOf(p);
        if (v == g_sentLevel[i]) continue;
        // Desligado e nunca mandado: nada a pedir.
        if (!(v == 0 && g_sentLevel[i] < 0)) {
            sendToServer("power " + std::to_string(i) + " " + std::to_string(v));
        }
        g_sentLevel[i] = v;
    }
}

/**
 * Servidor: poder de mundo mudou, ou segue segurando algo, manda o mundo aos
 * clientes. Vale para o que veio do menu dele e do pedido de um cliente.
 */
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

} // namespace

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

void setTimeOfDay(int which) {
    if (which < 0 || which >= static_cast<int>(std::size(kTimesOfDay))) return;
    g_timeRequest.store(which, std::memory_order_relaxed);
}

int worldHardmode() { return g_stateHardmode.load(std::memory_order_relaxed); }
int worldGameMode() { return g_stateGameMode.load(std::memory_order_relaxed); }

void tickPowers() {
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
        g_fallGrace = inWorld() && g_flyRefs;   // le a velocidade pelas refs do voo
        g_graceFell = false;
        g_graceStill = 0;
    }
    g_wasFlying = flying;
    if (!inWorld()) g_fallGrace = false;
    if (g_fallGrace) resetHook = true;
    g_anyPlayerPower.store(resetHook, std::memory_order_relaxed);
    const bool pending = g_timeRequest.load(std::memory_order_relaxed) >= 0 || g_revealX >= 0;
    if (!any && !pending && !g_fallGrace && g_rainApplied == 0 && g_windApplied == 0) {
        g_haveFrozenTime = false;
        return;
    }

    if (g_refs == State::NotTried) g_refs = resolveRefs() ? State::Ok : State::Failed;
    if (g_refs != State::Ok) return;

    Il2CppObject* exc = nullptr;
    const int localIndex = unboxInt(il2cpp::api().runtime_invoke(g_getMyPlayer, nullptr, nullptr, &exc));
    g_localPlayer.store(exc ? -1 : localIndex, std::memory_order_relaxed);

    // Instalados aqui, na thread do jogo e entre quadros: nenhum deles esta
    // rodando agora (o Present roda dentro do DoUpdate, que ainda nao comecou),
    // entao reescrever o comeco deles e seguro.
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
    if (levelOf(Power::Fly) > 0 && g_flyRefs && g_flyHook == State::NotTried) {
        g_flyHook = installHook("Player.Update", g_playerUpdate, &hkPlayerUpdate,
                                &g_origPlayerUpdate);
    }
    if (levelOf(Power::XRay) > 0 && g_lightRefs && g_lightHooks == State::NotTried) {
        g_lightHooks = installLightHooks();
    }
    holdClock();
    if (g_weatherRefs) holdWeather();

    // Reviver rapido: o UpdateDead conta o respawnTimer ate 0 e so entao chama
    // o Spawn. Zerado aqui, antes do quadro, o jogador volta neste quadro.
    if (levelOf(Power::FastRespawn) > 0 && inWorld()) {
        Il2CppObject* p = localPlayer();
        if (p && field<uint8_t>(p, P.dead) && field<int32_t>(p, P.respawnTimer) > 0) {
            field<int32_t>(p, P.respawnTimer) = 0;
        }
    }

    // Acao, nao estado: roda uma vez e o nivel volta a 0 sozinho.
    if (g_level[static_cast<int>(Power::Bestiary)].exchange(0) > 0 && inWorld() &&
        g_bestiaryRefs) {
        unlockBestiary();
    }
    tickWorldPowers();
}

} // namespace bl::runtime
