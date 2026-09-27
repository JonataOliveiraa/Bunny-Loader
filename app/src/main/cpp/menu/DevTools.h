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

/**
 * O Editor de JS em tela cheia congela o mundo: o Main.CanPauseGame passa a
 * dizer sim, e o jogo faz a pausa dele (a do inventario aberto com pausa
 * automatica: o DoUpdate so roda o DoUpdate_WhilePaused). O codigo do Editor
 * continua rodando, porque o tick dele vem antes, no hook do DoUpdate. So no
 * modo um jogador: com outras pessoas no mundo, ninguem para.
 *
 *   installDevTools()      sonda, com o resto do nucleo
 *   setGameFrozen(on)      thread de UI (o Editor abriu ou saiu da tela cheia)
 */
void installDevTools();
void setGameFrozen(bool on);

} // namespace bl::runtime
