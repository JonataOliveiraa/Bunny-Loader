#include "runtime/Powers.h"
#include "runtime/Cheats.h"
#include "core/Log.h"
#include "hook/HookManager.h"
#include "il2cpp/Api.h"
#include "il2cpp/Resolver.h"
#include "runtime/GameRefs.h"
#include <atomic>

namespace bl::runtime {

namespace {

constexpr int kPowerCount = static_cast<int>(Power::Count);

// Quantos niveis cada poder tem, na ordem do enum. Liga/desliga = 1.
constexpr int kMaxLevel[kPowerCount] = {3, 2, 2, 1, 1, 1, 1, 1, 1};

// Indice 0 = desligado.
constexpr float kDamage[] = {1.f, 2.f, 5.f, 10.f};
constexpr float kRunSpeed[] = {1.f, 2.f, 3.f};
// Somado ao jumpSpeed, que parte de 5,01: +5 e o dobro, +10 o triplo.
constexpr float kJumpBoost[] = {0.f, 5.f, 10.f};
// pickSpeed multiplica o tempo de uso da ferramenta: menor e mais rapido.
constexpr float kToolSpeed = 0.25f;

std::atomic<int> g_level[kPowerCount];

int levelOf(Power p) { return g_level[static_cast<int>(p)].load(std::memory_order_relaxed); }

// ------------------------------ refs ------------------------------

struct PlayerFields {
    int32_t whoAmI, meleeDamage, magicDamage, rangedDamage, minionDamage, moveSpeed,
        jumpSpeedBoost, noFallDmg, creativeGodMode, statLife, statLifeMax2, statMana,
        statManaMax2, manaCost, breath, breathMax, hasJumpOption_Cloud,
        canJumpAgain_Cloud, pickSpeed, nightVision, findTreasure, detectCreature,
        dangerSense;
} P;

int32_t g_npcImmune = -1;       // NPC.immune (int[], um por jogador)
int32_t g_projHostile = -1;     // Projectile.hostile
int32_t g_projFriendly = -1;    // Projectile.friendly

const MethodInfo* g_getMyPlayer = nullptr;
FieldInfo* g_time = nullptr;    // Main.time (double estatico)
const MethodInfo* g_reset = nullptr;
const MethodInfo* g_npcUpdate = nullptr;
const MethodInfo* g_projUpdate = nullptr;

enum class State { NotTried, Ok, Failed };
State g_refs = State::NotTried;
State g_playerHook = State::NotTried;
State g_timeHooks = State::NotTried;

// O jogador deste aparelho. -1 fora do mundo. Escrito no DoUpdate, lido no
// ResetEffects e no UpdateNPC — todos na thread do jogo, o atomico e so para
// deixar isso explicito.
std::atomic<int> g_localPlayer{-1};

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

    struct { int32_t* dst; const char* name; } fields[] = {
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
    };
    bool ok = true;
    for (auto& c : fields) {
        *c.dst = fieldOffset(player, c.name);   // loga o que faltar
        if (*c.dst < 0) ok = false;
    }
    g_npcImmune = fieldOffset(npc, "immune");
    g_projHostile = fieldOffset(proj, "hostile");
    g_projFriendly = fieldOffset(proj, "friendly");

    g_getMyPlayer = a.class_get_method_from_name(main, "get_myPlayer", 0);
    g_time = findField(main, "time");
    g_reset = a.class_get_method_from_name(player, "ResetEffects", 0);
    g_npcUpdate = a.class_get_method_from_name(npc, "UpdateNPC", 1);
    g_projUpdate = a.class_get_method_from_name(proj, "Update", 1);

    if (!ok || g_npcImmune < 0 || g_projHostile < 0 || g_projFriendly < 0 ||
        !g_getMyPlayer || !g_time || !g_reset || !g_npcUpdate ||
        !g_projUpdate) {
        BL_ERROR("poderes: refs faltando; os superpoderes ficam desligados");
        return false;
    }
    return true;
}

int unboxInt(Il2CppObject* o) {
    return o ? *reinterpret_cast<int*>(reinterpret_cast<char*>(o) + sizeof(Il2CppObject)) : 0;
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
}

void hkResetEffects(Il2CppObject* self, const MethodInfo* m) {
    g_origReset(self, m);
    if (!g_anyPlayerPower.load(std::memory_order_relaxed)) return;
    // ResetEffects roda para todo jogador ativo; o poder e so do deste aparelho.
    if (field<int32_t>(self, P.whoAmI) != g_localPlayer.load(std::memory_order_relaxed)) return;
    applyPowers(self);
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
    auto& a = il2cpp::api();
    if (levelOf(Power::TimeStop) == 0) { g_haveFrozenTime = false; return; }
    // No menu a hora parada nao vale: o proximo mundo tem o relogio dele.
    if (!inWorld()) { g_haveFrozenTime = false; return; }
    if (!g_haveFrozenTime) {
        a.field_static_get_value(g_time, &g_frozenTime);
        g_haveFrozenTime = true;
    } else {
        a.field_static_set_value(g_time, &g_frozenTime);
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

} // namespace

void setPower(int id, int level) {
    if (id < 0 || id >= kPowerCount) return;
    if (level < 0) level = 0;
    if (level > kMaxLevel[id]) level = kMaxLevel[id];
    g_level[id].store(level, std::memory_order_relaxed);
}

void tickPowers() {
    bool any = false, playerPower = false;
    for (int i = 0; i < kPowerCount; ++i) {
        if (g_level[i].load(std::memory_order_relaxed) == 0) continue;
        any = true;
        if (i != static_cast<int>(Power::TimeStop)) playerPower = true;
    }
    g_anyPlayerPower.store(playerPower, std::memory_order_relaxed);
    if (!any) { g_haveFrozenTime = false; return; }

    if (g_refs == State::NotTried) g_refs = resolveRefs() ? State::Ok : State::Failed;
    if (g_refs != State::Ok) return;

    Il2CppObject* exc = nullptr;
    const int localIndex = unboxInt(il2cpp::api().runtime_invoke(g_getMyPlayer, nullptr, nullptr, &exc));
    g_localPlayer.store(exc ? -1 : localIndex, std::memory_order_relaxed);

    // Instalados aqui, na thread do jogo e entre quadros: nenhum dos tres esta
    // rodando agora, entao reescrever o comeco deles e seguro.
    if (playerPower && g_playerHook == State::NotTried) {
        g_playerHook = installHook("Player.ResetEffects", g_reset,
                                 reinterpret_cast<void*>(&hkResetEffects),
                                 reinterpret_cast<void**>(&g_origReset));
    }
    if (levelOf(Power::TimeStop) > 0 && g_timeHooks == State::NotTried) {
        g_timeHooks = installHook("NPC.UpdateNPC", g_npcUpdate,
                               reinterpret_cast<void*>(&hkUpdateNPC),
                               reinterpret_cast<void**>(&g_origNpcUpdate));
        if (g_timeHooks == State::Ok) {
            g_timeHooks = installHook("Projectile.Update", g_projUpdate,
                                   reinterpret_cast<void*>(&hkProjUpdate),
                                   reinterpret_cast<void**>(&g_origProjUpdate));
        }
    }
    holdClock();
}

} // namespace bl::runtime
