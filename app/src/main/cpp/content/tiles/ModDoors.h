#pragma once

namespace bl::runtime {

/**
 * Portas de mod para quem só conhece as do jogo pelo número: o jogador que
 * encosta (DoorOpeningHelper.Update) e os NPCs que andam (AI_007_TownEntities,
 * AI_003_Fighters, AI_107_ImprovedWalkers) procuram `tipo == 10` (fechada) e
 * `11` (aberta). Durante cada um, as portas de mod em volta viram 10/11 e, na
 * volta, o que o jogo abriu ou fechou vira a porta de mod certa: o mesmo
 * disfarce do OpenDoor/CloseDoor no JS (TileUseLoader), em C++, porque roda a
 * cada quadro por NPC.
 *
 * Os ganchos entram no primeiro par registrado. Sem porta de mod por perto,
 * custam uma varredura de ~120 células por chamada.
 */
void setModDoorPair(int closedType, int openType);

} // namespace bl::runtime
