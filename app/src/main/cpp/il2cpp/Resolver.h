#pragma once
#include <cstdint>
#include <string_view>
#include "il2cpp/Api.h"

namespace bl::il2cpp {

struct TypeRef {
    std::string_view ns;
    std::string_view name;
    std::string_view nested;
};

Il2CppClass* findClass(const TypeRef& ref);
Il2CppClass* findNested(Il2CppClass* outer, std::string_view name);

Il2CppClass* findClassQuiet(const std::string& ns, const std::string& name);
int32_t fieldOffset(Il2CppClass* cls, std::string_view name);
FieldInfo* findField(Il2CppClass* cls, std::string_view name);

}
