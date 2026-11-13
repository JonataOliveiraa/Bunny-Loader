#pragma once
#include <string>
#include <vector>

namespace bl::graphics {

enum class EffectLayer : int { World = 0, Screen = 1 };

constexpr int kTexNone = 0;
constexpr int kTexScreen = -1;
constexpr int kTexHistory = -2;

struct UniformValue {
    std::string name;
    int components = 1;
    std::vector<float> v;
};

struct TextureBinding {
    std::string name;
    int texture = kTexNone;
};

struct TargetSpec {
    int id = 0;
    std::string name;
    float scale = 1.f;
    bool persistent = false;
};

struct EffectDraw {
    int shader = 0;
    EffectLayer layer = EffectLayer::World;
    int dest = 0;
    int source = kTexNone;
    float box[4] = {0, 0, 1, 1};
    float center[2] = {0.5f, 0.5f};
    float radius = 0.f;
    float intensity = 1.f;
    float progress = 0.f;
    float age = 0.f;
    float color[4] = {1, 1, 1, 1};
    std::vector<UniformValue> uniforms;
    std::vector<TextureBinding> textures;
};

struct EffectFrame {
    std::vector<EffectDraw> draws;
    std::vector<TargetSpec> targets;
    bool history = false;

    bool empty() const { return draws.empty(); }
};

}
