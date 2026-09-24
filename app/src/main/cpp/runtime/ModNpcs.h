#pragma once
#include <string>
#include <utility>
#include <vector>

#include "il2cpp/Types.h"

namespace bl::runtime {

/**
 * NPCs de mod: tipos NOVOS, com id depois dos do jogo — o modelo de itens e
 * projeteis (ModItems.h, ModProjectiles.h), com o que o NPC tem de proprio:
 *   - o jogo compara com NPCID.Count DIRETO no codigo em lugares que importam:
 *     Main.DrawNPCs pulava o NPC (invisivel), NPC.NPCLoot saia sem drop,
 *     Lang.GetNPCName devolvia nome vazio. O limite e trocado na instrucao
 *     (CodePatch.h);
 *   - o SetDefaults do jogo calcula vida, dano de base e a escala de
 *     dificuldade no FINAL, antes do mod preencher lifeMax: refeito depois;
 *   - a animacao e um switch pelo tipo em NPC.FindFrame: `animationType` faz o
 *     NPC animar como um do jogo (o AnimationType do tModLoader), trocando o
 *     tipo so durante a chamada;
 *   - Bestiario: matar um NPC le ContentSamples.NpcBestiaryCreditIdsByNpcNetIds
 *     pelo tipo, e sem entrada lanca excecao; a amostra e registrada;
 *   - Player.npcTypeNoAggro e bool[NPCID.Count] POR JOGADOR.
 * A IA vem do `aiStyle` (a do jogo). Spawn natural ainda nao.
 */

/** NPCID.Count do jogo. E `const` no C#, nao ha campo para ler: vem do dump. */
constexpr int kVanillaNpcCount = 697;

struct ModNpcDef {
    std::string mod;        // uid do mod que registrou
    std::string name;       // chave estavel dentro do mod
    std::string texture;    // caminho absoluto do PNG (quadros empilhados na vertical)
    int frames = 1;         // Main.npcFrameCount
    int animationType = 0;  // anima como este NPC do jogo (0 = nao anima)
    std::vector<std::pair<std::string, std::string>> names;   // "pt-BR" -> nome
};

/** Reserva o proximo id. THREAD-SAFE. -1 se o mesmo mod ja registrou esse nome. */
int registerModNpc(ModNpcDef def);

bool isModNpc(int type);

/** Vanilla + NPCs de mod JA instalados nas tabelas do jogo. */
int npcTypeCount();

/**
 * Troca os limites compilados no codigo (CodePatch.h) para o total registrado.
 * Chamar logo depois de carregar os mods, ANTES de o jogo rodar esses metodos:
 * no emulador (que traduz o ARM) uma troca feita depois de o metodo ja ter
 * rodado pode nao valer — o DrawNPCs, que roda desde o menu, continuava
 * pulando o NPC de mod. Sem NPC de mod no mundo ainda, trocar cedo e seguro.
 */
void prepareModNpcs();

/**
 * Chamado uma vez na thread do jogo, com os tipos [first, last] instalados E a
 * tabela de drop do jogo (Main.ItemDropsDB) pronta: a hora do
 * SetStaticDefaults.
 */
using NpcsInstalledHook = void (*)(int first, int last);
void setNpcsInstalledHook(NpcsInstalledHook hook);

/** Instala o registrado quando o jogo ja criou as tabelas; vigia as refeitas. Thread do jogo. */
void tickModNpcs();

struct ModNpcInfo {
    int type;
    std::string mod, name, texture;
    int frames;
};

/** Os registrados, na ordem do id. Copia: serve a qualquer thread. */
std::vector<ModNpcInfo> modNpcs();

/**
 * Depois do setDefaults do mod: o final do SetDefaults do jogo de novo (vida,
 * dano e defesa de base, netID, escala de dificuldade), ativo, e tamanho pela
 * textura se o mod nao disse.
 */
void finishModNpc(Il2CppObject* npc, int type);

} // namespace bl::runtime
