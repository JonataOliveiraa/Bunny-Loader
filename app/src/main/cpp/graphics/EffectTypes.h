#pragma once
#include <string>
#include <vector>

namespace bl::graphics {

// Em que ponto do quadro o efeito desenha: depois do mundo (a interface fica
// por cima, intacta) ou no fim do quadro, por cima de tudo.
enum class EffectLayer : int { World = 0, Screen = 1 };

// Uma textura que um passo le ou escreve. > 0: um alvo do quadro (TargetSpec.id).
constexpr int kTexNone = 0;
constexpr int kTexScreen = -1;    // a copia do que ja esta desenhado (uScreen)
constexpr int kTexHistory = -2;   // o mundo do quadro anterior, ja com os efeitos

// Um uniform do mod: `components` floats por elemento; mais de um elemento
// em `v` e um array (uniform vec4 uAlgo[8]).
struct UniformValue {
    std::string name;
    int components = 1;
    std::vector<float> v;
};

// Um sampler2D do mod ligado a uma textura (kTex* ou um alvo).
struct TextureBinding {
    std::string name;
    int texture = kTexNone;
};

// Um alvo fora da tela: cobre o viewport inteiro na escala dada (0.5 = meia
// resolucao). O temporario e reaproveitado de quadro em quadro pelo nome e
// pode vir com lixo fora do que foi desenhado nele; o persistente guarda o
// conteudo.
struct TargetSpec {
    int id = 0;
    std::string name;
    float scale = 1.f;
    bool persistent = false;
};

/**
 * Um passo de efeito, gravado na thread do jogo (bl.screen.submit) e
 * executado na de render. Coordenadas em fracao do viewport da camera
 * (0..1, v de baixo para cima); o raio em fracao da altura.
 */
struct EffectDraw {
    int shader = 0;
    EffectLayer layer = EffectLayer::World;
    int dest = 0;                  // 0: a tela; > 0: um alvo
    int source = kTexNone;         // o uSource do shader
    float box[4] = {0, 0, 1, 1};   // x0, y0, x1, y1: onde desenha (e o que copia da tela)
    float center[2] = {0.5f, 0.5f};
    float radius = 0.f;
    float intensity = 1.f;
    float progress = 0.f;
    float age = 0.f;               // segundos de vida do efeito
    float color[4] = {1, 1, 1, 1};
    std::vector<UniformValue> uniforms;
    std::vector<TextureBinding> textures;
};

// O que um quadro manda para uma camada: os passos em ordem, os alvos que
// eles usam e se alguem pediu o quadro anterior (History).
struct EffectFrame {
    std::vector<EffectDraw> draws;
    std::vector<TargetSpec> targets;
    bool history = false;

    bool empty() const { return draws.empty(); }
};

} // namespace bl::graphics
