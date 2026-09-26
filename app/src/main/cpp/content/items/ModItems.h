#pragma once
#include <string>
#include <utility>
#include <vector>

#include "il2cpp/Types.h"

namespace bl::runtime {

/**
 * Itens de mod: tipos NOVOS, com id depois dos do jogo.
 *
 * O id e ItemID.Count + a ordem de registro, entao sai na hora do
 * `bl.items.register` — o mod ja pode usar o numero —, mas o jogo so fica
 * sabendo depois: as tabelas indexadas por tipo de item (texturas, nomes, os
 * ItemID.Sets) sao aumentadas na thread do jogo, quando elas ja existem.
 *
 * Tres coisas do jogo tratam id acima de ItemID.Count como invalido, e cada
 * uma e contornada num lugar:
 *   - Item.SetDefaults zera o tipo: o hook de SetDefaults dos itens de mod
 *     (Bindings.cpp) nao chama o original para eles;
 *   - toda tabela nasce com ItemID.Count posicoes: `tickModItems` as aumenta;
 *   - Player.LoadPlayer transforma tipo desconhecido em nada: o save dos
 *     itens de mod vai num arquivo ao lado do personagem (ModItemSave.cpp).
 */

/** ItemID.Count do jogo. E `const` no C#, nao ha campo para ler: vem do dump. */
constexpr int kVanillaItemCount = 6147;

struct ModItemDef {
    std::string mod;      // uid do mod que registrou
    std::string name;     // chave estavel dentro do mod
    std::string texture;  // caminho absoluto do PNG
    // Ou o PNG em memoria (itens do proprio loader, como o "?"): tem precedencia.
    const unsigned char* textureData = nullptr;
    size_t textureSize = 0;
    /** Nome por cultura ("pt-BR" -> "Espada"), UTF-8. "" = serve para qualquer uma. */
    std::vector<std::pair<std::string, std::string>> names;
    /** Tooltip por cultura, linhas separadas por '\n'. Vazio = sem tooltip. */
    std::vector<std::pair<std::string, std::string>> tooltips;
};

/**
 * Reserva o proximo id. THREAD-SAFE (mods carregam na thread da sonda).
 * -1 se o mesmo mod ja registrou esse nome.
 */
int registerModItem(ModItemDef def);

bool isModItem(int type);

/**
 * O tooltip de um item de mod, por cultura. Vale a qualquer hora: antes da
 * instalacao fica guardado; depois, entra no cache de tooltips do jogo
 * (Lang._itemTooltipCache) e e reaplicado se a troca de idioma o refizer.
 */
void setModItemTooltip(int type, std::vector<std::pair<std::string, std::string>> tooltips);

/**
 * Nome estavel do item de mod, "<uid do mod>/<nome>", ou "" se `type` nao e
 * de mod. E o que o save guarda: o numero muda com os mods instalados.
 */
std::string modItemKey(int type);

/** O tipo de "<uid>/<nome>" nesta sessao, ou -1 se nenhum mod carregado o registrou. */
int modItemTypeByKey(const std::string& key);

/**
 * Item de mod AUSENTE (o mod foi desligado ou removido): vira um "?" sem uso
 * nenhum, que guarda o lugar, a pilha e o prefixo, e volta a ser o item quando
 * o mod voltar. Como o UnloadedItem do tModLoader.
 *
 * O "?" nao pode guardar de quem era num campo do item: o bau desta versao
 * guarda ChestItem (tipo, pilha, prefixo, favorito — 6 bytes), e mover para o
 * bau copia so isso. Entao a identidade vai no TIPO: uma reserva de tipos
 * "?", e cada item ausente distinto ganha um deles na sessao.
 */
constexpr int kUnloadedPoolSize = 64;

/** Registra a reserva. Uma vez, DEPOIS dos mods (os ids deles nao mudam). */
void registerUnloadedPool();

bool isUnloadedType(int type);

/**
 * O "?" que representa `key` nesta sessao (o mesmo para a mesma chave); -1 se
 * a reserva acabou ou ainda nao esta nas tabelas do jogo. modItemKey() de um
 * "?" devolve a chave original, entao quem salva nao precisa saber dele.
 */
int unloadedTypeFor(const std::string& key);

/** O setDefaults do "?": sem JS, e o jogo so tem o hook dos mods. */
void setupUnloadedItem(Il2CppObject* item, int type);

/** Vanilla + itens de mod JA instalados nas tabelas do jogo. */
int itemTypeCount();

/**
 * Instala o que foi registrado, quando o jogo ja criou as tabelas; e, a cada
 * quadro, confere se alguma foi recriada com o tamanho de fabrica (a troca de
 * idioma refaz os caches de nome) e a aumenta de novo. Thread do jogo.
 */
void tickModItems();

/** Tudo que foi registrado ja esta nas tabelas do jogo (ou falhou de vez). */
bool modItemsSettled();

/**
 * Chamado uma vez por lote instalado (thread do jogo), com os tipos novos —
 * o jogo ja conhece o item: sets e amostras existem. E onde roda o
 * SetStaticDefaults do mod. Inclui a reserva "?", que nao e de mod nenhum.
 */
using ItemsInstalledHook = void (*)(int first, int last);
void setItemsInstalledHook(ItemsInstalledHook hook);

/**
 * O que o SetDefaults do jogo faria antes dos SetDefaultsN: ResetStats e o
 * tipo. Chamado pelo hook, antes do setDefaults do mod.
 */
void prepareModItem(Il2CppObject* item, int type);

/** Depois do setDefaults do mod: tamanho pela textura, se ele nao disse. */
void finishModItem(Il2CppObject* item, int type);

struct ModItemInfo {
    int type;
    std::string mod, name, texture;
};

/** Os registrados, na ordem do id. Copia: serve a qualquer thread. */
std::vector<ModItemInfo> modItems();

// O catalogo do menu (pastas por mod) esta em ModMenu.h.

} // namespace bl::runtime
