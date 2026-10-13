#pragma once
#include <string>

namespace bl::graphics {

/**
 * O GLSL dos efeitos de tela: o vertex shader comum, o preludio que vai na
 * frente do fragment shader do mod (versao, uniforms padrao, blScreen) e a
 * biblioteca de `#include <bl/...>` (screen, math, noise, color, shapes, mask).
 *
 * O mod escreve so o fragment shader, em GLSL ES 3.00 (`fragColor`,
 * `texture`) ou no estilo 1.00 (`gl_FragColor`, `texture2D`, viram os de
 * 3.00 por #define).
 */
class ShaderLibrary {
public:
    static const char* Vertex();

    // O fragment shader completo. Cada trecho ganha um `#line 1 <n>`: o
    // codigo do mod e a fonte 0, o preludio a 1 e cada include 2, 3...; o
    // erro do driver ("0:12: ...") volta para o arquivo certo em Describe.
    // Include desconhecido: devolve vazio e o motivo em `error`.
    static std::string BuildFragment(const std::string& source, std::string& error);

    // O log do compilador com "fonte:linha" trocado pelo nome de cada trecho.
    static std::string Describe(const std::string& log, const std::string& name, const std::string& source);
};

} // namespace bl::graphics
