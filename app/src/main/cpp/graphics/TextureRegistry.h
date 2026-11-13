#pragma once
#include "graphics/GlApi.h"

#include <string>

namespace bl::graphics {

constexpr int kFirstModTexture = -1000;

struct LoadedTexture {
    GLuint texture = 0;
    GLuint sampler = 0;
    GLint width = 0, height = 0;
};

class TextureRegistry {
public:
    static int Load(const std::string& path, bool linear, bool repeat, int* width, int* height);

    static const LoadedTexture* Use(int id);

    static bool SaveNeutralLut(const std::string& path, int size);
};

}
