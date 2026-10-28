#pragma once
#include <atomic>
#include <cstdint>

namespace bl::runtime::content {

enum class ItemDrawKind : int32_t { World = 0, Inventory = 1 };

struct ItemDrawWorld {
    uint32_t light;
    uint32_t alpha;
    float rotation;
    float scale;
};

struct ItemDrawInventory {
    void* item;
    void* batch;
    float position[2];
    int32_t frame[4];
    uint32_t light;
    uint32_t environment;
    float origin[2];
    float scale;
};

struct ItemDrawListener {
    const std::atomic<uint8_t>* active = nullptr;
    bool (*pre)(void* user, void* args) = nullptr;
    void (*post)(void* user) = nullptr;
    void* user = nullptr;
};

bool addItemDrawListener(ItemDrawKind kind, const ItemDrawListener& listener);

}
