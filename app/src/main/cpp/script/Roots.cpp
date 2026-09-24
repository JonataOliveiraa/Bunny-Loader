#include "script/Roots.h"
#include "core/Log.h"
#include "il2cpp/Api.h"

#include <vector>

namespace bl::script {

namespace {

// Slots por bloco. Um bloco novo so e pedido quando todos os anteriores estao
// cheios; os blocos nunca sao devolvidos (o endereco de um slot tem de ser
// estavel enquanto estiver em uso) e 4096 ponteiros sao 32 KB.
constexpr uint32_t kChunk = 4096;

enum class Mode { Unknown, Slots, Handles };
Mode g_mode = Mode::Unknown;
bool g_barrier = false;

std::vector<void**> g_chunks;
std::vector<uint32_t> g_free;   // slots soltos, reusados antes de crescer
uint32_t g_next = 0;            // proximo slot nunca usado
uint32_t g_live = 0;

void decideMode() {
    auto& a = il2cpp::api();
    if (a.gc_alloc_fixed) {
        g_mode = Mode::Slots;
        // Com coletor incremental, gravar sem avisar pode passar despercebido
        // numa coleta ja em andamento — ai a barreira e obrigatoria. Sem ele
        // (o caso deste Terraria) ela nao faz nada e so custa uma chamada ao
        // runtime por objeto. Sem como perguntar, fica ligada.
        const bool incremental = !a.gc_is_incremental || a.gc_is_incremental();
        g_barrier = incremental && a.gc_wbarrier_set_field != nullptr;
        BL_INFO("raizes: tabela em memoria fixa do coletor (coletor %s, barreira %s)",
                incremental ? "incremental" : "nao incremental", g_barrier ? "sim" : "nao");
    } else {
        g_mode = Mode::Handles;
        BL_WARN("raizes: il2cpp_gc_alloc_fixed ausente; um gchandle por objeto");
    }
}

void** slotAt(uint32_t i) { return &g_chunks[i / kChunk][i % kChunk]; }

void store(void** slot, void* obj) {
    if (g_barrier) il2cpp::api().gc_wbarrier_set_field(nullptr, slot, obj);
    else *slot = obj;
}

} // namespace

uint32_t Roots::add(void* obj) {
    if (!obj) return 0;
    if (g_mode == Mode::Unknown) decideMode();
    auto& a = il2cpp::api();
    if (g_mode == Mode::Handles) {
        return a.gchandle_new ? a.gchandle_new(static_cast<Il2CppObject*>(obj), false) : 0;
    }

    uint32_t i;
    if (!g_free.empty()) {
        i = g_free.back();
        g_free.pop_back();
    } else {
        if (g_next == g_chunks.size() * kChunk) {
            auto* chunk = static_cast<void**>(a.gc_alloc_fixed(kChunk * sizeof(void*)));
            if (!chunk) {
                BL_ERROR("raizes: gc_alloc_fixed falhou; objeto sem ancora");
                return 0;
            }
            for (uint32_t k = 0; k < kChunk; ++k) chunk[k] = nullptr;
            g_chunks.push_back(chunk);
        }
        i = g_next++;
    }
    store(slotAt(i), obj);
    ++g_live;
    return i + 1;   // 0 fica reservado para "sem ancora"
}

void Roots::remove(uint32_t token) {
    if (!token) return;
    if (g_mode == Mode::Handles) {
        if (il2cpp::api().gchandle_free) il2cpp::api().gchandle_free(token);
        return;
    }
    const uint32_t i = token - 1;
    *slotAt(i) = nullptr;
    g_free.push_back(i);
    --g_live;
}

uint32_t Roots::live() { return g_live; }

} // namespace bl::script
