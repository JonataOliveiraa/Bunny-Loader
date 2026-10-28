#pragma once

namespace bl::runtime {

/**
 * O momento em que o conteudo de mod pode ser ligado ao resto do jogo:
 * itens, projeteis e NPCs de mod nas tabelas (e o SetStaticDefaults dos NPCs
 * ja rodado), as receitas do jogo montadas, a tabela de drop e o Bestiario
 * criados. E a hora do PostSetupContent do tModLoader: receitas de mod,
 * entradas do Bestiario.
 */
using ContentReadyHook = void (*)();
void addContentReadyHook(ContentReadyHook hook);
void tickContentReady();

/**
 * O mesmo momento, como pergunta: vale com ou sem mod que pediu o aviso. O
 * inicio rapido (boot/QuickStart) so entra no mundo depois disso.
 */
bool contentSettled();

} // namespace bl::runtime
