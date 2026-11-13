#pragma once
#include <cstdint>

namespace bl::dotnet {

struct MonoDomain;
struct MonoAssembly;
struct MonoImage;
struct MonoClass;
struct MonoMethod;
struct MonoThread;

struct MonoError {
    uint16_t errorCode;
    uint16_t flags;
    void* hiddenFields[12];
};

struct MonoApi {
    int (*monovm_initialize)(int count, const char** keys, const char** values) = nullptr;
    void (*mono_set_assemblies_path)(const char* path) = nullptr;
    void (*mono_set_signal_chaining)(int chain) = nullptr;
    void (*mono_set_crash_chaining)(int chain) = nullptr;
    MonoDomain* (*mono_jit_init_version)(const char* name, const char* version) = nullptr;
    MonoAssembly* (*mono_domain_assembly_open)(MonoDomain* domain, const char* path) = nullptr;
    MonoImage* (*mono_assembly_get_image)(MonoAssembly* assembly) = nullptr;
    MonoClass* (*mono_class_from_name)(MonoImage* image, const char* ns, const char* name) = nullptr;
    MonoMethod* (*mono_class_get_method_from_name)(MonoClass* cls, const char* name, int argc) = nullptr;
    void* (*mono_method_get_unmanaged_callers_only_ftnptr)(MonoMethod* method, MonoError* error) = nullptr;
    const char* (*mono_error_get_message)(MonoError* error) = nullptr;
    void (*mono_error_cleanup)(MonoError* error) = nullptr;
    MonoThread* (*mono_thread_current)() = nullptr;
    void (*mono_thread_detach)(MonoThread* thread) = nullptr;
    void* (*mono_threads_enter_gc_unsafe_region_unbalanced)(void** stackdata) = nullptr;
    char* (*mono_get_runtime_build_info)() = nullptr;
    void (*mono_free)(void* ptr) = nullptr;

    bool load();
    bool loaded() const { return loaded_; }

private:
    bool loaded_ = false;
};

MonoApi& mono();

}
