#pragma once
#include "script/bridge/ScriptEngine.h"

#if BL_HAVE_QUICKJS
#include "quickjs.h"

namespace bl::script {

/**
 * bl.buffs (buffs de mod: register, typeOf, isModBuff). Ver
 * content/buffs/ModBuffs.h para o que acontece no jogo.
 */
void installBuffsApi(JSContext* ctx, JSValue bl);

} // namespace bl::script
#endif
