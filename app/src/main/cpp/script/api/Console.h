#pragma once
#include <string>
#include <vector>

#include "script/bridge/ScriptEngine.h"

#if BL_HAVE_QUICKJS
#include "quickjs.h"
#endif

namespace bl::script {

/**
 * O console do Editor: codigo digitado no aparelho, rodado na thread do jogo,
 * e o que ele e os mods dizem de volta.
 *
 * O Java manda o codigo (consoleSubmit, na thread de UI) e busca a saida
 * (consoleTake); quem roda e o tickConsole, no Main.DoUpdate. O codigo JS roda
 * no QuickJS, com o motor travado como num hook. O codigo C# chega com o byte
 * 0x02 na frente: vai ao runtime .NET (BunnyLoader.Host.ConsoleSubmit), que
 * compila fora da thread do jogo, e roda num tickConsole seguinte. Cada
 * entrada da saida e um texto com a ESPECIE no primeiro caractere:
 *
 *   r  resultado de um codigo do console
 *   x  erro de um codigo do console (com a pilha)
 *   p  print() / bl.chat() de qualquer mod ou do console
 *   l  linha do log (bl.log dos mods, resumo do nucleo)
 *   w  aviso do log
 *   e  erro do log (hook quebrado, mod que nao carregou...)
 */
void consoleSubmit(std::string code);
std::vector<std::string> consoleTake();

/** Thread do jogo, a cada quadro: roda o que o console mandou e poe o chat na tela. */
void tickConsole();

/** Uma entrada na saida do console (especie como acima). De qualquer thread. */
void consolePrint(char kind, const std::string& text);

/**
 * Liga o log ao console (as linhas l/w/e acima), com ou sem o QuickJS. Na
 * sonda, junto com o resto do nucleo.
 */
void installConsoleLog();

#if BL_HAVE_QUICKJS
/**
 * print(...valores): uma linha no chat do jogo, com os valores juntos como no
 * bl.log. bl.chat(texto, cor?): o mesmo com cor (Color, { R, G, B } ou
 * '#RRGGBB'). De qualquer thread: a linha vai ao chat no proximo quadro.
 */
void installConsoleApi(JSContext* ctx, JSValueConst global, JSValueConst bl);
#endif

} // namespace bl::script
