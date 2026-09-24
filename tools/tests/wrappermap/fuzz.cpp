// Fuzz do WrapperMap (script/WrapperMap.h) contra std::unordered_map.
//
// Compila para Android x86_64 (o MuMu roda nativo) e roda por adb:
//   tools/tests/wrappermap/run.sh
//
// Chaves de um conjunto pequeno de enderecos alinhados, para forcar colisao,
// aglomerado e a volta do fim da tabela para o comeco — onde a remocao por
// deslocamento para tras costuma errar.
#include "script/WrapperMap.h"

#include <cstdio>
#include <random>
#include <unordered_map>

using bl::script::WrapperMap;

int main() {
    std::mt19937_64 rng(12345);
    for (int keySpace : {64, 700, 5000}) {
        WrapperMap map;
        std::unordered_map<void*, int32_t> ref;
        const long ops = 3000000;
        for (long op = 0; op < ops; ++op) {
            void* key = reinterpret_cast<void*>(0x10000 + 16 * (rng() % keySpace));
            const int kind = static_cast<int>(rng() % 3);
            if (kind == 0 && !ref.count(key)) {
                const int32_t v = static_cast<int32_t>(rng());
                map.insert(key, JS_MKVAL(JS_TAG_INT, v));
                ref[key] = v;
            } else if (kind == 1) {
                map.erase(key);
                ref.erase(key);
            }
            // Confere a chave sorteada e, de vez em quando, todas.
            JSValue* got = map.find(key);
            auto it = ref.find(key);
            if ((got != nullptr) != (it != ref.end()) ||
                (got && JS_VALUE_GET_INT(*got) != it->second)) {
                std::printf("FALHOU: espaco=%d op=%ld chave=%p\n", keySpace, op, key);
                return 1;
            }
            if (op % 50000 == 0) {
                for (int k = 0; k < keySpace; ++k) {
                    void* kk = reinterpret_cast<void*>(0x10000 + 16 * k);
                    JSValue* g = map.find(kk);
                    auto r = ref.find(kk);
                    if ((g != nullptr) != (r != ref.end()) ||
                        (g && JS_VALUE_GET_INT(*g) != r->second)) {
                        std::printf("FALHOU (varredura): espaco=%d op=%ld\n", keySpace, op);
                        return 1;
                    }
                }
            }
        }
        std::printf("ok: espaco=%d, %ld operacoes, %zu no fim\n", keySpace, ops, ref.size());
    }
    std::printf("FUZZ OK\n");
    return 0;
}
