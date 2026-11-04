#pragma once
#include <chrono>

namespace bl::runtime {

/**
 * "Inicio rapido" (Configuracoes > Desenvolvedor): o jogo abre direto no
 * mundo escolhido, com o personagem escolhido. E para quem desenvolve mod,
 * que abre o jogo dezenas de vezes por dia so para chegar no mesmo lugar.
 *
 * Tres partes, na ordem em que acontecem:
 *
 *  1. A sonda sobe quando o jogo termina de carregar (waitForGameLoaded), em
 *     vez de esperar os 10 s fixos dela.
 *  2. O splash da Re-Logic acaba assim que o jogo esta pronto E os mods ja
 *     carregaram. Sem isso ele dura 620 quadros (10,3 s) contados de um
 *     cronometro, tenha o jogo terminado de carregar ou nao.
 *  3. Na tela de titulo, com o conteudo de mod pronto, faz o que os botoes
 *     "Jogar" das duas listas fazem (tickQuickStart).
 *
 * As partes 2 e 3 so com config().fastBoot. O hook do splash vale sempre: ele
 * segura a abertura ate os mods carregarem e, depois dela, mantem a tela de
 * carga ate o conteudo de mod ficar pronto.
 */

/** Hook no Main.DrawSplash. Idempotente: o boot() e a sonda chamam. */
void installSplashHook();

void markProbeFinished();
bool gameInitialized();

/** Na sonda: volta quando o jogo termina de carregar, ou depois de `max`. */
void waitForGameLoaded(std::chrono::milliseconds max);

/**
 * Na sonda, depois de carregar os mods e instalar o nucleo. Ate aqui o
 * splash nao acaba e a entrada no mundo nao comeca: os dois mantem a ordem de
 * sempre (mods carregados antes do resto da inicializacao do jogo).
 */
void markCoreReady();
bool isCoreReady();

/** Thread do jogo, a cada quadro (hook de Main.DoUpdate). */
void tickQuickStart();

} // namespace bl::runtime
