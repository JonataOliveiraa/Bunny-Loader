#pragma once
#include <string>
#include <utility>
#include <vector>

#include "il2cpp/Types.h"

namespace bl::runtime::content {

/**
 * O que todo conteudo de mod (item, projetil...) precisa entregar ao jogo:
 * textura como Asset ja carregado, nome por idioma como LocalizedText, e a
 * escrita disso nas tabelas por tipo. Thread do jogo (cria objeto de Unity).
 */

/**
 * Asset<Texture2D> JA CARREGADO a partir de um PNG — do arquivo `path`, ou de
 * `data` quando nao for nulo. O jogo so desenha pelo asset e antes pergunta o
 * estado: NotLoaded o faria pedir "Images/Item_6147" ao disco. Filtro de
 * vizinho-mais-proximo (pixel art). nullptr, com log, se falhar.
 */
Il2CppObject* loadTextureAsset(const std::string& path, const unsigned char* data, size_t size,
                               const std::string& assetName, int* width, int* height);

using CultureNames = std::vector<std::pair<std::string, std::string>>;

/** O texto na cultura do jogo agora: exata, pela lingua, en-US ou "", qualquer; senao `fallback`. */
std::string textForCulture(const CultureNames& names, const std::string& fallback);

/** new LocalizedText(key, text), ou nullptr. */
Il2CppObject* makeLocalizedText(const std::string& key, const std::string& text);

/** tabela[index] = value no array estatico `table`, pelo Array.SetValue (barreira do coletor). */
bool setTableElement(FieldInfo* table, int index, Il2CppObject* value);

} // namespace bl::runtime::content
