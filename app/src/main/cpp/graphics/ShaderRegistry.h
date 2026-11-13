#pragma once
#include "graphics/GlApi.h"

#include <string>
#include <unordered_map>

namespace bl::graphics {

enum class ShaderState : int { Pending = 0, Ready = 1, Failed = -1 };

struct BuiltinUniforms {
    GLint box = -1, screen = -1, source = -1, screenTexel = -1, sourceTexel = -1, view = -1, resolution = -1;
    GLint aspect = -1, time = -1, age = -1, progress = -1, intensity = -1, center = -1, radius = -1, color = -1;
};

struct CompiledShader {
    GLuint program = 0;
    BuiltinUniforms builtin;
    std::unordered_map<std::string, GLint> custom;
};

class ShaderRegistry {
public:
    static int Register(const std::string& name, const std::string& source);
    static ShaderState Status(int id, std::string* error = nullptr);

    static CompiledShader* Use(int id);
    static GLint Uniform(CompiledShader& shader, const std::string& name);
};

}
