#pragma once
#include <atomic>
#include <cstdint>

namespace bl::runtime {

struct BuffIconLook {
    std::atomic<uint8_t> active{0};
    void* texture = nullptr;
    void* newTexture = nullptr;
    float dx = 0, dy = 0, tx = 0, ty = 0;
    uint8_t hasSource = 0;
    int32_t source[4] = {};
    uint8_t hasColor = 0;
    uint32_t color = 0;
    int32_t applied = 0;
};

BuffIconLook* buffIconLook();

}
