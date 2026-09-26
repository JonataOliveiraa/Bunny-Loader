#pragma once
#include <string>
#include <utility>
#include <vector>

#include "il2cpp/Types.h"

namespace bl::runtime {

/**
 * Projeteis de mod: tipos NOVOS, com id depois dos do jogo — o mesmo modelo
 * dos itens de mod (ModItems.h), com as diferencas do projetil:
 *   - Projectile.SetDefaults NAO zera o tipo desconhecido: grava o tipo, zera
 *     o resto e cai no `else` do switch, que so faz `active = false`. Entao o
 *     hook JS chama o original, depois o setDefaults do mod, e reativa;
 *   - as tabelas por tipo nascem com ProjectileID.Count (1111) posicoes,
 *     inclusive uma em CADA jogador: Player.ownedProjectileCounts, que o jogo
 *     ESCREVE todo quadro pelo tipo de cada projetil vivo;
 *   - a IA vem do `aiStyle` (o do jogo). IA propria do mod ainda nao.
 */

/** ProjectileID.Count do jogo. E `const` no C#, nao ha campo para ler: vem do dump. */
constexpr int kVanillaProjectileCount = 1111;

struct ModProjectileDef {
    std::string mod;       // uid do mod que registrou
    std::string name;      // chave estavel dentro do mod
    std::string texture;   // caminho absoluto do PNG (quadros empilhados na vertical)
    int frames = 1;        // Main.projFrames
    std::vector<std::pair<std::string, std::string>> names;   // "pt-BR" -> nome
};

/** Reserva o proximo id. THREAD-SAFE. -1 se o mesmo mod ja registrou esse nome. */
int registerModProjectile(ModProjectileDef def);

bool isModProjectile(int type);

/** Vanilla + projeteis de mod JA instalados nas tabelas do jogo. */
int projectileTypeCount();

/** Instala o registrado quando o jogo ja criou as tabelas; vigia as refeitas. Thread do jogo. */
void tickModProjectiles();

/** Tudo que foi registrado ja esta nas tabelas do jogo (ou falhou de vez). */
bool modProjectilesSettled();

/** Depois do setDefaults do mod: reativa, e tamanho pela textura se ele nao disse. */
void finishModProjectile(Il2CppObject* projectile, int type);

/**
 * Chamado uma vez por lote instalado (thread do jogo), com os tipos novos ja
 * nas tabelas do jogo: a hora do SetStaticDefaults.
 */
using ProjectilesInstalledHook = void (*)(int first, int last);
void setProjectilesInstalledHook(ProjectilesInstalledHook hook);

/**
 * Quadros definidos depois do registro — o SetStaticDefaults do mod escreve
 * Main.projFrames[tipo], como no tModLoader. Vale para a altura de um quadro
 * e para a tabela refeita. Thread do jogo.
 */
void setModProjectileFrames(int type, int frames);

/** O tipo do projetil `name` do mod `mod`, ou -1. */
int modProjectileTypeByName(const std::string& mod, const std::string& name);

} // namespace bl::runtime
