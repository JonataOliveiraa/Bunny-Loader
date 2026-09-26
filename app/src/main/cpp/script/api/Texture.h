#pragma once
#include "script/bridge/ScriptEngine.h"
#include <string>

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

/**
 * bl.loadTextureAsset(caminho) -> Asset<Texture2D> do jogo, ja carregado: o que
 * as tabelas e os perfis do jogo guardam (TextureAssets, retrato de morador).
 * Thread do jogo.
 */
JSValue loadTextureAsset(JSContext* ctx, int argc, JSValueConst* argv);

/** Caminho relativo a pasta do mod de quem chamou (a mesma regra do loadTexture). */
std::string resolveModPath(JSContext* ctx, const std::string& caminho);

/** Uid do mod de quem chamou, pelo modulo JS. Vazio se nao da para saber. */
std::string callerModId(JSContext* ctx);

} // namespace bl::script
#endif
