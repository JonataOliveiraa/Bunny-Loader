#pragma once
#include <cstdint>

// Tipos opacos do IL2CPP — só manipulados por ponteiro via a Api.
struct Il2CppDomain;
struct Il2CppAssembly;
struct Il2CppImage;
struct Il2CppClass;
struct Il2CppType;
struct Il2CppThread;
struct MethodInfo;
struct FieldInfo;

// Layouts que o núcleo lê diretamente. Estáveis em versões recentes da Unity,
// mas CONFIRA contra a versão do jogo (ler um array/string conhecido).
struct Il2CppObject {
    Il2CppClass* klass;
    void* monitor;
};

struct Il2CppArrayBounds;

struct Il2CppArray {
    Il2CppObject obj;
    Il2CppArrayBounds* bounds;
    uintptr_t length;
};

inline void* arrayData(Il2CppArray* a) {
    return reinterpret_cast<char*>(a) + sizeof(Il2CppArray);
}

struct Il2CppString {
    Il2CppObject obj;
    int32_t length;
    char16_t chars[1];
};
