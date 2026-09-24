#include "il2cpp/Api.h"
#include "core/Log.h"
#include <cstring>
#include <dlfcn.h>

namespace bl::il2cpp {

Api& api() {
    static Api instance;
    return instance;
}

namespace {
bool bind(void* lib, const char* name, void* slot) {
    void* sym = dlsym(lib, name);
    if (!sym) {
        BL_ERROR("Simbolo ausente: %s", name);
        return false;
    }
    *reinterpret_cast<void**>(slot) = sym;
    return true;
}

// Simbolo que NAO derruba a carga se faltar. Quem usa checa o ponteiro e cai
// num caminho pior porem correto. Um simbolo novo no `ok &=` faria o jogo
// inteiro rodar sem mods por causa de um recurso acessorio.
void bindSoft(void* lib, const char* name, void* slot) {
    void* sym = dlsym(lib, name);
    if (!sym) {
        BL_WARN("Simbolo opcional ausente: %s", name);
        return;
    }
    *reinterpret_cast<void**>(slot) = sym;
}
}

bool Api::load() {
    void* lib = dlopen("libil2cpp.so", RTLD_NOW | RTLD_NOLOAD);
    if (!lib) {
        BL_ERROR("libil2cpp.so nao esta carregada");
        return false;
    }

    bool ok = true;
    ok &= bind(lib, "il2cpp_domain_get", &domain_get);
    ok &= bind(lib, "il2cpp_domain_get_assemblies", &domain_get_assemblies);
    ok &= bind(lib, "il2cpp_assembly_get_image", &assembly_get_image);
    ok &= bind(lib, "il2cpp_image_get_name", &image_get_name);
    ok &= bind(lib, "il2cpp_class_from_name", &class_from_name);
    ok &= bind(lib, "il2cpp_class_get_method_from_name", &class_get_method_from_name);
    ok &= bind(lib, "il2cpp_class_get_field_from_name", &class_get_field_from_name);
    ok &= bind(lib, "il2cpp_class_get_parent", &class_get_parent);
    ok &= bind(lib, "il2cpp_class_get_name", &class_get_name);
    ok &= bind(lib, "il2cpp_field_get_offset", &field_get_offset);
    ok &= bind(lib, "il2cpp_field_static_get_value", &field_static_get_value);
    ok &= bind(lib, "il2cpp_field_static_set_value", &field_static_set_value);
    ok &= bind(lib, "il2cpp_runtime_invoke", &runtime_invoke);
    ok &= bind(lib, "il2cpp_method_get_name", &method_get_name);
    ok &= bind(lib, "il2cpp_method_get_flags", &method_get_flags);
    ok &= bind(lib, "il2cpp_class_get_methods", &class_get_methods);
    ok &= bind(lib, "il2cpp_method_get_param_count", &method_get_param_count);
    ok &= bind(lib, "il2cpp_method_get_param", &method_get_param);
    ok &= bind(lib, "il2cpp_method_get_return_type", &method_get_return_type);
    ok &= bind(lib, "il2cpp_class_get_namespace", &class_get_namespace);
    ok &= bind(lib, "il2cpp_image_get_class_count", &image_get_class_count);
    ok &= bind(lib, "il2cpp_image_get_class", &image_get_class);
    ok &= bind(lib, "il2cpp_field_get_type", &field_get_type);
    ok &= bind(lib, "il2cpp_class_from_il2cpp_type", &class_from_il2cpp_type);
    ok &= bind(lib, "il2cpp_class_is_enum", &class_is_enum);
    ok &= bind(lib, "il2cpp_class_is_valuetype", &class_is_valuetype);
    bind(lib, "il2cpp_class_is_assignable_from", &class_is_assignable_from);
    bind(lib, "il2cpp_method_is_instance", &method_is_instance);
    ok &= bind(lib, "il2cpp_method_get_class", &method_get_class);
    ok &= bind(lib, "il2cpp_class_get_element_class", &class_get_element_class);
    bindSoft(lib, "il2cpp_class_get_rank", &class_get_rank);
    bindSoft(lib, "il2cpp_value_box", &value_box);
    ok &= bind(lib, "il2cpp_class_value_size", &class_value_size);
    ok &= bind(lib, "il2cpp_class_get_type", &class_get_type);
    ok &= bind(lib, "il2cpp_type_get_name", &type_get_name);
    ok &= bind(lib, "il2cpp_free", &il2cpp_free);
    ok &= bind(lib, "il2cpp_object_new", &object_new);
    ok &= bind(lib, "il2cpp_object_get_class", &object_get_class);
    bind(lib, "il2cpp_array_new", &array_new);
    ok &= bind(lib, "il2cpp_string_new", &string_new);
    ok &= bind(lib, "il2cpp_gchandle_new", &gchandle_new);
    ok &= bind(lib, "il2cpp_gchandle_free", &gchandle_free);
    bindSoft(lib, "il2cpp_gc_alloc_fixed", &gc_alloc_fixed);
    bindSoft(lib, "il2cpp_gc_free_fixed", &gc_free_fixed);
    bindSoft(lib, "il2cpp_gc_wbarrier_set_field", &gc_wbarrier_set_field);
    bindSoft(lib, "il2cpp_gc_is_incremental", &gc_is_incremental);
    bindSoft(lib, "il2cpp_runtime_class_init", &runtime_class_init);
    bindSoft(lib, "il2cpp_field_get_parent", &field_get_parent);
    ok &= bind(lib, "il2cpp_gchandle_get_target", &gchandle_get_target);
    ok &= bind(lib, "il2cpp_thread_attach", &thread_attach);
    bindSoft(lib, "il2cpp_class_enum_basetype", &class_enum_basetype);
    bindSoft(lib, "il2cpp_class_get_fields", &class_get_fields);
    bindSoft(lib, "il2cpp_field_get_name", &field_get_name);
    bindSoft(lib, "il2cpp_field_get_flags", &field_get_flags);
    bindSoft(lib, "il2cpp_class_get_nested_types", &class_get_nested_types);
    if (!ok) return false;

    size_t count = 0;
    auto assemblies = domain_get_assemblies(domain_get(), &count);
    for (size_t i = 0; i < count; ++i) {
        auto image = assembly_get_image(assemblies[i]);
        const char* name = image_get_name(image);
        BL_INFO("assembly: %s", name);
        if (strcmp(name, "Assembly-CSharp.dll") == 0) gameImage = image;
        if (strcmp(name, "mscorlib.dll") == 0) corlibImage = image;
    }
    return gameImage && corlibImage;
}

} // namespace bl::il2cpp
