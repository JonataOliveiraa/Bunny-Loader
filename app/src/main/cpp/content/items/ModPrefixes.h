#pragma once
#include <string>

namespace bl::runtime {

/**
 * Prefixos (modificadores) de mod: so o NOME de cada um e o numero que ele
 * ganhou nesta sessao. O resto (tabelas, rolagem, efeitos) e do PrefixLoader,
 * no JS. O save precisa daqui: o numero depende dos mods instalados e da ordem
 * de carga, e o .plr/.wld guardam so o byte.
 */

/** PrefixID.Count de fabrica (98 nesta versao), lido do jogo na primeira vez. */
int vanillaPrefixCount();

/**
 * Registra "<mod>/<nome>" e devolve o numero: vanillaPrefixCount() + a ordem.
 * -1 se o mod ja registrou esse nome, ou se passou de 255 (Item.prefix e byte).
 */
int registerModPrefix(const std::string& mod, const std::string& name);

/** "<uid>/<nome>" do prefixo de mod, ou "" se `id` nao e de mod. */
std::string modPrefixKey(int id);

/** O numero de "<uid>/<nome>" nesta sessao, ou -1 se nenhum mod carregado o tem. */
int modPrefixByKey(const std::string& key);

} // namespace bl::runtime
