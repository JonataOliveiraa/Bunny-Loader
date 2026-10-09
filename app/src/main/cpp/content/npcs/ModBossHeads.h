#pragma once

#include <string>

namespace bl::runtime {

/**
 * Os icones de chefe de mod (o `_Head_Boss` do tModLoader e os de
 * Mod.AddBossHeadTexture). A barra de chefe (CommonBossBigProgressBar) e os
 * icones do mapa (GUIMap.DrawMap, Main.DrawNPCMapIcons) leem
 * NPC.GetBossHeadTextureIndex, que devolve NPCID.Sets.BossHeadTextures[type]
 * (ou o que o BossHeadSlot do mod puser): com -1 o chefe fica sem barra e sem
 * cabeca no mapa. Os PNGs entram no fim de TextureAssets.NpcHeadBoss.
 */

/**
 * Reserva a vaga do icone `texturePath` (PNG) em TextureAssets.NpcHeadBoss e
 * a devolve ja no registro, como o tModLoader (o mod guarda o numero para o
 * BossHeadSlot). O mesmo arquivo devolve a mesma vaga. `npcType` >= 0 liga o
 * tipo a ela (o icone padrao do chefe). -1 se a tabela do jogo nao foi achada
 * ou se a instalacao ja passou.
 */
int reserveBossHead(const std::string& texturePath, const std::string& assetName, int npcType);

/** A vaga reservada para o arquivo, ou -1. */
int bossHeadSlotOf(const std::string& texturePath);

/** Carrega os icones e liga cada tipo ao seu. Thread do jogo, depois de as tabelas de NPC crescerem. */
void installBossHeads();

/** Tabela que o jogo refez (NpcHeadBoss, BossHeadTextures): reaplica. Thread do jogo, de vez em quando. */
void watchBossHeads();

} // namespace bl::runtime
