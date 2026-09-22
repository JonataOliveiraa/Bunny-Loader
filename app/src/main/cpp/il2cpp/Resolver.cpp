#include "il2cpp/Resolver.h"
#include "core/Log.h"
#include <string>

namespace bl::il2cpp {

Il2CppClass* findClass(const TypeRef& ref) {
    auto& a = api();
    if (!a.gameImage) return nullptr;
    std::string ns(ref.ns);
    std::string name(ref.name);
    Il2CppClass* cls = a.class_from_name(a.gameImage, ns.c_str(), name.c_str());
    if (!cls && a.corlibImage) cls = a.class_from_name(a.corlibImage, ns.c_str(), name.c_str());
    // TODO(Fase 3): resolver ref.nested via class_get_nested_types.
    if (!cls) BL_ERROR("classe nao encontrada: %s.%s", ns.c_str(), name.c_str());
    return cls;
}

FieldInfo* findField(Il2CppClass* cls, std::string_view name) {
    auto& a = api();
    std::string n(name);
    for (Il2CppClass* c = cls; c; c = a.class_get_parent(c)) {
        FieldInfo* f = a.class_get_field_from_name(c, n.c_str());
        if (f) return f;
    }
    return nullptr;
}

int32_t fieldOffset(Il2CppClass* cls, std::string_view name) {
    FieldInfo* f = findField(cls, name);
    if (!f) {
        BL_ERROR("campo nao encontrado: %.*s", (int)name.size(), name.data());
        return -1;
    }
    return static_cast<int32_t>(api().field_get_offset(f));
}

} // namespace bl::il2cpp
