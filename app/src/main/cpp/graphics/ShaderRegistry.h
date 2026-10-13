#pragma once
#include "graphics/GlApi.h"

#include <string>
#include <unordered_map>

namespace bl::graphics {

enum class ShaderState : int { Pending = 0, Ready = 1, Failed = -1 };

// Os uniforms padrao do preludio (ShaderLibrary), com o lugar ja procurado.
struct BuiltinUniforms {
    GLint box = -1, screen = -1, source = -1, screenTexel = -1, sourceTexel = -1, view = -1, resolution = -1;
    GLint aspect = -1, time = -1, age = -1, progress = -1, intensity = -1, center = -1, radius = -1, color = -1;
};

struct CompiledShader {
    GLuint program = 0;
    BuiltinUniforms builtin;
    std::unordered_map<std::string, GLint> custom;
};

/**
 * Os shaders dos efeitos de tela. O mod registra o fragment shader na thread
 * do jogo (bl.screen.shader) e recebe um id na hora; a compilacao fica para a
 * thread de render, no primeiro desenho, e o erro (em ingles, com a linha do
 * arquivo do mod) vai para o painel de erro e para ShaderStatus.
 */
class ShaderRegistry {
public:
    // Thread do jogo. O mesmo nome com o mesmo codigo devolve o mesmo id.
    static int Register(const std::string& name, const std::string& source);
    static ShaderState Status(int id, std::string* error = nullptr);

    // Thread de render: o programa pronto (compila na primeira vez), ou nullptr.
    static CompiledShader* Use(int id);
    static GLint Uniform(CompiledShader& shader, const std::string& name);
};

} // namespace bl::graphics
