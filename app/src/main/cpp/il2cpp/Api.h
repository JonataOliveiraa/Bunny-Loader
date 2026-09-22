#pragma once
#include <cstddef>
#include <cstdint>
#include "il2cpp/Types.h"

namespace bl::il2cpp {

// Ponteiros para as funções exportadas pela libil2cpp.so.
// Lista reduzida para o esqueleto; EXPANDIR conforme a API cresce
// (ver design LemonRunner §5.5 para o conjunto completo).
struct Api {
    Il2CppDomain* (*domain_get)() = nullptr;
    const Il2CppAssembly** (*domain_get_assemblies)(const Il2CppDomain*, size_t*) = nullptr;
    const Il2CppImage* (*assembly_get_image)(const Il2CppAssembly*) = nullptr;
    const char* (*image_get_name)(const Il2CppImage*) = nullptr;
    Il2CppClass* (*class_from_name)(const Il2CppImage*, const char*, const char*) = nullptr;
    const MethodInfo* (*class_get_method_from_name)(Il2CppClass*, const char*, int) = nullptr;
    FieldInfo* (*class_get_field_from_name)(Il2CppClass*, const char*) = nullptr;
    Il2CppClass* (*class_get_parent)(Il2CppClass*) = nullptr;
    const char* (*class_get_name)(Il2CppClass*) = nullptr;
    size_t (*field_get_offset)(FieldInfo*) = nullptr;
    void (*field_static_get_value)(FieldInfo*, void*) = nullptr;
    void (*field_static_set_value)(FieldInfo*, void*) = nullptr;
    // Invoca qualquer metodo (boxing/unboxing automatico). Evita precisar de
    // bridges por assinatura e funciona sob houdini (e chamada normal).
    Il2CppObject* (*runtime_invoke)(const MethodInfo*, void*, void**, Il2CppObject**) = nullptr;
    const char* (*method_get_name)(const MethodInfo*) = nullptr;
    Il2CppObject* (*object_new)(Il2CppClass*) = nullptr;
    Il2CppClass* (*object_get_class)(Il2CppObject*) = nullptr;
    Il2CppString* (*string_new)(const char*) = nullptr;
    uint32_t (*gchandle_new)(Il2CppObject*, bool) = nullptr;
    void (*gchandle_free)(uint32_t) = nullptr;
    Il2CppObject* (*gchandle_get_target)(uint32_t) = nullptr;
    Il2CppThread* (*thread_attach)(Il2CppDomain*) = nullptr;

    const Il2CppImage* gameImage = nullptr;   // Assembly-CSharp.dll
    const Il2CppImage* corlibImage = nullptr; // mscorlib.dll

    bool load();
};

Api& api();

// A MethodInfo do IL2CPP começa com o ponteiro para a função nativa.
inline void* methodPointer(const MethodInfo* m) {
    return *reinterpret_cast<void* const*>(m);
}

} // namespace bl::il2cpp
