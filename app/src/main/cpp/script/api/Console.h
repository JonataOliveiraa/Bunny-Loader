#pragma once
#include <string>
#include <vector>

namespace bl::script {

/**
 * O console do Editor: codigo digitado no aparelho, rodado na thread do jogo,
 * e o que ele e os mods dizem de volta.
 *
 * O Java manda o codigo (consoleSubmit, na thread de UI) e busca a saida
 * (consoleTake); quem roda e o tickConsole, no Main.DoUpdate. O codigo vai ao
 * runtime .NET (BunnyLoader.Host.ConsoleSubmit), que compila fora da thread do
 * jogo, e roda num tickConsole seguinte. Cada
 * entrada da saida e um texto com a ESPECIE no primeiro caractere:
 *
 *   r  resultado de um codigo do console
 *   x  erro de um codigo do console (com a pilha)
 *   l  linha do log (log dos mods, resumo do nucleo)
 *   w  aviso do log
 *   e  erro do log (hook quebrado, mod que nao carregou...)
 */
void consoleSubmit(std::string code);
std::vector<std::string> consoleTake();

/** Thread do jogo, a cada quadro: roda o que o console mandou. */
void tickConsole();

void consolePrint(char kind, const std::string& text);

void installConsoleLog();


} // namespace bl::script
