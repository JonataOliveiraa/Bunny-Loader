#pragma once
#include <string>
#include <vector>
#include "il2cpp/Types.h"

namespace bl::il2cpp {

struct Signature {
    std::string returnType;
    std::string name;
    std::vector<std::string> paramTypes;
    std::vector<std::string> paramNames;
    bool valid = false;
};

Signature parseSignature(std::string_view text);

const MethodInfo* findMethodBySignature(Il2CppClass* cls, const Signature& sig,
                                        bool* ambiguous = nullptr,
                                        const MethodInfo** nameMismatch = nullptr);

std::string explainParamNames(const MethodInfo* m, const Signature& sig);

std::vector<std::string> listOverloads(Il2CppClass* cls, std::string_view name);

std::string describeMethod(const MethodInfo* m);

}
