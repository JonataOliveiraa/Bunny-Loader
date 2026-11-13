#pragma once
#include "il2cpp/Api.h"

#include <string>

namespace bl::il2cpp {

struct Caught {
    bool threw = false;
    Il2CppObject* exception = nullptr;
    const char* cppType = nullptr;
};

bool guardedCall(void (*body)(void*), void* arg, Caught* caught = nullptr);

template <class F>
bool guarded(F& fn, Caught* caught = nullptr) {
    return guardedCall([](void* p) { (*static_cast<F*>(p))(); }, &fn, caught);
}

std::string describeCaught(const Caught& c);

std::string describeException(Il2CppObject* exception);

}
