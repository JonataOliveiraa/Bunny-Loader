#pragma once

namespace bl::runtime {

/**
 * Morador de mod no mundo salvo. O .wld do jogo guarda o morador pelo NUMERO
 * do tipo (e a sala dele, e a lista de "ja passou pelo shimmer"); aberto sem o
 * mod, esse numero cai fora das tabelas do jogo. Como o tModLoader (.twld):
 * no save, o morador de mod fica fora do .wld e vai, PELO NOME, para
 * `<mundo>.wld.npcs.bl`, com posicao, casa, nome proprio e variacao; no load,
 * volta. Mod ausente: a entrada fica guardada no arquivo ate ele voltar.
 */
void installModNpcSave();

} // namespace bl::runtime
