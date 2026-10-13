#pragma once
#include "script/bridge/ScriptEngine.h"

#if BL_HAVE_QUICKJS
#include "quickjs.h"

namespace bl::script {

/**
 * bl.screen: o piso dos efeitos de tela (graphics/). Os mods usam as classes
 * de script/js/mod/Effects (ModScreenEffect, ScreenEffects, ScreenGraphics).
 *
 *   bl.screen.shader(nome, glsl)   -> id; compila na thread de render
 *   bl.screen.shaderStatus(id)     -> { state: 'pending' | 'ready' | 'failed', error }
 *   bl.screen.submit(efeitos)      -> os efeitos do quadro (ver screen_submit)
 *   bl.screen.__stats()            -> contadores do hook e do GL (testes)
 *   bl.screen.__trace()            -> um quadro de chamadas GL no log (testes)
 *
 * Thread do jogo.
 */
void installScreenApi(JSContext* ctx, JSValue bl);

} // namespace bl::script
#endif
