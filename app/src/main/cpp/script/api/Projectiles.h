#pragma once
#include "script/bridge/ScriptEngine.h"

#if BL_HAVE_QUICKJS
#include "quickjs.h"

namespace bl::script {

/**
 * bl.projectiles (projeteis de mod: register, isModProjectile). Ver
 * content/projectiles/ModProjectiles.h para o que acontece no jogo.
 */
void installProjectilesApi(JSContext* ctx, JSValue bl);

} // namespace bl::script
#endif
