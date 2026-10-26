#include "script/dotnet/MonoApi.h"
#include "core/Log.h"

#include <dlfcn.h>

#include <type_traits>

namespace bl::dotnet {

MonoApi& mono() {
    static MonoApi api;
    return api;
}

bool MonoApi::load() {
    if (loaded_) return true;
    void* lib = dlopen("libmonosgen-2.0.so", RTLD_NOW | RTLD_GLOBAL);
    if (!lib) {
        BL_WARN("dotnet: libmonosgen-2.0.so not found (%s); C# mods disabled", dlerror());
        return false;
    }
    bool ok = true;
    auto resolve = [&](auto& slot, const char* name) {
        slot = reinterpret_cast<std::remove_reference_t<decltype(slot)>>(dlsym(lib, name));
        if (!slot) {
            BL_ERROR("dotnet: missing Mono symbol %s", name);
            ok = false;
        }
    };
    resolve(monovm_initialize, "monovm_initialize");
    resolve(mono_set_assemblies_path, "mono_set_assemblies_path");
    resolve(mono_set_signal_chaining, "mono_set_signal_chaining");
    resolve(mono_set_crash_chaining, "mono_set_crash_chaining");
    resolve(mono_jit_init_version, "mono_jit_init_version");
    resolve(mono_domain_assembly_open, "mono_domain_assembly_open");
    resolve(mono_assembly_get_image, "mono_assembly_get_image");
    resolve(mono_class_from_name, "mono_class_from_name");
    resolve(mono_class_get_method_from_name, "mono_class_get_method_from_name");
    resolve(mono_method_get_unmanaged_callers_only_ftnptr, "mono_method_get_unmanaged_callers_only_ftnptr");
    resolve(mono_error_get_message, "mono_error_get_message");
    resolve(mono_error_cleanup, "mono_error_cleanup");
    resolve(mono_thread_current, "mono_thread_current");
    resolve(mono_thread_detach, "mono_thread_detach");
    resolve(mono_threads_enter_gc_unsafe_region_unbalanced, "mono_threads_enter_gc_unsafe_region_unbalanced");
    resolve(mono_get_runtime_build_info, "mono_get_runtime_build_info");
    resolve(mono_free, "mono_free");
    loaded_ = ok;
    return ok;
}

} // namespace bl::dotnet
