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

/** A cabeca do NPC `type` (PNG). Antes da instalacao; sem arquivo, nada. */
void setModNpcHead(int type, const std::string& texturePath, const std::string& assetName);

/** O indice da cabeca do NPC de mod `type` em TextureAssets.NpcHead, ou -1. */
int modNpcHeadSlot(int type);

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
