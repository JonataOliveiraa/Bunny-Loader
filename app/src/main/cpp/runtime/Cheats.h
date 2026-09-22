#pragma once

namespace bl::runtime {

// Menu de cheats (acoes nativas). Por ora: dar um item ao jogador.
//
// A UI (botao) vive no launcher e escreve um arquivo de comando; a lib le esse
// arquivo a cada frame (hook em Main.DoUpdate) e executa a acao na thread do
// jogo. Resolve as refs necessarias na primeira chamada.
void installCheats();

// Da `type` ao jogador local (spawna no mundo, na posicao dele). No-op fora do
// mundo (sem jogador). Deve ser chamada na thread do jogo.
void giveItem(int type);

} // namespace bl::runtime
