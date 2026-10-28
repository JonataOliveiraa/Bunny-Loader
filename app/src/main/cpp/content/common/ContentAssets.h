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
 * estado: NotLoaded o faria pedir "Images/Item_6196" ao disco. Filtro de
 * vizinho-mais-proximo (pixel art). nullptr, com log, se falhar.
 */
Il2CppObject* loadTextureAsset(const std::string& path, const unsigned char* data, size_t size,
                               const std::string& assetName, int* width, int* height);

/**
 * So o Texture2D do jogo (Microsoft.Xna), sem Asset, com o FilterMode da Unity
 * pedido (0 Point, 1 Bilinear, 2 Trilinear) e mipmaps. `what` so vai no log.
 */
Il2CppObject* loadGameTexture(const std::string& path, const unsigned char* data, size_t size,
                              const std::string& what, int filterMode, int* width, int* height);
/** Libera a textura Unity de um Texture2D criado por loadGameTexture. */
void destroyGameTexture(Il2CppObject* texture);

/** Dimensoes logicas do sprite vanilla (nao as da pagina Unity/atlas). */
bool textureAssetSize(Il2CppObject* asset, int* width, int* height);
Il2CppClass* textureAssetClass();
std::string textureAssetName(Il2CppObject* asset);
Il2CppObject* textureAssetValue(Il2CppObject* asset);
Il2CppObject* textureAssetSource(Il2CppObject* asset);
int textureAssetState(Il2CppObject* asset);
bool replaceTextureAssetValue(Il2CppObject* asset, Il2CppObject* value, Il2CppObject* source, int state = 2);
std::vector<Il2CppObject*> textureAssets();
/** Libera a textura Unity criada por loadTextureAsset. Nunca para assets vanilla. */
void destroyTextureAsset(Il2CppObject* asset);

/** "Terraria\\Images\\Item_1.png" -> "Images/Item_1": o nome de uma imagem do jogo. */
std::string normalizeTextureAssetName(std::string name);

/**
 * O Asset<Texture2D> de uma imagem do jogo ("Images/Item_1"), carregado: o
 * que ja esta nas TextureAssets, ou o Main.Assets.Request<Texture2D> dele.
 * nullptr se nao ha; `missingOnMobile` = o jogo devolveu o 1x1 vazio de uma
 * imagem que o celular nao tem. Fica vivo ate o fim da sessao. Thread do jogo.
 */
Il2CppObject* vanillaTextureAsset(const std::string& name, bool* missingOnMobile);

using CultureNames = std::vector<std::pair<std::string, std::string>>;

/** O texto na cultura do jogo agora: exata, pela lingua, en-US ou "", qualquer; senao `fallback`. */
std::string textForCulture(const CultureNames& names, const std::string& fallback);

/** new LocalizedText(key, text), ou nullptr. */
Il2CppObject* makeLocalizedText(const std::string& key, const std::string& text);

std::string activeCultureName();
Il2CppObject* newLocalizedText(const std::string& key, const std::string& text);
void addLanguageKey(const std::string& key, Il2CppObject* text);
Il2CppObject* findLanguageText(const std::string& key);
bool setLocalizedTextValue(Il2CppObject* text, const std::string& value);
std::string localizedTextValue(Il2CppObject* text);

/** tabela[index] = value no array estatico `table`, pelo Array.SetValue (barreira do coletor). */
bool setTableElement(FieldInfo* table, int index, Il2CppObject* value);

} // namespace bl::runtime::content
