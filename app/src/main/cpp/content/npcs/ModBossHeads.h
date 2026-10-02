#pragma once

#include <string>

namespace bl::runtime {

/**
 * O icone de chefe de um NPC de mod (o `_Head_Boss` do tModLoader). A barra de
 * chefe (CommonBossBigProgressBar) e os icones do mapa (GUIMap.DrawMap,
 * Main.DrawNPCMapIcons) leem NPC.GetBossHeadTextureIndex, que devolve
 * NPCID.Sets.BossHeadTextures[type]: com -1 o chefe fica sem barra e sem
 * cabeca no mapa. O PNG entra no fim de TextureAssets.NpcHeadBoss e o indice
 * vai para a tabela do tipo.
 */

/** O icone de chefe do NPC `type` (PNG). Antes da instalacao. */
void setModBossHead(int type, const std::string& texturePath, const std::string& assetName);

/** Carrega os icones e liga cada tipo ao seu. Thread do jogo, depois de as tabelas de NPC crescerem. */
void installBossHeads();

/** Tabela que o jogo refez (NpcHeadBoss, BossHeadTextures): reaplica. Thread do jogo, de vez em quando. */
void watchBossHeads();

} // namespace bl::runtime
