#pragma once
#include "script/ScriptEngine.h"

#if BL_HAVE_QUICKJS
#include "quickjs.h"

namespace bl::script {

/**
 * bl.loadTexture(caminho) -> Texture2D do jogo, pronto para o SpriteBatch.
 *
 * Caminho relativo vale a partir da pasta do mod que esta carregando, entao
 * `bl.loadTexture('icone.png')` acha o arquivo ao lado do main.js. Caminho
 * absoluto e usado como veio.
 *
 * Aceita o que a Unity decodifica: PNG e JPG.
 */
JSValue loadTexture(JSContext* ctx, int argc, JSValueConst* argv);

} // namespace bl::script
#endif
