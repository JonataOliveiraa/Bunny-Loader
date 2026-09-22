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

// Pede pra dar `type` ao jogador. THREAD-SAFE: pode ser chamada de qualquer
// thread (ex.: a UI thread, no onClick do botao). Apenas registra o pedido; o
// hook de Main.DoUpdate o executa no proximo frame, na thread do jogo (onde as
// chamadas il2cpp sao validas).
void requestGive(int type);

} // namespace bl::runtime
