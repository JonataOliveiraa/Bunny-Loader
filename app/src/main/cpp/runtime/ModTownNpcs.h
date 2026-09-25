#pragma once

#include <string>

namespace bl::runtime {

/**
 * O que um NPC de mod precisa para ser MORADOR (o townNPC do jogo), alem do
 * que ModNpcs.h ja faz para todo NPC:
 *   - a cabeca (NPCHeadID): TextureAssets.NpcHead e as tabelas do tamanho de
 *     NPCHeadID.Count crescem, a cabeca entra no fim da lista do menu de casas
 *     (NPCHeadID.Sets.HeadListOrder), e NPC.TypeToDefaultHeadIndex a devolve.
 *     Sem cabeca o jogo nao muda o morador para casa nenhuma (o tModLoader
 *     tambem exige);
 *   - os lacos compilados ate NPCID.Count que decidem quem se muda
 *     (WorldGen.IsThereASpawnablePrioritizedTownNPC, Main.UpdateTime_SpawnTownNPCs)
 *     e os bool[NPCID.Count] indexados pelo tipo do morador
 *     (TownRoomManager._hasRoom, o nearbyNPCsByType do ShopHelper.ProcessMood).
 */

/**
 * A cabeca do NPC `type` (PNG). `variant` 0: a normal (TypeToDefaultHeadIndex);
 * 1: a de depois do shimmer (o perfil de morador a usa). Antes da instalacao.
 */
void setModNpcHead(int type, int variant, const std::string& texturePath, const std::string& assetName);

/** O indice da cabeca do NPC de mod `type` (variante 0/1) em TextureAssets.NpcHead, ou -1. */
int modNpcHeadSlot(int type, int variant = 0);

/**
 * Os lacos e alocacoes compilados, para `totalTypes` tipos. Cedo, com o
 * prepareModNpcs: no emulador uma troca feita depois de o metodo ja ter
 * rodado pode nao valer (CodePatch.h).
 */
void prepareTownNpcs(int totalTypes);

/** Cabecas e tabelas de instancia, depois de os tipos estarem instalados. Thread do jogo. */
void installTownNpcs(int totalTypes);

/** Tabela de cabeca que o jogo refez: reaplica. Thread do jogo, de vez em quando. */
void watchTownNpcs();

} // namespace bl::runtime
