#include "graphics/ScreenApi.h"
#include "graphics/GpuTimer.h"
#include "graphics/RenderHook.h"
#include "graphics/ShaderRegistry.h"
#include "graphics/TextureRegistry.h"
#include "core/Log.h"

#include <algorithm>
#include <cstring>
#include <string>

namespace bl::graphics {

namespace {

class Reader {
public:
    Reader(const uint8_t* data, int32_t length) : data_(data), end_(data + (length > 0 ? length : 0)) {}

    bool ok() const { return ok_; }

    int32_t i32() {
        int32_t v = 0;
        take(&v, sizeof v);
        return v;
    }

    float f32() {
        float v = 0;
        take(&v, sizeof v);
        return v;
    }

    void floats(float* out, int count) {
        for (int i = 0; i < count; ++i) out[i] = f32();
    }

    std::string text() {
        const int32_t n = i32();
        if (n < 0 || n > end_ - data_) {
            ok_ = false;
            return {};
        }
        std::string s(reinterpret_cast<const char*>(data_), static_cast<size_t>(n));
        data_ += n;
        return s;
    }

private:
    void take(void* out, size_t size) {
        if (!ok_ || static_cast<size_t>(end_ - data_) < size) {
            ok_ = false;
            std::memset(out, 0, size);
            return;
        }
        std::memcpy(out, data_, size);
        data_ += size;
    }

    const uint8_t* data_;
    const uint8_t* end_;
    bool ok_ = true;
};

constexpr int32_t kMaxDraws = 256;
constexpr int32_t kMaxUniforms = 64;
constexpr int32_t kMaxUniformFloats = 256;

bool readFrame(Reader& in, EffectFrame& frame) {
    const int32_t draws = in.i32();
    const int32_t targets = in.i32();
    frame.history = in.i32() != 0;
    if (!in.ok() || draws < 0 || draws > kMaxDraws || targets < 0 || targets > kMaxDraws) return false;
    frame.draws.reserve(static_cast<size_t>(draws));
    for (int32_t i = 0; i < draws && in.ok(); ++i) {
        EffectDraw d;
        d.shader = in.i32();
        d.layer = in.i32() >= 1 ? EffectLayer::Screen : EffectLayer::World;
        d.dest = in.i32();
        d.source = in.i32();
        in.floats(d.box, 4);
        in.floats(d.center, 2);
        d.radius = in.f32();
        d.intensity = in.f32();
        d.progress = in.f32();
        d.age = in.f32();
        in.floats(d.color, 4);
        const int32_t uniforms = in.i32();
        const int32_t textures = in.i32();
        if (uniforms < 0 || uniforms > kMaxUniforms || textures < 0 || textures > kMaxUniforms) return false;
        for (int32_t k = 0; k < uniforms && in.ok(); ++k) {
            UniformValue u;
            u.name = in.text();
            u.components = in.i32();
            const int32_t count = in.i32();
            if (count < 0 || count > kMaxUniformFloats) return false;
            u.v.resize(static_cast<size_t>(count));
            in.floats(u.v.data(), count);
            if (u.components < 1 || u.components > 4) continue;
            u.v.resize(u.v.size() - u.v.size() % static_cast<size_t>(u.components));
            if (!u.v.empty() && !u.name.empty()) d.uniforms.push_back(std::move(u));
        }
        for (int32_t k = 0; k < textures && in.ok(); ++k) {
            TextureBinding t;
            t.name = in.text();
            t.texture = in.i32();
            if (t.texture != kTexNone && !t.name.empty()) d.textures.push_back(std::move(t));
        }
        if (d.shader > 0) frame.draws.push_back(std::move(d));
    }
    for (int32_t i = 0; i < targets && in.ok(); ++i) {
        TargetSpec t;
        t.id = in.i32();
        t.scale = in.f32();
        t.persistent = in.i32() != 0;
        t.name = in.text();
        if (t.id > 0 && t.scale > 0.f && t.scale <= 1.f) frame.targets.push_back(std::move(t));
    }
    return in.ok();
}

} // namespace

int32_t screenShader(const char* name, const char* source) {
    if (!source || !*source) return 0;
    return ShaderRegistry::Register(name ? name : "", source);
}

int32_t screenShaderStatus(int32_t id, char* error, int32_t capacity) {
    std::string text;
    const ShaderState state = ShaderRegistry::Status(id, &text);
    if (error && capacity > 0) {
        const size_t n = std::min(text.size(), static_cast<size_t>(capacity - 1));
        std::memcpy(error, text.data(), n);
        error[n] = '\0';
    }
    return static_cast<int32_t>(state);
}

int32_t screenSubmit(const uint8_t* data, int32_t length) {
    EffectFrame frame;
    if (data && length > 0) {
        Reader in(data, length);
        if (!readFrame(in, frame)) {
            BL_ERROR("screen effects: a frame of %d bytes could not be read; nothing drawn", length);
            frame = EffectFrame();
            submitEffects(std::move(frame));
            return 0;
        }
    }
    submitEffects(std::move(frame));
    return 1;
}

int32_t screenTexture(const char* path, int32_t linear, int32_t repeat, int32_t* width, int32_t* height) {
    if (!path || !*path) return 0;
    int w = 0, h = 0;
    const int id = TextureRegistry::Load(path, linear != 0, repeat != 0, &w, &h);
    if (width) *width = w;
    if (height) *height = h;
    return id;
}

int32_t screenSaveNeutralLut(const char* path, int32_t size) {
    if (!path || !*path) return 0;
    return TextureRegistry::SaveNeutralLut(path, size) ? 1 : 0;
}

int32_t screenStats(int64_t* out, int32_t capacity) {
    GpuTimer::Enable();
    const RenderStats s = renderStats();
    const int64_t values[] = {
        s.worldPostRender, s.uiPostRender, s.otherPostRender, s.draws, s.frames, s.markersIssued,
        s.markerHits, s.screenFrames, s.renderCpuNs, s.markerCpuNs, s.gpuNs, s.gpuSamples,
        s.gpuFrameNs, s.gpuFrameSamples, s.boxPixels, s.copyFallbacks,
        s.hookInstalled ? 1 : 0, s.hasContext ? 1 : 0, s.markerReady ? 1 : 0, s.gpuTimer ? 1 : 0,
    };
    const int32_t count = static_cast<int32_t>(sizeof values / sizeof values[0]);
    if (out) std::copy(values, values + std::min(count, capacity), out);
    return count;
}

} // namespace bl::graphics
