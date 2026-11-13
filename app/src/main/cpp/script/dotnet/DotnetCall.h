#pragma once
#include <cstdint>

namespace bl::dotnet {

struct CallFrame {
    intptr_t x[16];
    uint64_t d[8];
    void* indirect;
    int32_t ret;
    int32_t retSize;
    uint64_t out[4];
};

enum class RetKind : int32_t {
    Int = 0,
    F32 = 1,
    F64 = 2,
    S16 = 3,
    H1F = 4, H2F = 5, H3F = 6, H4F = 7,
    H1D = 8, H2D = 9, H3D = 10, H4D = 11,
    Indirect = 12,
};

constexpr int32_t kMaxIndirectReturn = 512;

int32_t callGame(void* fn, CallFrame* frame);

const char* lastCallError();

}
