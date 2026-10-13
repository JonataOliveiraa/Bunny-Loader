#pragma once
#include <string>
#include <vector>

namespace bl::graphics {

// Em que ponto do quadro o efeito desenha: depois do mundo (a interface fica
// por cima, intacta) ou no fim do quadro, por cima de tudo.
enum class EffectLayer : int { World = 0, Screen = 1 };

// Um uniform do mod: ate 4 floats.
struct UniformValue {
    std::string name;
    int count = 1;
    float v[4] = {0, 0, 0, 0};
};

/**
 * Um desenho de efeito, gravado na thread do jogo (bl.screen.submit) e
 * executado na de render. Coordenadas em fracao do viewport da camera
 * (0..1, v de baixo para cima); o raio em fracao da altura.
 */
struct EffectDraw {
    int shader = 0;
    EffectLayer layer = EffectLayer::World;
    float box[4] = {0, 0, 1, 1};   // x0, y0, x1, y1: o que e copiado e redesenhado
    float center[2] = {0.5f, 0.5f};
    float radius = 0.f;
    float intensity = 1.f;
    float progress = 0.f;
    float age = 0.f;               // segundos de vida do efeito
    float color[4] = {1, 1, 1, 1};
    std::vector<UniformValue> uniforms;
};

using EffectList = std::vector<EffectDraw>;

} // namespace bl::graphics
