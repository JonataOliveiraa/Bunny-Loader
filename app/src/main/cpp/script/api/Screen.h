#pragma once
#include "script/bridge/ScriptEngine.h"

#if BL_HAVE_QUICKJS
#include "quickjs.h"

namespace bl::script {

/**
 * bl.screen: os efeitos de tela (graphics/RenderHook.h). Por enquanto so a
 * prova de conceito:
 *
 *   bl.screen.__poc(ligado, u, v, raio, giro)  -> redemoinho em volta de (u, v)
 *   bl.screen.__stats()                        -> contadores do hook e do GL
 *   bl.screen.__trace()                        -> um quadro de chamadas GL no log
 */
void installScreenApi(JSContext* ctx, JSValue bl);

} // namespace bl::script
#endif
