#pragma once
#include <cstdint>
#include <string_view>
#include "il2cpp/Api.h"

namespace bl::il2cpp {

struct TypeRef {
    std::string_view ns;
    std::string_view name;
    std::string_view nested; // classe aninhada, ex: ProjectileID/Sets
};

// Resolve por nome. Chamado SÓ no carregamento; nunca no caminho quente.
Il2CppClass* findClass(const TypeRef& ref);
// A aninhada `name` dentro de `outer` (ItemID -> Sets). nullptr se nao ha.
Il2CppClass* findNested(Il2CppClass* outer, std::string_view name);

// Igual, mas SEM logar quando nao acha. A arvore de namespaces (Terraria.*)
// consulta a cada acesso de propriedade para decidir se o nome e uma classe ou
// um sub-namespace; com log, um `Terraria.ID.ItemID` encheria a trilha de
// "classe nao encontrada: Terraria.ID".
Il2CppClass* findClassQuiet(const std::string& ns, const std::string& name);
int32_t fieldOffset(Il2CppClass* cls, std::string_view name); // sobe a hierarquia
FieldInfo* findField(Il2CppClass* cls, std::string_view name);

} // namespace bl::il2cpp
