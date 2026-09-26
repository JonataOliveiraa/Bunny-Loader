#include "hook/CodePatch.h"
#include "core/Log.h"
#include "il2cpp/Api.h"

#include <dlfcn.h>
#include <sys/mman.h>
#include <unistd.h>

#include <atomic>
#include <cerrno>
#include <cstdio>
#include <cstring>

namespace bl::runtime {

namespace {

constexpr uintptr_t kMaxMethodBytes = 512 * 1024;

// A ultima escrita recusada (errno do mprotect), para o describeCompareMiss
// separar "nao achei" de "achei e nao consegui escrever".
std::atomic<int> g_writeFailures{0};
std::atomic<int> g_lastWriteErrno{0};

void* methodPointerOf(const MethodInfo* m) {
    return *reinterpret_cast<void* const*>(m);   // primeiro campo do MethodInfo (refs/il2cpp.h)
}

/**
 * Onde o metodo termina: o primeiro metodo da MESMA classe que comeca depois
 * dele. O IL2CPP gera os metodos de uma classe em sequencia, entao isso
 * limita a busca ao corpo dele (com um teto, por garantia).
 */
uintptr_t methodEnd(const MethodInfo* m, uintptr_t start) {
    auto& a = il2cpp::api();
    uintptr_t end = start + kMaxMethodBytes;
    Il2CppClass* cls = a.method_get_class ? a.method_get_class(m) : nullptr;
    if (!cls || !a.class_get_methods) return end;
    void* it = nullptr;
    while (const MethodInfo* other = a.class_get_methods(cls, &it)) {
        const auto p = reinterpret_cast<uintptr_t>(methodPointerOf(other));
        if (p > start && p < end) end = p;
    }
    return end;
}

/**
 * `cmp wN, #imm`: SUBS WZR, Wn, #imm12 (sf=0). `shifted`: a forma
 * `#imm, lsl #12` (sh=1), em que o limite vale imm * 4096.
 */
bool isCompareImmediate(uint32_t insn, uint32_t imm, bool shifted = false) {
    const uint32_t op = shifted ? 0x7140001Fu : 0x7100001Fu;
    return (insn & 0xFFC0001Fu) == op && ((insn >> 10) & 0xFFFu) == imm;
}

/**
 * b.cond de ORDEM contra o limite: HI/LS (sem sinal) ou GT/LE (com sinal) —
 * `tipo > limite` sai. Igualdade NAO: `cmp w8, #696; b.ne` e o `if (type ==
 * 696)` do desenho especial de um NPC do jogo (Main.DrawNPCDirect tem dois), e
 * troca-lo mandava o NPC de mod para aquele ramo (ficava invisivel) e tirava
 * o desenho do NPC 696.
 */
bool isLimitBranch(uint32_t insn, bool belowToo) {
    if ((insn & 0xFF000010u) != 0x54000000u) return false;   // b.cond: 0101 0100 imm19 0 cond
    const uint32_t cond = insn & 0xFu;
    if (cond == 0x8 || cond == 0x9 || cond == 0xC || cond == 0xD) return true;   // HI, LS, GT, LE
    // `tipo < Count` (LO/HS sem sinal, LT/GE com sinal): so quando quem chama
    // conferiu o metodo — nos NPCs isso nunca foi olhado.
    return belowToo && (cond == 0x2 || cond == 0x3 || cond == 0xA || cond == 0xB);
}

bool writeInstruction(uint32_t* at, uint32_t insn) {
    const long pageSize = sysconf(_SC_PAGESIZE);
    auto page = reinterpret_cast<uintptr_t>(at) & ~static_cast<uintptr_t>(pageSize - 1);
    if (mprotect(reinterpret_cast<void*>(page), static_cast<size_t>(pageSize),
                 PROT_READ | PROT_WRITE | PROT_EXEC) != 0) {
        g_lastWriteErrno.store(errno);
        g_writeFailures.fetch_add(1);
        return false;
    }
    *at = insn;
    mprotect(reinterpret_cast<void*>(page), static_cast<size_t>(pageSize), PROT_READ | PROT_EXEC);
    // O processador (e o houdini, no emulador) pode ter a instrucao velha em
    // cache: sem isto a troca pode simplesmente nao valer.
    __builtin___clear_cache(reinterpret_cast<char*>(at), reinterpret_cast<char*>(at + 1));
    return true;
}

} // namespace

int patchCompareLimit(const MethodInfo* m, uint32_t oldLimit, uint32_t newLimit, bool belowToo,
                      bool shifted) {
    if (!m || newLimit > 0xFFF || oldLimit > 0xFFF) return 0;
    const auto start = reinterpret_cast<uintptr_t>(methodPointerOf(m));
    if (!start) return 0;
    const uintptr_t end = methodEnd(m, start);
    int patched = 0;
    for (auto* p = reinterpret_cast<uint32_t*>(start); reinterpret_cast<uintptr_t>(p + 1) < end; ++p) {
        if (!isCompareImmediate(*p, oldLimit, shifted) || !isLimitBranch(p[1], belowToo)) continue;
        const uint32_t insn = (*p & ~(0xFFFu << 10)) | (newLimit << 10);
        if (writeInstruction(p, insn)) ++patched;
    }
    return patched;
}

std::string describeMethodCode(const MethodInfo* m) {
    if (!m) return "metodo nao achado pelo nome";
    const auto start = reinterpret_cast<uintptr_t>(methodPointerOf(m));
    if (!start) return "metodo sem codigo (methodPointer nulo)";
    const uintptr_t end = methodEnd(m, start);
    char where[160];
    Dl_info info{};
    if (dladdr(reinterpret_cast<void*>(start), &info) && info.dli_fbase) {
        const char* lib = info.dli_fname ? std::strrchr(info.dli_fname, '/') : nullptr;
        std::snprintf(where, sizeof(where), "%s+0x%lx", lib ? lib + 1 : "?",
                      static_cast<unsigned long>(start - reinterpret_cast<uintptr_t>(info.dli_fbase)));
    } else {
        std::snprintf(where, sizeof(where), "0x%lx", static_cast<unsigned long>(start));
    }
    // As 4 primeiras instrucoes: se um hook (ShadowHook) reescreveu a entrada,
    // aparece aqui um `ldr x17`/`br x17` ou um `b` longe, no lugar do prologo.
    const auto* p = reinterpret_cast<const uint32_t*>(start);
    char out[320];
    std::snprintf(out, sizeof(out), "metodo em %s, %lu bytes, entrada %08x %08x %08x %08x", where,
                  static_cast<unsigned long>(end - start), p[0], p[1], p[2], p[3]);
    return out;
}

std::string describeCompareMiss(const MethodInfo* m, uint32_t oldLimit, uint32_t newLimit, bool belowToo,
                                bool shifted) {
    if (!m) return "metodo nao achado pelo nome";
    if (newLimit > 0xFFF || oldLimit > 0xFFF) return "limite nao cabe no imediato de 12 bits do cmp";
    const auto start = reinterpret_cast<uintptr_t>(methodPointerOf(m));
    if (!start) return describeMethodCode(m);
    const uintptr_t end = methodEnd(m, start);
    int old = 0, oldOtherBranch = 0, already = 0;
    uint32_t otherInsn = 0;
    for (auto* p = reinterpret_cast<const uint32_t*>(start); reinterpret_cast<uintptr_t>(p + 1) < end; ++p) {
        if (isCompareImmediate(*p, oldLimit, shifted)) {
            if (isLimitBranch(p[1], belowToo)) ++old;
            else if (!oldOtherBranch++) otherInsn = p[1];
        } else if (isCompareImmediate(*p, newLimit, shifted) && isLimitBranch(p[1], belowToo)) {
            ++already;
        }
    }
    char why[200];
    if (old > 0) {
        const int err = g_lastWriteErrno.load();
        std::snprintf(why, sizeof(why), "o cmp #%u esta la (%d vez(es)), mas a escrita foi recusada "
                      "(%d falha(s) de mprotect, a ultima: %s)", oldLimit, old, g_writeFailures.load(),
                      err ? std::strerror(err) : "?");
    } else if (already > 0) {
        std::snprintf(why, sizeof(why), "ja esta #%u (%d vez(es)): trocado antes, instalacao repetida?",
                      newLimit, already);
    } else if (oldOtherBranch > 0) {
        std::snprintf(why, sizeof(why), "o cmp #%u existe (%d vez(es)), mas sem desvio de ordem logo "
                      "depois (1o: %08x)", oldLimit, oldOtherBranch, otherInsn);
    } else {
        std::snprintf(why, sizeof(why), "nenhum cmp #%u no metodo", oldLimit);
    }
    return std::string(why) + "; " + describeMethodCode(m);
}

int patchLoopEnd(const MethodInfo* m, uint32_t oldEnd, uint32_t newEnd) {
    if (!m || newEnd > 0xFFF || oldEnd > 0xFFF) return 0;
    const auto start = reinterpret_cast<uintptr_t>(methodPointerOf(m));
    if (!start) return 0;
    const uintptr_t end = methodEnd(m, start);
    int patched = 0;
    for (auto* p = reinterpret_cast<uint32_t*>(start); reinterpret_cast<uintptr_t>(p + 1) < end; ++p) {
        // cmp xN, #imm (SUBS XZR, Xn, #imm12, sf=1) + b.ne/b.lt de volta ao laco
        if ((*p & 0xFFC0001Fu) != 0xF100001Fu || ((*p >> 10) & 0xFFFu) != oldEnd) continue;
        if ((p[1] & 0xFF000010u) != 0x54000000u) continue;
        const uint32_t cond = p[1] & 0xFu;
        if (cond != 0x1 && cond != 0xB && cond != 0x3) continue;   // NE, LT, LO
        if (writeInstruction(p, (*p & ~(0xFFFu << 10)) | (newEnd << 10))) ++patched;
    }
    return patched;
}

int patchHalvedLimit(const MethodInfo* m, uint32_t oldImm, uint32_t newImm) {
    if (!m || newImm > 0xFFF || oldImm > 0xFFF) return 0;
    const auto start = reinterpret_cast<uintptr_t>(methodPointerOf(m));
    if (!start) return 0;
    const uintptr_t end = methodEnd(m, start);
    int patched = 0;
    for (auto* p = reinterpret_cast<uint32_t*>(start); reinterpret_cast<uintptr_t>(p + 2) < end; ++p) {
        // lsr wB, wA, #1 = UBFM wB, wA, #1, #31
        if ((*p & 0xFFFFFC00u) != 0x53017C00u) continue;
        const uint32_t reg = *p & 31u;
        if (!isCompareImmediate(p[1], oldImm) || ((p[1] >> 5) & 31u) != reg) continue;
        if ((p[2] & 0xFF00001Fu) != 0x54000008u) continue;   // b.hi
        if (writeInstruction(p + 1, (p[1] & ~(0xFFFu << 10)) | (newImm << 10))) ++patched;
    }
    return patched;
}

int patchMovImmediate(const MethodInfo* m, uint32_t oldValue, uint32_t newValue) {
    if (!m || newValue > 0xFFFF || oldValue > 0xFFFF) return 0;
    const auto start = reinterpret_cast<uintptr_t>(methodPointerOf(m));
    if (!start) return 0;
    const uintptr_t end = methodEnd(m, start);
    int patched = 0;
    for (auto* p = reinterpret_cast<uint32_t*>(start); reinterpret_cast<uintptr_t>(p) < end; ++p) {
        // movz wN, #imm16 (sf=0, hw=0)
        if ((*p & 0xFFE00000u) != 0x52800000u || ((*p >> 5) & 0xFFFFu) != oldValue) continue;
        if (writeInstruction(p, (*p & ~(0xFFFFu << 5)) | (newValue << 5))) ++patched;
    }
    return patched;
}

} // namespace bl::runtime
