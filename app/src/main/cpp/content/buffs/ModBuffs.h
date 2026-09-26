#pragma once
#include <string>
#include <utility>
#include <vector>

#include "il2cpp/Types.h"

namespace bl::runtime {

/**
 * Buffs de mod: tipos NOVOS, com id depois dos do jogo — o mesmo modelo dos
 * projeteis (ModProjectiles.h), com o que e so do buff:
 *   - as tabelas por tipo (Main.debuff, BuffID.Sets..., Lang, TextureAssets)
 *     nascem com BuffID.Count (389) posicoes, e o `buffImmune` tambem, em
 *     CADA Player e NPC (o construtor cria; Update/SetDefaults so zeram 389);
 *   - o limite esta COMPILADO em alguns metodos. O pior e o GUIBuffs.Draw: ao
 *     montar a barra ele ZERA todo buff do jogador com tipo >= 389. Tambem a
 *     enfermeira (nao cura) e a remocao de buff de NPC pela rede;
 *   - o save do personagem descarta tipo >= 389 ao carregar: o buff de mod vai
 *     pelo nome no `.plr.bl`, junto dos itens (ModItemSave).
 */

/** BuffID.Count do jogo. E `const` no C#: vem do dump. */
constexpr int kVanillaBuffCount = 389;

struct ModBuffDef {
    std::string mod;       // uid do mod que registrou
    std::string name;      // chave estavel dentro do mod
    std::string texture;   // caminho absoluto do PNG (32x32 no jogo)
    std::vector<std::pair<std::string, std::string>> names;          // "pt-BR" -> nome
    std::vector<std::pair<std::string, std::string>> descriptions;   // "pt-BR" -> descricao
};

/** Reserva o proximo id. THREAD-SAFE. -1 se o mesmo mod ja registrou esse nome. */
int registerModBuff(ModBuffDef def);

bool isModBuff(int type);

/** Vanilla + buffs de mod JA instalados nas tabelas do jogo. */
int buffTypeCount();

/** Instala o registrado quando o jogo ja criou as tabelas; vigia as refeitas. Thread do jogo. */
void tickModBuffs();

/** Tudo que foi registrado ja esta nas tabelas do jogo (ou falhou de vez). */
bool modBuffsSettled();

/** Chamado uma vez por lote instalado (thread do jogo): a hora do SetStaticDefaults. */
using BuffsInstalledHook = void (*)(int first, int last);
void setBuffsInstalledHook(BuffsInstalledHook hook);

/** O tipo do buff `name` do mod `mod`, ou -1. */
int modBuffTypeByName(const std::string& mod, const std::string& name);

/** "<uid>/<nome>" do buff de mod `type`, ou "". */
std::string modBuffKey(int type);

struct ModBuffInfo {
    int type;
    std::string mod, name, texture;
};

/** Os registrados, na ordem do id. Copia: serve a qualquer thread (menu). */
std::vector<ModBuffInfo> modBuffs();

/** O tipo pela chave "<uid>/<nome>", ou -1 (mod nao carregado). */
int modBuffTypeByKey(const std::string& key);

/** Um buff de mod ativo no jogador, para o `.plr.bl`. */
struct SavedBuff {
    int slot = 0;
    int time = 0;
    std::string key;   // "<uid>/<nome>"
};

/** Os buffs de mod ativos de `player` que valem salvar (sem Main.buffNoSave). */
std::vector<SavedBuff> collectModBuffs(Il2CppObject* player);

/**
 * Repoe os buffs salvos em `player` recem-carregado, no primeiro slot livre
 * (o jogo compacta a lista ao carregar). Devolve os que
 * nao deu para repor (mod nao carregado, sem slot), para voltar ao arquivo.
 */
std::vector<SavedBuff> restoreModBuffs(Il2CppObject* player, const std::vector<SavedBuff>& saved);

} // namespace bl::runtime
