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
    std::vector<std::string> paramTypes;  // só os tipos; nomes descartados
    bool valid = false;
};

/** Aceita "ret Nome(T a, U b)" e também "Nome(T a)" (sem tipo de retorno). */
Signature parseSignature(std::string_view text);

/**
 * Casa `sig` contra os métodos de `cls` (e das superclasses).
 *
 * @param ambiguous recebe true quando mais de um método casa — nesse caso
 *   devolve nullptr, porque escolher um "qualquer" é como se chegou no bug do
 *   NewItem.
 */
const MethodInfo* findMethodBySignature(Il2CppClass* cls, const Signature& sig,
                                        bool* ambiguous = nullptr);

/**
 * Overloads com este nome, já formatados como assinatura. Serve para a
 * mensagem de erro dizer o que existe em vez de só "não encontrado".
 */
std::vector<std::string> listOverloads(Il2CppClass* cls, std::string_view name);

/** Assinatura legível de um método concreto, para diagnóstico. */
std::string describeMethod(const MethodInfo* m);

} // namespace bl::il2cpp
