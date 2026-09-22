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
    ok &= bind(lib, "il2cpp_object_new", &object_new);
    ok &= bind(lib, "il2cpp_object_get_class", &object_get_class);
    ok &= bind(lib, "il2cpp_string_new", &string_new);
    ok &= bind(lib, "il2cpp_gchandle_new", &gchandle_new);
    ok &= bind(lib, "il2cpp_gchandle_free", &gchandle_free);
    ok &= bind(lib, "il2cpp_gchandle_get_target", &gchandle_get_target);
    ok &= bind(lib, "il2cpp_thread_attach", &thread_attach);
    // TODO(Fase 2/3): expandir (class_get_methods, method_get_param, arrays...).
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
