#pragma once

#include <cstdint>

namespace bl::runtime {

struct ResourceDrawArgs {
    void* texture;
    float x, y;
    int32_t hasSource;
    int32_t source[4];
    uint32_t color;
    float rotation;
    float originX, originY;
    float scaleX, scaleY;
    int32_t effects;
};

using ResourceDrawCallback = int32_t (*)(int32_t phase, void* spriteBatch, ResourceDrawArgs* args);

bool setResourceDrawCallback(ResourceDrawCallback callback);
void setResourceDrawActive(bool active);
void* assetOfTexture(void* texture);

}
