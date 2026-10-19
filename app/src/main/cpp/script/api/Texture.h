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

/**
 * bl.builtinTexture(nome) -> Asset<Texture2D> de uma imagem embutida na
 * libbunny ('bunnyHead': o coelho do botão do Mod Menu e do "Config. dos
 * Mods"). Thread do jogo.
 */
JSValue builtinTexture(JSContext* ctx, int argc, JSValueConst* argv);

/**
 * bl.vanillaTexture(caminho) -> Asset<Texture2D> de uma imagem do jogo, pelo
 * nome do conteudo ('Images/UI/ButtonPlay'): a ja carregada, ou pedida ao
 * Main.Assets (o ModContent.Request("Terraria/...") do tModLoader). Thread do jogo.
 */
JSValue vanillaTexture(JSContext* ctx, int argc, JSValueConst* argv);

/** bl.textureSize(asset ou Texture2D) -> { Width, Height } (carrega o asset do jogo se preciso). */
JSValue textureSize(JSContext* ctx, int argc, JSValueConst* argv);

/** bl.assetOfTexture(Texture2D) -> o Asset<Texture2D> carregado que a guarda, ou null. */
JSValue assetOfTexture(JSContext* ctx, int argc, JSValueConst* argv);

/**
 * bl.loadFont(caminho) -> SpriteFont novo de um content/Fonts/<Fonte>.json
 * (blfont/1) do mod, para desenhar texto com a fonte dele. Nao mexe nas
 * fontes do jogo. Thread do jogo, com o conteudo pronto.
 */
JSValue loadFont(JSContext* ctx, int argc, JSValueConst* argv);

/** bl.isTextureAsset(valor) -> o valor e um Asset<Texture2D>. */
JSValue isTextureAsset(JSContext* ctx, int argc, JSValueConst* argv);

/** Caminho relativo a pasta do mod de quem chamou (a mesma regra do loadTexture). */
std::string resolveModPath(JSContext* ctx, const std::string& caminho);

/** Uid do mod de quem chamou, pelo modulo JS. Vazio se nao da para saber. */
std::string callerModId(JSContext* ctx);

} // namespace bl::script
#endif
