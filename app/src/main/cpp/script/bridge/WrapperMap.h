#pragma once
#include "quickjs.h"

#include <cstddef>
#include <cstdint>
#include <vector>

namespace bl::script {

/**
 * Ponteiro do jogo -> wrapper JS, sem alocacao por entrada.
 *
 * Era um std::unordered_map: um no alocado a cada wrapper novo e liberado a
 * cada wrapper morto — e wrapper novo e o caso de `self` de hook e de elemento
 * de array que o JS nao guarda (docs/historico/PONTE-OTIMIZACAO.md, passo 5c).
 *
 * Enderecamento aberto com sondagem linear e remocao por deslocamento para
 * tras (sem lapides): a busca para no primeiro balde vazio, entao remover
 * precisa puxar de volta quem estava "empurrado" para depois do buraco. Chave
 * nula e o balde vazio — objeto do jogo nunca tem endereco 0.
 *
 * Nao e thread-safe: so com o motor travado (JsLock), como o resto da ponte.
 */
class WrapperMap {
public:
    /** O wrapper de `key`, ou nullptr. O ponteiro vale ate o proximo insert. */
    JSValue* find(void* key) {
        if (count_ == 0) return nullptr;
        for (size_t i = home(key);; i = (i + 1) & mask_) {
            if (slots_[i].key == key) return &slots_[i].val;
            if (!slots_[i].key) return nullptr;
        }
    }

    /** `key` nao pode estar no mapa. */
    void insert(void* key, JSValue val) {
        if ((count_ + 1) * 2 > slots_.size()) grow();
        size_t i = home(key);
        while (slots_[i].key) i = (i + 1) & mask_;
        slots_[i].key = key;
        slots_[i].val = val;
        ++count_;
    }

    /** Tira `key`, se estiver. */
    void erase(void* key) {
        if (count_ == 0) return;
        size_t i = home(key);
        while (slots_[i].key != key) {
            if (!slots_[i].key) return;
            i = (i + 1) & mask_;
        }
        // Deslocamento para tras: cada entrada seguinte do mesmo aglomerado
        // que NAO esteja no seu lugar ideal entre o buraco e ela volta para o
        // buraco, e o buraco anda.
        size_t hole = i;
        for (size_t j = (i + 1) & mask_; slots_[j].key; j = (j + 1) & mask_) {
            const size_t ideal = home(slots_[j].key);
            // `ideal` esta ciclicamente em (hole, j]? Entao fica onde esta.
            const bool staysPut = hole <= j ? (ideal > hole && ideal <= j)
                                            : (ideal > hole || ideal <= j);
            if (!staysPut) {
                slots_[hole] = slots_[j];
                hole = j;
            }
        }
        slots_[hole].key = nullptr;
        --count_;
    }

private:
    struct Slot {
        void* key = nullptr;
        JSValue val = JS_UNDEFINED;
    };

    size_t home(void* key) const {
        // Objetos do IL2CPP sao alinhados: os bits baixos sao sempre zero.
        // Mistura para nao aglomerar enderecos vizinhos.
        uint64_t x = reinterpret_cast<uintptr_t>(key) >> 3;
        x ^= x >> 33;
        x *= 0xff51afd7ed558ccdULL;
        x ^= x >> 33;
        return static_cast<size_t>(x) & mask_;
    }

    void grow() {
        std::vector<Slot> old;
        old.swap(slots_);
        slots_.assign(old.empty() ? 1024 : old.size() * 2, Slot{});
        mask_ = slots_.size() - 1;
        count_ = 0;
        for (const Slot& s : old) {
            if (s.key) insert(s.key, s.val);
        }
    }

    std::vector<Slot> slots_;
    size_t mask_ = 0;
    size_t count_ = 0;
};

} // namespace bl::script
