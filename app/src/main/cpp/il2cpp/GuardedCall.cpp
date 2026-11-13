#include "il2cpp/GuardedCall.h"

#include <cstdint>
#include <cstring>
#include <dlfcn.h>
#include <unwind.h>

#if defined(__aarch64__)

namespace {

struct Raw {
    bool cxx;
    const void* typeAt88;
    const void* typeAt80;
    void* thrownWord;
};

constexpr uint64_t kMarkMagic = 0x626c5f65685f6d6bull;
constexpr uintptr_t kMarkOffset = 160;

constexpr uint64_t kClangCxx = 0x434C4E47432B2B00ull;
constexpr uint64_t kGnuCxx = 0x474E5543432B2B00ull;

void copyRaw(uint64_t cls, _Unwind_Exception* ue, Raw* out) {
    *out = Raw{};
    if (cls != kClangCxx && cls != kGnuCxx) return;
    auto* base = reinterpret_cast<char*>(ue);
    out->cxx = true;
    out->typeAt88 = *reinterpret_cast<void* const*>(base - 88);
    out->typeAt80 = *reinterpret_cast<void* const*>(base - 80);
    out->thrownWord = *reinterpret_cast<void**>(base + sizeof(_Unwind_Exception));
}

uintptr_t* findMark() {
    auto* p = static_cast<uintptr_t*>(__builtin_frame_address(0));
    while (*p != (kMarkMagic ^ reinterpret_cast<uintptr_t>(p))) ++p;
    return p;
}

bool inLoadedModule(const void* p) {
    Dl_info info;
    return p && dladdr(p, &info) != 0 && info.dli_fbase != nullptr;
}

const char* thrownTypeName(const Raw& r) {
    for (const void* type : {r.typeAt88, r.typeAt80}) {
        if (!inLoadedModule(type)) continue;
        const char* name = reinterpret_cast<const char* const*>(type)[1];
        if (inLoadedModule(name)) return name;
    }
    return nullptr;
}

}

extern "C" {

int bl_eh_try(void (*body)(void*), void* arg, void* raw);
[[noreturn]] void bl_eh_land(uintptr_t sp);

__attribute__((visibility("hidden"))) _Unwind_Reason_Code
bl_eh_personality(int version, _Unwind_Action actions, uint64_t cls, _Unwind_Exception* ue,
                  _Unwind_Context*) {
    if (version != 1 || !ue) return _URC_FATAL_PHASE1_ERROR;
    if (actions & _UA_SEARCH_PHASE) return _URC_HANDLER_FOUND;
    if ((actions & _UA_FORCE_UNWIND) || !(actions & _UA_HANDLER_FRAME)) {
        return _URC_CONTINUE_UNWIND;
    }
    uintptr_t* mark = findMark();
    copyRaw(cls, ue, reinterpret_cast<Raw*>(mark[1]));
    if (ue->exception_cleanup) ue->exception_cleanup(_URC_FOREIGN_EXCEPTION_CAUGHT, ue);
    bl_eh_land(reinterpret_cast<uintptr_t>(mark) - kMarkOffset);
}

}

namespace bl::il2cpp {

bool guardedCall(void (*body)(void*), void* arg, Caught* caught) {
    Raw raw;
    if (!bl_eh_try(body, arg, &raw)) {
        if (caught) *caught = Caught{};
        return true;
    }
    if (caught) {
        *caught = Caught{};
        caught->threw = true;
        const char* name = raw.cxx ? thrownTypeName(raw) : nullptr;
        if (name && std::strstr(name, "Il2CppExceptionWrapper")) {
            caught->exception = static_cast<Il2CppObject*>(raw.thrownWord);
        } else {
            caught->cppType = name;
        }
    }
    return false;
}

}

#else

namespace bl::il2cpp {
bool guardedCall(void (*body)(void*), void* arg, Caught* caught) {
    body(arg);
    if (caught) *caught = Caught{};
    return true;
}
}

#endif

namespace bl::il2cpp {

std::string describeException(Il2CppObject* exception) {
    if (!exception) return "exception without an object";
    auto& a = api();
    if (a.format_exception) {
        char buf[512];
        buf[0] = 0;
        a.format_exception(exception, buf, sizeof(buf));
        buf[sizeof(buf) - 1] = 0;
        if (buf[0]) return buf;
    }
    Il2CppClass* cls = a.object_get_class ? a.object_get_class(exception) : nullptr;
    if (!cls) return "C# exception";
    std::string ns = a.class_get_namespace(cls);
    return (ns.empty() ? "" : ns + ".") + a.class_get_name(cls);
}

std::string describeCaught(const Caught& c) {
    if (c.exception) return describeException(c.exception);
    if (c.cppType) return std::string("C++ exception ") + c.cppType;
    return "exception of unknown type";
}

}
