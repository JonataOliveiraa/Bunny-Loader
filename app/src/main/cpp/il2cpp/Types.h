#pragma once
#include <cstdint>

struct Il2CppDomain;
struct Il2CppAssembly;
struct Il2CppImage;
struct Il2CppClass;
struct Il2CppType;
struct Il2CppThread;
struct MethodInfo;
struct FieldInfo;

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
