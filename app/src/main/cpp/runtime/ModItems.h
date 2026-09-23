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
 *   - Player.LoadPlayer transforma tipo desconhecido em nada: item de mod NAO
 *     sobrevive a salvar e voltar ao mundo. Ainda nao ha persistencia.
 */

/** ItemID.Count do jogo. E `const` no C#, nao ha campo para ler: vem do dump. */
constexpr int kVanillaItemCount = 6147;

struct ModItemDef {
    std::string mod;      // uid do mod que registrou
    std::string name;     // chave estavel dentro do mod
    std::string texture;  // caminho absoluto do PNG
    /** Nome por cultura ("pt-BR" -> "Espada"), UTF-8. "" = serve para qualquer uma. */
    std::vector<std::pair<std::string, std::string>> names;
};

/**
 * Reserva o proximo id. THREAD-SAFE (mods carregam na thread da sonda).
 * -1 se o mesmo mod ja registrou esse nome.
 */
int registerModItem(ModItemDef def);

bool isModItem(int type);

/** Vanilla + itens de mod JA instalados nas tabelas do jogo. */
int itemTypeCount();

/**
 * Instala o que foi registrado, quando o jogo ja criou as tabelas; e, a cada
 * quadro, confere se alguma foi recriada com o tamanho de fabrica (a troca de
 * idioma refaz os caches de nome) e a aumenta de novo. Thread do jogo.
 */
void tickModItems();

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

/**
 * Catalogo de itens de mod no menu, como o do TL Pro: cada mod ganha a sua
 * categoria (o nome dele, o icone dele) e pode criar outras.
 */
struct ModCategory {
    std::string mod, name, icon;   // icone: caminho absoluto de PNG, ou vazio
    std::vector<int> types;
};

/** A categoria `nome` do mod, criada se ainda nao existe. Devolve o indice. */
int modCategory(const std::string& mod, const std::string& name, const std::string& icon);

/** Poe o item na categoria (uma vez so). false se o indice nao existe. */
bool addToModCategory(int categoria, int type);

std::vector<ModCategory> modCategories();

} // namespace bl::runtime
