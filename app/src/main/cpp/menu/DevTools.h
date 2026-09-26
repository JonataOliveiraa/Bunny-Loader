#pragma once

namespace bl::runtime {

/**
 * Reiniciar o jogo pelo Mod Menu, para os mods serem lidos de novo da pasta.
 *
 * Quem reinicia e o Java (fecha o processo do jogo e abre de novo pela
 * RestartActivity do launcher); aqui fica a parte que precisa da thread do
 * jogo: salvar o jogador e o mundo antes, se pedido.
 *
 *   requestRestart(save)   thread de UI: pede
 *   tickRestart()          thread do jogo, a cada quadro: salva (se pedido)
 *   restartReady()         thread de UI: pronto para fechar o processo
 */
void requestRestart(bool save);
bool restartReady();
void tickRestart();

} // namespace bl::runtime
