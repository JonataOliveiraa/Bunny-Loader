#include "graphics/ShaderLibrary.h"

#include <cstdio>
#include <cstring>
#include <sstream>
#include <vector>

namespace bl::graphics {

namespace {

// vUV: a posicao do fragmento em fracao do viewport da camera. O quad cobre
// so a caixa do passo (uBox, na mesma fracao) e o viewport e ela. Toda
// textura dos efeitos (a copia da tela, o quadro anterior, os alvos) cobre o
// viewport inteiro: `texture(uAlgo, vUV)` le o mesmo lugar em qualquer uma.
const char* kVertex = R"(#version 300 es
layout(location = 0) in vec2 aPos;
uniform vec4 uBox;
out vec2 vUV;
void main() {
    vUV = mix(uBox.xy, uBox.zw, aPos * 0.5 + 0.5);
    gl_Position = vec4(aPos, 0.0, 1.0);
}
)";

const char* kVersion = "#version 300 es\n";

const char* kPrelude = R"(precision highp float;
precision highp int;
in vec2 vUV;
out vec4 fragColor;
#define gl_FragColor fragColor
#define texture2D texture
uniform sampler2D uScreen;
uniform sampler2D uSource;
uniform vec2 uScreenTexel;
uniform vec2 uSourceTexel;
uniform vec4 uView;
uniform vec2 uResolution;
uniform float uAspect;
uniform float uTime;
uniform float uAge;
uniform float uProgress;
uniform float uIntensity;
uniform vec2 uCenter;
uniform float uRadius;
uniform vec4 uColor;
vec4 blScreen(vec2 uv) {
    return texture(uScreen, uv);
}
vec4 blSource(vec2 uv) {
    return texture(uSource, uv);
}
)";

struct Include {
    const char* name;
    const char* text;
};

const Include kIncludes[] = {
    {"screen", R"(vec2 blFromCenter(vec2 uv) {
    vec2 d = uv - uCenter;
    d.x *= uAspect;
    return d;
}
vec2 blToUV(vec2 d) {
    return vec2(d.x / uAspect, d.y);
}
vec2 blAtCenter(vec2 d) {
    return uCenter + blToUV(d);
}
float blDistance(vec2 uv) {
    return length(blFromCenter(uv));
}
)"},
    {"math", R"(const float BL_PI = 3.14159265359;
const float BL_TAU = 6.28318530718;
vec2 blRotate(vec2 v, float a) {
    float c = cos(a), s = sin(a);
    return vec2(v.x * c - v.y * s, v.x * s + v.y * c);
}
float blRemap(float v, float a, float b, float c, float d) {
    return c + (v - a) * (d - c) / (b - a);
}
float blSaturate(float v) {
    return clamp(v, 0.0, 1.0);
}
)"},
    {"noise", R"(float blHash(vec2 p) {
    p = fract(p * vec2(123.34, 456.21));
    p += dot(p, p + 45.32);
    return fract(p.x * p.y);
}
float blValueNoise(vec2 p) {
    vec2 i = floor(p), f = fract(p);
    vec2 u = f * f * (3.0 - 2.0 * f);
    return mix(mix(blHash(i), blHash(i + vec2(1.0, 0.0)), u.x),
               mix(blHash(i + vec2(0.0, 1.0)), blHash(i + vec2(1.0, 1.0)), u.x), u.y);
}
vec2 blGradient(vec2 p) {
    float a = blHash(p) * 6.28318530718;
    return vec2(cos(a), sin(a));
}
float blNoise(vec2 p) {
    vec2 i = floor(p), f = fract(p);
    vec2 u = f * f * (3.0 - 2.0 * f);
    float a = dot(blGradient(i), f);
    float b = dot(blGradient(i + vec2(1.0, 0.0)), f - vec2(1.0, 0.0));
    float c = dot(blGradient(i + vec2(0.0, 1.0)), f - vec2(0.0, 1.0));
    float d = dot(blGradient(i + vec2(1.0, 1.0)), f - vec2(1.0, 1.0));
    return 0.5 + mix(mix(a, b, u.x), mix(c, d, u.x), u.y);
}
float blFbm(vec2 p, int octaves) {
    float sum = 0.0, amp = 0.5;
    for (int i = 0; i < 8; i++) {
        if (i >= octaves) break;
        sum += amp * blNoise(p);
        p *= 2.0;
        amp *= 0.5;
    }
    return sum;
}
)"},
    {"color", R"(const int BL_BLEND_NORMAL = 0;
const int BL_BLEND_MULTIPLY = 1;
const int BL_BLEND_SCREEN = 2;
const int BL_BLEND_OVERLAY = 3;
const int BL_BLEND_SOFT_LIGHT = 4;
const int BL_BLEND_ADD = 5;
const int BL_BLEND_COLOR = 6;
const int BL_BLEND_LUMINOSITY = 7;
float blLuma(vec3 c) {
    return dot(c, vec3(0.2126, 0.7152, 0.0722));
}
vec3 blRgbToHsv(vec3 c) {
    vec4 k = vec4(0.0, -1.0 / 3.0, 2.0 / 3.0, -1.0);
    vec4 p = mix(vec4(c.bg, k.wz), vec4(c.gb, k.xy), step(c.b, c.g));
    vec4 q = mix(vec4(p.xyw, c.r), vec4(c.r, p.yzx), step(p.x, c.r));
    float d = q.x - min(q.w, q.y);
    return vec3(abs(q.z + (q.w - q.y) / (6.0 * d + 1e-10)), d / (q.x + 1e-10), q.x);
}
vec3 blHsvToRgb(vec3 c) {
    vec3 p = abs(fract(c.xxx + vec3(1.0, 2.0 / 3.0, 1.0 / 3.0)) * 6.0 - 3.0);
    return c.z * mix(vec3(1.0), clamp(p - 1.0, 0.0, 1.0), c.y);
}
vec3 blSetLuma(vec3 c, float l) {
    vec3 r = c + (l - blLuma(c));
    float lum = blLuma(r), lo = min(min(r.r, r.g), r.b), hi = max(max(r.r, r.g), r.b);
    if (lo < 0.0) r = lum + (r - lum) * lum / (lum - lo + 1e-5);
    if (hi > 1.0) r = lum + (r - lum) * (1.0 - lum) / (hi - lum + 1e-5);
    return r;
}
vec3 blBlend(vec3 base, vec3 top, int mode) {
    if (mode == BL_BLEND_MULTIPLY) return base * top;
    if (mode == BL_BLEND_SCREEN) return 1.0 - (1.0 - base) * (1.0 - top);
    if (mode == BL_BLEND_OVERLAY) return mix(2.0 * base * top, 1.0 - 2.0 * (1.0 - base) * (1.0 - top), step(0.5, base));
    if (mode == BL_BLEND_SOFT_LIGHT) return mix(base - (1.0 - 2.0 * top) * base * (1.0 - base),
                                                base + (2.0 * top - 1.0) * (sqrt(base) - base), step(0.5, top));
    if (mode == BL_BLEND_ADD) return min(base + top, 1.0);
    if (mode == BL_BLEND_COLOR) return blSetLuma(top, blLuma(base));
    if (mode == BL_BLEND_LUMINOSITY) return blSetLuma(base, blLuma(top));
    return top;
}
vec3 blAdjust(vec3 c, float saturation, float contrast, float brightness, float gamma, float hue, float temperature) {
    c = (c + brightness - 0.5) * contrast + 0.5;
    c = mix(vec3(blLuma(c)), c, saturation);
    if (hue != 0.0) {
        vec3 hsv = blRgbToHsv(clamp(c, 0.0, 1.0));
        hsv.x = fract(hsv.x + hue);
        c = blHsvToRgb(hsv);
    }
    c *= vec3(1.0 + 0.2 * temperature, 1.0 + 0.02 * temperature, 1.0 - 0.2 * temperature);
    c = pow(max(c, 0.0), vec3(1.0 / max(gamma, 1e-3)));
    return clamp(c, 0.0, 1.0);
}
vec3 blLut(sampler2D lut, vec3 c, float size) {
    c = clamp(c, 0.0, 1.0);
    float slice = c.b * (size - 1.0);
    float s0 = floor(slice);
    float s1 = min(s0 + 1.0, size - 1.0);
    float x = (c.r * (size - 1.0) + 0.5) / (size * size);
    float y = 1.0 - (c.g * (size - 1.0) + 0.5) / size;
    vec3 a = texture(lut, vec2(x + s0 / size, y)).rgb;
    vec3 b = texture(lut, vec2(x + s1 / size, y)).rgb;
    return mix(a, b, slice - s0);
}
)"},
    {"mask", R"(uniform float uMaskType;
uniform vec4 uMaskA;
uniform vec4 uMaskB;
uniform sampler2D uMaskTexture;
float blMaskEdge(float t, float falloff) {
    t = clamp(t, 0.0, 1.0);
    if (falloff > 1.5) return exp(-5.0 * t) * (1.0 - t);
    if (falloff > 0.5) return 1.0 - smoothstep(0.0, 1.0, t);
    return 1.0 - t;
}
float blMask(vec2 uv) {
    int type = int(uMaskType + 0.5);
    float m = 1.0;
    if (type == 1) {
        vec2 d = uv - uMaskA.xy;
        d.x *= uAspect;
        float inner = uMaskA.z - uMaskA.w;
        float r = length(d);
        m = r <= inner ? 1.0 : blMaskEdge((r - inner) / max(uMaskA.w, 1e-5), uMaskB.x);
    } else if (type == 2) {
        vec2 d = uv - uMaskA.xy;
        d.x *= uAspect;
        float dist = abs(length(d) - uMaskA.z) - uMaskA.w * 0.5;
        m = dist <= 0.0 ? 1.0 : blMaskEdge(dist / max(uMaskB.z, 1e-5), uMaskB.x);
    } else if (type == 3) {
        vec2 outside = max(max(uMaskA.xy - uv, uv - uMaskA.zw), 0.0);
        outside.x *= uAspect;
        float dist = length(outside);
        m = dist <= 0.0 ? 1.0 : blMaskEdge(dist / max(uMaskB.z, 1e-5), uMaskB.x);
    } else if (type == 4) {
        float t = dot(uv - 0.5, uMaskA.xy) + 0.5;
        m = blMaskEdge((t - uMaskA.z) / max(uMaskA.w - uMaskA.z, 1e-5), uMaskB.x);
    } else if (type == 5) {
        vec2 scale = vec2(mix(1.0, uAspect, uMaskA.y), 1.0);
        float r = length((uv - 0.5) * 2.0 * scale) / length(scale);
        m = smoothstep(uMaskA.x, uMaskA.x + max(uMaskA.z, 1e-4), r);
    } else if (type == 6) {
        vec4 c = texture(uMaskTexture, uv);
        m = uMaskB.z > 0.5 ? dot(c.rgb, vec3(0.2126, 0.7152, 0.0722)) : c.a;
    }
    if (uMaskB.y > 0.5) m = 1.0 - m;
    return m;
}
)"},
    {"shapes", R"(float blCircle(float distance, float radius, float feather) {
    return 1.0 - smoothstep(radius - feather, radius, distance);
}
float blRing(float distance, float radius, float width, float feather) {
    return blCircle(abs(distance - radius), width * 0.5, feather);
}
float blFalloff(float distance, float radius) {
    float f = 1.0 - clamp(distance / max(radius, 1e-5), 0.0, 1.0);
    return f * f;
}
)"},
};

const Include* findInclude(const std::string& name) {
    for (const Include& inc : kIncludes) {
        if (name == inc.name) return &inc;
    }
    return nullptr;
}

// `#include <bl/nome>` na linha (com espacos em volta): o nome, ou vazio.
bool parseInclude(const std::string& line, std::string& name) {
    size_t i = line.find_first_not_of(" \t");
    if (i == std::string::npos || line.compare(i, 8, "#include") != 0) return false;
    const size_t open = line.find('<', i);
    const size_t close = line.find('>', open == std::string::npos ? i : open);
    if (open == std::string::npos || close == std::string::npos) {
        name.clear();
        return true;
    }
    name = line.substr(open + 1, close - open - 1);
    if (name.compare(0, 3, "bl/") == 0) name = name.substr(3);
    return true;
}

// Os nomes dos includes na ordem em que aparecem (a mesma numeracao de fonte
// do BuildFragment), sem repetir.
std::vector<std::string> includesOf(const std::string& source) {
    std::vector<std::string> names;
    std::istringstream in(source);
    std::string line, name;
    while (std::getline(in, line)) {
        if (!parseInclude(line, name) || name.empty()) continue;
        bool seen = false;
        for (const auto& n : names) seen = seen || n == name;
        if (!seen) names.push_back(name);
    }
    return names;
}

} // namespace

const char* ShaderLibrary::Vertex() {
    return kVertex;
}

std::string ShaderLibrary::BuildFragment(const std::string& source, std::string& error) {
    const std::vector<std::string> includes = includesOf(source);
    std::string out = kVersion;
    out += "#line 1 1\n";
    out += kPrelude;

    std::vector<bool> emitted(includes.size(), false);
    std::istringstream in(source);
    std::string line, name;
    int number = 0;
    bool started = false;
    while (std::getline(in, line)) {
        ++number;
        if (!line.empty() && line.back() == '\r') line.pop_back();
        // O #version do mod sai: o nosso ja esta no topo.
        if (line.compare(0, 8, "#version") == 0) continue;
        if (parseInclude(line, name)) {
            const Include* inc = findInclude(name);
            if (!inc) {
                error = "line " + std::to_string(number) + ": unknown include '" + line + "' (available: bl/screen, bl/math, bl/noise, bl/color, bl/shapes, bl/mask)";
                return {};
            }
            for (size_t k = 0; k < includes.size(); ++k) {
                if (includes[k] != name || emitted[k]) continue;
                emitted[k] = true;
                out += "#line 1 " + std::to_string(k + 2) + "\n";
                out += inc->text;
            }
            started = false;
            continue;
        }
        if (!started) {
            out += "#line " + std::to_string(number) + " 0\n";
            started = true;
        }
        out += line;
        out += '\n';
    }
    return out;
}

std::string ShaderLibrary::Describe(const std::string& log, const std::string& name, const std::string& source) {
    const std::vector<std::string> includes = includesOf(source);
    std::string out;
    std::istringstream in(log);
    std::string line;
    while (std::getline(in, line)) {
        // "ERROR: 0:12: ..." ou "0:12(5): ..." (o formato muda por driver)
        int sourceNumber = -1, lineNumber = -1, consumed = 0;
        const size_t colon = line.find(':');
        const char* at = line.c_str();
        if (colon != std::string::npos && line.compare(0, 6, "ERROR:") == 0) at = line.c_str() + 7;
        if (std::sscanf(at, "%d:%d%n", &sourceNumber, &lineNumber, &consumed) == 2) {
            std::string where = name;
            if (sourceNumber == 1) where = "prelude";
            else if (sourceNumber >= 2 && sourceNumber - 2 < static_cast<int>(includes.size())) where = "bl/" + includes[sourceNumber - 2];
            out += where + ":" + std::to_string(lineNumber) + (at + consumed);
        } else {
            out += line;
        }
        out += '\n';
    }
    return out;
}

} // namespace bl::graphics
