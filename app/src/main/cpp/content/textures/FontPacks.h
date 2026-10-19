#pragma once
#include "core/Config.h"
#include "il2cpp/Types.h"

namespace bl::runtime {

// Publica a selecao do launcher: pacotes de textura e de fonte com content/Fonts.
void configureFontPacks(const std::string& root, const std::vector<ModSpec>& enabled);
// Thread do jogo: restaura as fontes da selecao anterior e aplica as da nova.
void tickFontPacks();

/**
 * Fonte propria de um mod: um content/Fonts/<Fonte>.json (blfont/1) vira um
 * SpriteFont NOVO, sem mexer nas fontes do jogo. Comeca como copia da
 * MouseText original (o que o arquivo nao traz sai com a letra do jogo) e
 * fica viva ate o fim do processo; o mesmo arquivo devolve a mesma fonte.
 * Thread do jogo, com o conteudo assentado. nullptr, com log, se falhar.
 */
Il2CppObject* loadModFont(const std::string& path);

} // namespace bl::runtime
