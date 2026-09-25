#include "runtime/CodePatch.h"
#include "core/Log.h"
#include "il2cpp/Api.h"

#include <sys/mman.h>
#include <unistd.h>

namespace bl::runtime {

namespace {

constexpr uintptr_t kMaxMethodBytes = 512 * 1024;

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

} // namespace bl::runtime
