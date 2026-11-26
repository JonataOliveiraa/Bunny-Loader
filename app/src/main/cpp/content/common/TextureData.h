#pragma once
#include <cstddef>
#include <cstdint>

#include "il2cpp/Types.h"

namespace bl::runtime::content {

enum class PixelOrder : int32_t {
    Rgba = 0,
    Abgr = 1,
};

enum class TextureDataResult : int32_t {
    Ok = 0,
    NoTexture = 1,
    NotOwned = 2,
    NotReadable = 3,
    BadRegion = 4,
    UnsupportedFormat = 5,
    Failed = 6,
};

Il2CppObject* createTexture(int width, int height);
Il2CppObject* textureFromPng(const uint8_t* data, size_t size);

TextureDataResult writeTexture(Il2CppObject* texture, int x, int y, int width, int height, const uint8_t* pixels, PixelOrder order);
TextureDataResult readTexture(Il2CppObject* texture, int x, int y, int width, int height, uint8_t* pixels, PixelOrder order);

}
