#pragma once
#include <string>
#include <vector>
#include "il2cpp/Types.h"

namespace bl::il2cpp {

/**
 * Resolução de método por ASSINATURA C#, no formato que o modder lê no dump:
 *
 *   Item['void SetDefaults(int Type, ItemVariant variant)']
 *   Main['int NewItem(int X, int Y, int W, int H, int Type, int Stack,
 *                     bool noBroadcast, int pfix, bool noGrabDelay)']
 *
 * Por que isto existe: nome + contagem de parâmetros NÃO desambigua. O
 * `Item.NewItem` tem quatro overloads de 9 parâmetros, e escolher o errado dava
 * exceção a cada frame — foi preciso peneirar na mão em C++. Com a assinatura
 * inteira o modder diz exatamente qual quer.
 */
struct Signature {
    std::string returnType;               // vazio = não verificar
    std::string name;
    std::vector<std::string> paramTypes;
    std::vector<std::string> paramNames;  // como escritos; "" = faltou o nome
    bool valid = false;
};

/** Aceita "ret Nome(T a, U b)" e também "Nome(T a)" (sem tipo de retorno). */
Signature parseSignature(std::string_view text);

/**
 * Casa `sig` contra os métodos de `cls` (e das superclasses).
 *
 * O NOME de cada parâmetro faz parte da assinatura, exato e com a mesma
 * caixa do jogo: `PlayerDrawSet drawinfo` casa, `PlayerDrawSet drawInfo` não,
 * e só os tipos (`void X(PlayerDrawSet)`) também não. Nome diferente quer dizer
 * que a assinatura veio de outro lugar (outra versão, outro método): o erro
 * na hora mostra a certa, em vez de o hook pegar algo que ninguém conferiu.
 *
 * @param ambiguous recebe true quando mais de um método casa — nesse caso
 *   devolve nullptr, porque escolher um "qualquer" é como se chegou no bug do
 *   NewItem.
 * @param nameMismatch recebe o método cujos TIPOS casam mas um nome não (ou
 *   faltou). Sem ele (busca interna, C++), o motivo vai ao log.
 */
const MethodInfo* findMethodBySignature(Il2CppClass* cls, const Signature& sig,
                                        bool* ambiguous = nullptr,
                                        const MethodInfo** nameMismatch = nullptr);

/**
 * O que difere entre os nomes de `sig` e os de `m`, para mensagem:
 * "o parametro 1 se chama 'drawinfo', nao 'drawInfo'. No jogo: ...".
 */
std::string explainParamNames(const MethodInfo* m, const Signature& sig);

/**
 * Overloads com este nome, já formatados como assinatura. Serve para a
 * mensagem de erro dizer o que existe em vez de só "não encontrado".
 */
std::vector<std::string> listOverloads(Il2CppClass* cls, std::string_view name);

/**
 * Assinatura de um método concreto como no dump, com os nomes dos
 * parâmetros: `void SetDefaults(int Type, ItemVariant variant)`. Colada de
 * volta num `Classe['...']`, resolve o mesmo método.
 */
std::string describeMethod(const MethodInfo* m);

} // namespace bl::il2cpp
