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
    const char* (*class_get_namespace)(Il2CppClass*) = nullptr;
    // Enumeracao de classes da imagem: usada uma vez, no boot, para descobrir
    // os namespaces RAIZ e publicar cada um como global (Terraria.*, System.*).
    size_t (*image_get_class_count)(const Il2CppImage*) = nullptr;
    Il2CppClass* (*image_get_class)(const Il2CppImage*, size_t) = nullptr;
    // Tipo do campo: sem ele nao da para saber se um campo de instancia e int
    // ou float, e ler float como int devolve lixo.
    const Il2CppType* (*field_get_type)(FieldInfo*) = nullptr;
    // Enums: o type_get_name devolve o nome do enum (Terraria.ItemVariant), nao
    // o do tipo subjacente. Sem isto, um parametro enum seria tratado como
    // referencia e iria como ponteiro nulo.
    Il2CppClass* (*class_from_il2cpp_type)(const Il2CppType*) = nullptr;
    bool (*class_is_enum)(const Il2CppClass*) = nullptr;
    bool (*class_is_valuetype)(const Il2CppClass*) = nullptr;
    // Conferir atribuicao de objeto: sem isto, gravar um NPC num campo
    // de Item passava batido e quebrava longe dali.
    bool (*class_is_assignable_from)(Il2CppClass*, Il2CppClass*) = nullptr;
    // Tipo SUBJACENTE do enum (int32 quase sempre, mas ha byte e long). Sem
    // ele um enum de 1 byte seria lido como 4 e pegaria o campo vizinho junto.
    const Il2CppType* (*class_enum_basetype)(Il2CppClass*) = nullptr;
    // Iteracao de campos: usada para detectar HFA (struct so de floats, que o
    // AAPCS passa em d0-d3) e para o toString de struct.
    FieldInfo* (*class_get_fields)(Il2CppClass*, void**) = nullptr;
    const char* (*field_get_name)(FieldInfo*) = nullptr;
    uint32_t (*field_get_flags)(FieldInfo*) = nullptr;
    Il2CppClass* (*method_get_class)(const MethodInfo*) = nullptr;
    // Arrays: tipo e tamanho do elemento. Um Player[] guarda ponteiros (8 B);
    // um Vector2[] guarda os structs em linha (8 B de dados). Sem o tamanho
    // certo o indice anda errado.
    Il2CppClass* (*class_get_element_class)(Il2CppClass*) = nullptr;
    int32_t (*class_value_size)(Il2CppClass*, uint32_t*) = nullptr;
    // Caminho de volta classe -> tipo: o elemento de um array chega como
    // classe, e quem sabe ler memoria trabalha com Il2CppType.
    const Il2CppType* (*class_get_type)(Il2CppClass*) = nullptr;
    size_t (*field_get_offset)(FieldInfo*) = nullptr;
    void (*field_static_get_value)(FieldInfo*, void*) = nullptr;
    void (*field_static_set_value)(FieldInfo*, void*) = nullptr;
    // Invoca qualquer metodo (boxing/unboxing automatico). Evita precisar de
    // bridges por assinatura e funciona sob houdini (e chamada normal).
    Il2CppObject* (*runtime_invoke)(const MethodInfo*, void*, void**, Il2CppObject**) = nullptr;
    const char* (*method_get_name)(const MethodInfo*) = nullptr;
    uint32_t (*method_get_flags)(const MethodInfo*, uint32_t*) = nullptr;
    // Estatico ou de instancia, sem deduzir de `self != nullptr`: um getter
    // estatico tambem chega com self nulo.
    bool (*method_is_instance)(const MethodInfo*) = nullptr;
    // Iteracao de metodos + tipos dos parametros (para desambiguar overloads
    // que so diferem no tipo, nao na contagem — ex.: Item.NewItem).
    const MethodInfo* (*class_get_methods)(Il2CppClass*, void**) = nullptr;
    uint32_t (*method_get_param_count)(const MethodInfo*) = nullptr;
    const Il2CppType* (*method_get_param)(const MethodInfo*, uint32_t) = nullptr;
    const Il2CppType* (*method_get_return_type)(const MethodInfo*) = nullptr;
    char* (*type_get_name)(const Il2CppType*) = nullptr;  // malloc; liberar com free
    void (*il2cpp_free)(void*) = nullptr;
    Il2CppObject* (*object_new)(Il2CppClass*) = nullptr;
    Il2CppClass* (*object_get_class)(Il2CppObject*) = nullptr;
    // Criar array no heap do jogo (byte[] para carregar textura).
    Il2CppArray* (*array_new)(Il2CppClass* elementType, uintptr_t length) = nullptr;
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
