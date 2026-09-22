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
int32_t fieldOffset(Il2CppClass* cls, std::string_view name); // sobe a hierarquia
FieldInfo* findField(Il2CppClass* cls, std::string_view name);

} // namespace bl::il2cpp
