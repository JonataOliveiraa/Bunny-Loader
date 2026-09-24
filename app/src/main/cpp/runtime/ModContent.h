#pragma once

namespace bl::runtime {

/**
 * O momento em que o conteudo de mod pode ser ligado ao resto do jogo:
 * itens, projeteis e NPCs de mod nas tabelas (e o SetStaticDefaults dos NPCs
 * ja rodado), as receitas do jogo montadas, a tabela de drop e o Bestiario
 * criados. E a hora do PostSetupContent do tModLoader: receitas de mod,
 * entradas do Bestiario.
 *
 * Uma vez por sessao. Thread do jogo, chamado do DoUpdate.
 */
using ContentReadyHook = void (*)();
void setContentReadyHook(ContentReadyHook hook);
void tickContentReady();

} // namespace bl::runtime
