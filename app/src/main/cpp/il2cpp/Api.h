#pragma once
#include <cstddef>
#include <cstdint>
#include "il2cpp/Types.h"

namespace bl::il2cpp {

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
    const char* (*class_get_namespace)(Il2CppClass*) = nullptr;
    size_t (*image_get_class_count)(const Il2CppImage*) = nullptr;
    Il2CppClass* (*image_get_class)(const Il2CppImage*, size_t) = nullptr;
    const Il2CppType* (*field_get_type)(FieldInfo*) = nullptr;
    Il2CppClass* (*class_from_il2cpp_type)(const Il2CppType*) = nullptr;
    bool (*class_is_enum)(const Il2CppClass*) = nullptr;
    bool (*class_is_valuetype)(const Il2CppClass*) = nullptr;
    bool (*class_is_assignable_from)(Il2CppClass*, Il2CppClass*) = nullptr;
    const Il2CppType* (*class_enum_basetype)(Il2CppClass*) = nullptr;
    FieldInfo* (*class_get_fields)(Il2CppClass*, void**) = nullptr;
    const char* (*field_get_name)(FieldInfo*) = nullptr;
    uint32_t (*field_get_flags)(FieldInfo*) = nullptr;
    Il2CppClass* (*method_get_class)(const MethodInfo*) = nullptr;
    Il2CppClass* (*class_get_element_class)(Il2CppClass*) = nullptr;
    int (*class_get_rank)(const Il2CppClass*) = nullptr;
    Il2CppObject* (*value_box)(Il2CppClass*, void*) = nullptr;
    int32_t (*class_value_size)(Il2CppClass*, uint32_t*) = nullptr;
    const Il2CppType* (*class_get_type)(Il2CppClass*) = nullptr;
    size_t (*field_get_offset)(FieldInfo*) = nullptr;
    Il2CppClass* (*class_get_nested_types)(Il2CppClass*, void**) = nullptr;
    void (*field_static_get_value)(FieldInfo*, void*) = nullptr;
    void (*field_static_set_value)(FieldInfo*, void*) = nullptr;
    Il2CppObject* (*runtime_invoke)(const MethodInfo*, void*, void**, Il2CppObject**) = nullptr;
    const char* (*method_get_name)(const MethodInfo*) = nullptr;
    uint32_t (*method_get_flags)(const MethodInfo*, uint32_t*) = nullptr;
    bool (*method_is_instance)(const MethodInfo*) = nullptr;
    const MethodInfo* (*class_get_methods)(Il2CppClass*, void**) = nullptr;
    uint32_t (*method_get_param_count)(const MethodInfo*) = nullptr;
    const Il2CppType* (*method_get_param)(const MethodInfo*, uint32_t) = nullptr;
    const char* (*method_get_param_name)(const MethodInfo*, uint32_t) = nullptr;
    const Il2CppType* (*method_get_return_type)(const MethodInfo*) = nullptr;
    char* (*type_get_name)(const Il2CppType*) = nullptr;
    uint32_t (*type_get_attrs)(const Il2CppType*) = nullptr;
    void (*il2cpp_free)(void*) = nullptr;
    Il2CppObject* (*object_new)(Il2CppClass*) = nullptr;
    Il2CppClass* (*object_get_class)(Il2CppObject*) = nullptr;
    Il2CppArray* (*array_new)(Il2CppClass* elementType, uintptr_t length) = nullptr;
    Il2CppArray* (*array_new_full)(Il2CppClass* arrayClass, uintptr_t* lengths, uintptr_t* lowerBounds) = nullptr;
    Il2CppString* (*string_new)(const char*) = nullptr;
    uint32_t (*gchandle_new)(Il2CppObject*, bool) = nullptr;
    void (*gchandle_free)(uint32_t) = nullptr;
    void* (*gc_alloc_fixed)(size_t) = nullptr;
    void (*gc_free_fixed)(void*) = nullptr;
    void (*gc_wbarrier_set_field)(Il2CppObject*, void**, void*) = nullptr;
    bool (*gc_is_incremental)() = nullptr;
    void (*runtime_class_init)(Il2CppClass*) = nullptr;
    Il2CppClass* (*field_get_parent)(FieldInfo*) = nullptr;
    Il2CppObject* (*gchandle_get_target)(uint32_t) = nullptr;
    uint32_t (*gchandle_new_weakref)(Il2CppObject*, bool trackResurrection) = nullptr;
    Il2CppObject* (*type_get_object)(const Il2CppType*) = nullptr;
    Il2CppClass* (*class_from_system_type)(Il2CppObject*) = nullptr;
    Il2CppObject* (*method_get_object)(const MethodInfo*, Il2CppClass*) = nullptr;
    const MethodInfo* (*method_get_from_reflection)(Il2CppObject*) = nullptr;
    Il2CppClass* (*class_get_declaring_type)(Il2CppClass*) = nullptr;
    Il2CppThread* (*thread_attach)(Il2CppDomain*) = nullptr;
    void (*thread_detach)(Il2CppThread*) = nullptr;
    void (*format_exception)(Il2CppObject*, char*, int) = nullptr;
    void (*monitor_enter)(Il2CppObject*) = nullptr;
    void (*monitor_exit)(Il2CppObject*) = nullptr;

    const Il2CppImage* gameImage = nullptr;
    const Il2CppImage* corlibImage = nullptr;

    bool load();
};

Api& api();

inline void* methodPointer(const MethodInfo* m) {
    return *reinterpret_cast<void* const*>(m);
}

}
