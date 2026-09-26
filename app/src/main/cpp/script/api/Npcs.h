#pragma once
#include "script/bridge/ScriptEngine.h"

#if BL_HAVE_QUICKJS
#include "quickjs.h"

namespace bl::script {

/**
 * bl.npcs (NPCs de mod: register, isModNpc). Ver content/npcs/ModNpcs.h para o
 * que acontece no jogo.
 */
void installNpcsApi(JSContext* ctx, JSValue bl);

} // namespace bl::script
#endif
