#include "il2cpp/Resolver.h"
#include "core/Log.h"
#include <string>

namespace bl::il2cpp {

namespace {

/**
 * Procura em TODOS os assemblies carregados, nao so no do jogo e no corlib.
 *
 * `UnityEngine.Texture2D` mora na CoreModule e `ImageConversion` na
 * ImageConversionModule: nenhuma das duas aparecia, e por tabela nada de
 * UnityEngine era alcancavel — nem pelo nucleo, nem por um mod.
 *
 * O jogo e o corlib vem primeiro porque sao a esmagadora maioria das buscas; o
 * resto e varredura, so quando os dois primeiros falham.
 */
Il2CppClass* procurar(const char* ns, const char* name) {
    auto& a = api();
    if (!a.gameImage) return nullptr;
    if (Il2CppClass* c = a.class_from_name(a.gameImage, ns, name)) return c;
    if (a.corlibImage) {
        if (Il2CppClass* c = a.class_from_name(a.corlibImage, ns, name)) return c;
    }
    if (!a.domain_get || !a.domain_get_assemblies || !a.assembly_get_image) return nullptr;

    size_t n = 0;
    const Il2CppAssembly** todos = a.domain_get_assemblies(a.domain_get(), &n);
    if (!todos) return nullptr;
    for (size_t i = 0; i < n; ++i) {
        const Il2CppImage* img = a.assembly_get_image(todos[i]);
        if (!img || img == a.gameImage || img == a.corlibImage) continue;
        if (Il2CppClass* c = a.class_from_name(img, ns, name)) return c;
    }
    return nullptr;
}

} // namespace

Il2CppClass* findClass(const TypeRef& ref) {
    std::string ns(ref.ns);
    std::string name(ref.name);
    Il2CppClass* cls = procurar(ns.c_str(), name.c_str());
    // TODO(Fase 3): resolver ref.nested via class_get_nested_types.
    if (!cls) BL_ERROR("classe nao encontrada: %s.%s", ns.c_str(), name.c_str());
    return cls;
}

Il2CppClass* findClassQuiet(const std::string& ns, const std::string& name) {
    return procurar(ns.c_str(), name.c_str());
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
