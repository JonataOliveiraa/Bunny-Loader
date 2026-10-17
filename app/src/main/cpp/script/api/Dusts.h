#pragma once
#include "script/bridge/ScriptEngine.h"

#if BL_HAVE_QUICKJS
#include "quickjs.h"

namespace bl::script {

/**
 * bl.dusts (poeiras de mod: register, setUpdateType, texture, setCallbacks).
 * Ver content/dusts/ModDusts.h para o que acontece no jogo.
 */
void installDustsApi(JSContext* ctx, JSValue bl);

} // namespace bl::script
#endif
