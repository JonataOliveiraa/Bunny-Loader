#pragma once
#include "il2cpp/Api.h"

namespace bl::runtime {

// Tudo que o núcleo usa do jogo é resolvido UMA vez aqui. Nenhum código de
// runtime busca classe/método/campo por nome.
//
// Offsets de referência conferidos no dump de Terraria 1.4.5.6.4
// (versionCode 301543, metadata v31, arm64-v8a). Servem de sanity-check na
// Fase 3 — NÃO são hardcoded: tudo é resolvido pelo Resolver em runtime.

// Campos declarados em Terraria.Entity (base de Projectile/NPC/Item/Player).
// ATENÇÃO: `active` NÃO mora aqui — é declarado em cada subclasse.
struct EntityRefs {
    int32_t whoAmI   = -1; // ref 0x10
    int32_t position = -1; // ref 0x14  Vector2
    int32_t velocity = -1; // ref 0x1C  Vector2
    int32_t width    = -1; // ref 0x3C
    int32_t height   = -1; // ref 0x40
};

struct ProjectileRefs {
    Il2CppClass* cls = nullptr;
    int32_t active   = -1; // ref 0x49  (declarado em Projectile, não em Entity)
    int32_t rotation = -1; // ref 0x60
    int32_t type     = -1; // ref 0x64
    int32_t owner    = -1; // ref 0x70
    int32_t timeLeft = -1; // ref 0x98
    int32_t damage   = -1; // ref 0xA0
    const MethodInfo* setDefaults = nullptr; // void SetDefaults(int Type)
    const MethodInfo* aiMethod    = nullptr; // void AI()
    const MethodInfo* kill        = nullptr; // void Kill()
    const MethodInfo* update      = nullptr; // void Update(int i)
};

struct GameRefs {
    EntityRefs entity;
    ProjectileRefs proj;
    // TODO(Fase 5): NPCRefs (NPC.active @ 0x49), ItemRefs, MainRefs.
};

GameRefs& game();

// Resolve tudo e valida. Se algo faltar, loga e retorna false — o loader recusa
// iniciar em vez de crashar no meio da partida.
bool resolveGameRefs();

template <typename T>
inline T& field(Il2CppObject* obj, int32_t offset) {
    return *reinterpret_cast<T*>(reinterpret_cast<char*>(obj) + offset);
}

} // namespace bl::runtime
