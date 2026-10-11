#include "il2cpp/GuardedCall.h"

#include <cstdint>
#include <cstring>
#include <dlfcn.h>
#include <unwind.h>

#if defined(__aarch64__)

namespace {

/**
 * O que a personalidade copia da excecao antes de libera-la: so palavras, sem
 * chamar nada (ela roda dentro do desenrolador de quem lancou). Quem entende
 * os valores e o guardedCall, depois do pouso.
 */
struct Raw {
    bool cxx;              // excecao C++ (CLNGC++ ou GNUCC++)
    const void* typeAt88;
    const void* typeAt80;
    void* thrownWord;      // a primeira palavra do objeto lancado
};

// A marca que o trampolim grava no proprio frame (ver GuardedCall.S): em
// [sp+160] vai kMarkMagic ^ (sp+160), em [sp+168] o Raw desta chamada.
constexpr uint64_t kMarkMagic = 0x626c5f65685f6d6bull;
constexpr uintptr_t kMarkOffset = 160;

// O exception_class de uma excecao C++: "CLNGC++\0" (libc++abi) e
// "GNUCC++\0" (libsupc++).
constexpr uint64_t kClangCxx = 0x434C4E47432B2B00ull;
constexpr uint64_t kGnuCxx = 0x474E5543432B2B00ull;

/**
 * Copia o que interessa do __cxa_exception, que termina no _Unwind_Exception.
 *
 * O objeto lancado vem logo depois do _Unwind_Exception (32 bytes). O IL2CPP
 * lanca um Il2CppExceptionWrapper, cujo unico campo e o Il2CppException*.
 *
 * O `std::type_info* exceptionType` muda de lugar com a versao do libc++abi:
 * o _Unwind_Exception e alinhado a 16, e o antigo (o do NDK r21, que o
 * libil2cpp traz) deixa 8 bytes de preenchimento antes dele, o que poe o tipo
 * a -88; o novo move o preenchimento para o comeco (o campo `reserve`), e o
 * tipo fica a -80, com o referenceCount a -88. Medido no libil2cpp:
 * -88 e o tipo, -80 o destrutor (nulo).
 */
void copyRaw(uint64_t cls, _Unwind_Exception* ue, Raw* out) {
    *out = Raw{};
    if (cls != kClangCxx && cls != kGnuCxx) return;
    auto* base = reinterpret_cast<char*>(ue);
    out->cxx = true;
    out->typeAt88 = *reinterpret_cast<void* const*>(base - 88);
    out->typeAt80 = *reinterpret_cast<void* const*>(base - 80);
    out->thrownWord = *reinterpret_cast<void**>(base + sizeof(_Unwind_Exception));
}

/**
 * A marca do trampolim mais interno, subindo a pilha a partir daqui.
 *
 * Sem limite de proposito: a personalidade so chega aqui na fase 2, no frame
 * do trampolim, que o desenrolador achou nesta pilha, acima de nos. A marca
 * esta la. Uma marca velha abaixo dela nao existe: o trampolim apaga a sua ao
 * sair, pelos dois caminhos.
 */
uintptr_t* findMark() {
    auto* p = static_cast<uintptr_t*>(__builtin_frame_address(0));
    while (*p != (kMarkMagic ^ reinterpret_cast<uintptr_t>(p))) ++p;
    return p;
}

/** O ponteiro cai dentro de uma biblioteca carregada (da para ler). */
bool inLoadedModule(const void* p) {
    Dl_info info;
    return p && dladdr(p, &info) != 0 && info.dli_fbase != nullptr;
}

/**
 * O nome (mangled) do tipo lancado. Cada candidato so e lido se aponta para
 * dentro de uma biblioteca: no layout novo, -88 e um contador (1), e no
 * antigo, -80 e o destrutor (nulo, ou codigo).
 */
const char* thrownTypeName(const Raw& r) {
    for (const void* type : {r.typeAt88, r.typeAt80}) {
        if (!inLoadedModule(type)) continue;
        // std::type_info: { vtable, const char* name }.
        const char* name = reinterpret_cast<const char* const*>(type)[1];
        if (inLoadedModule(name)) return name;
    }
    return nullptr;
}

} // namespace

extern "C" {

int bl_eh_try(void (*body)(void*), void* arg, void* raw);
[[noreturn]] void bl_eh_land(uintptr_t sp);

/**
 * A personalidade do trampolim. Chamada pelo desenrolador de quem lancou,
 * com o contexto DELE — que por isso nunca e lido aqui.
 */
__attribute__((visibility("hidden"))) _Unwind_Reason_Code
bl_eh_personality(int version, _Unwind_Action actions, uint64_t cls, _Unwind_Exception* ue,
                  _Unwind_Context*) {
    if (version != 1 || !ue) return _URC_FATAL_PHASE1_ERROR;
    // Fase 1: o tratador e o trampolim. Ainda nao mexemos em nada: os frames
    // entre o throw e ele rodam as limpezas na fase 2.
    if (actions & _UA_SEARCH_PHASE) return _URC_HANDLER_FOUND;
    // Desenrolar forcado (nao existe no bionic, mas o protocolo manda): deixa
    // passar.
    if ((actions & _UA_FORCE_UNWIND) || !(actions & _UA_HANDLER_FRAME)) {
        return _URC_CONTINUE_UNWIND;
    }
    uintptr_t* mark = findMark();
    copyRaw(cls, ue, reinterpret_cast<Raw*>(mark[1]));
    // Como quem pega uma excecao estrangeira: o dono dela libera. O ponteiro
    // da excecao C# ja foi copiado para a pilha, onde o coletor o ve. (O
    // contador de std::uncaught_exceptions do libil2cpp fica com um a mais
    // nesta thread: so o __cxa_begin_catch dele o desconta.)
    if (ue->exception_cleanup) ue->exception_cleanup(_URC_FOREIGN_EXCEPTION_CAUGHT, ue);
    // Os frames abaixo do trampolim ja limparam o que tinham (a fase 2 chegou
    // aqui); o que resta deles e so pilha. Volta como se o trampolim
    // devolvesse 1.
    bl_eh_land(reinterpret_cast<uintptr_t>(mark) - kMarkOffset);
}

} // extern "C"

namespace bl::il2cpp {

bool guardedCall(void (*body)(void*), void* arg, Caught* caught) {
    Raw raw;   // so a personalidade escreve, e so quando lancou
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

} // namespace bl::il2cpp

#else

namespace bl::il2cpp {
bool guardedCall(void (*body)(void*), void* arg, Caught* caught) {
    body(arg);
    if (caught) *caught = Caught{};
    return true;
}
} // namespace bl::il2cpp

#endif

namespace bl::il2cpp {

std::string describeException(Il2CppObject* exception) {
    if (!exception) return "excecao sem objeto";
    auto& a = api();
    if (a.format_exception) {
        char buf[512];
        buf[0] = 0;
        a.format_exception(exception, buf, sizeof(buf));
        buf[sizeof(buf) - 1] = 0;
        if (buf[0]) return buf;
    }
    Il2CppClass* cls = a.object_get_class ? a.object_get_class(exception) : nullptr;
    if (!cls) return "excecao C#";
    std::string ns = a.class_get_namespace(cls);
    return (ns.empty() ? "" : ns + ".") + a.class_get_name(cls);
}

std::string describeCaught(const Caught& c) {
    if (c.exception) return describeException(c.exception);
    if (c.cppType) return std::string("excecao C++ ") + c.cppType;
    return "excecao de tipo desconhecido";
}

} // namespace bl::il2cpp
