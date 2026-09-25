#pragma once
#include "script/ScriptEngine.h"

#if BL_HAVE_QUICKJS
#include "quickjs.h"

namespace bl::script {

/**
 * bl.tiles (tiles de mod: register, typeOf, isModTile, typeAt). Ver
 * runtime/ModTiles.h para o que acontece no jogo.
 */
void installTilesApi(JSContext* ctx, JSValue bl);

} // namespace bl::script
#endif
