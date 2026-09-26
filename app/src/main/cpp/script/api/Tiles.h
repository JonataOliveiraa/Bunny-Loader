#pragma once
#include "script/bridge/ScriptEngine.h"

#if BL_HAVE_QUICKJS
#include "quickjs.h"

namespace bl::script {

/**
 * bl.tiles (tiles de mod: register, typeOf, isModTile, typeAt). Ver
 * content/tiles/ModTiles.h para o que acontece no jogo.
 */
void installTilesApi(JSContext* ctx, JSValue bl);

} // namespace bl::script
#endif
