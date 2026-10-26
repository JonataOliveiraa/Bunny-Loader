#pragma once
#include <cstdint>

namespace bl::dotnet {

// Tipos opacos do Mono: a libbunny nao inclui os headers dele nem liga contra
// a libmonosgen. Tudo vem por dlsym, como a API do IL2CPP (il2cpp/Api.h):
// sem a lib no APK, os mods C# ficam desligados e o resto segue igual.
struct MonoDomain;
struct MonoAssembly;
struct MonoImage;
struct MonoClass;
struct MonoMethod;
struct MonoThread;

// MonoError tem tamanho fixo na ABI (mono/utils/mono-error.h): 2 palavras de
// codigo e flags e 12 ponteiros.
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
    // Suspensao cooperativa: a thread fora do C# fica em BLOCKING (o retorno de
    // um [UnmanagedCallersOnly] a deixa assim); as chamadas da API de
    // embutir esperam RUNNING. `stackdata` e um endereco da pilha de quem chama.
    void* (*mono_threads_enter_gc_unsafe_region_unbalanced)(void** stackdata) = nullptr;
    char* (*mono_get_runtime_build_info)() = nullptr;
    void (*mono_free)(void* ptr) = nullptr;

    // Abre a libmonosgen-2.0.so (nativeLibraryDir) e resolve tudo. false se a
    // lib nao esta no APK ou falta algum simbolo.
    bool load();
    bool loaded() const { return loaded_; }

private:
    bool loaded_ = false;
};

MonoApi& mono();

} // namespace bl::dotnet
