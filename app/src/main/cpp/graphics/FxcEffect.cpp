#include "graphics/FxcEffect.h"

#include "mojoshader.h"

#include <algorithm>
#include <cstdio>
#include <cstring>
#include <memory>
#include <mutex>
#include <sstream>
#include <string>
#include <vector>

namespace bl::graphics {

namespace {

constexpr int kFloatRegisters = 256;
constexpr int kIntRegisters = 16;
constexpr int kBoolRegisters = 16;
constexpr const char* kProfile = MOJOSHADER_PROFILE_GLSLES3;

struct Registers {
    float vf[kFloatRegisters * 4] = {};
    int vi[kIntRegisters * 4] = {};
    unsigned char vb[kBoolRegisters] = {};
    float pf[kFloatRegisters * 4] = {};
    int pi[kIntRegisters * 4] = {};
    unsigned char pb[kBoolRegisters] = {};
};

struct Entry {
    MOJOSHADER_effect* effect = nullptr;
    MOJOSHADER_effectStateChanges changes{};
    Registers registers;
};

std::mutex g_mutex;
std::vector<std::unique_ptr<Entry>> g_entries;

void* MOJOSHADERCALL compileShader(const void*, const char* mainfn, const unsigned char* tokens, const unsigned int size,
                                   const MOJOSHADER_swizzle* swiz, const unsigned int swizcount,
                                   const MOJOSHADER_samplerMap* smap, const unsigned int smapcount) {
    return const_cast<MOJOSHADER_parseData*>(
        MOJOSHADER_parse(kProfile, mainfn, tokens, size, swiz, swizcount, smap, smapcount, nullptr, nullptr, nullptr));
}

void MOJOSHADERCALL addRef(void*) {}

void MOJOSHADERCALL deleteShader(const void*, void* shader) {
    MOJOSHADER_freeParseData(static_cast<const MOJOSHADER_parseData*>(shader));
}

MOJOSHADER_parseData* MOJOSHADERCALL parseData(void* shader) {
    return static_cast<MOJOSHADER_parseData*>(shader);
}

void MOJOSHADERCALL bindShaders(const void*, void*, void*) {}

void MOJOSHADERCALL boundShaders(const void*, void** vertex, void** pixel) {
    *vertex = nullptr;
    *pixel = nullptr;
}

void MOJOSHADERCALL mapRegisters(const void* context, float** vsf, int** vsi, unsigned char** vsb, float** psf, int** psi,
                                 unsigned char** psb) {
    auto* r = const_cast<Registers*>(static_cast<const Registers*>(context));
    *vsf = r->vf;
    *vsi = r->vi;
    *vsb = r->vb;
    *psf = r->pf;
    *psi = r->pi;
    *psb = r->pb;
}

void MOJOSHADERCALL unmapRegisters(const void*) {}

const char* MOJOSHADERCALL lastError(const void*) {
    return "";
}

Entry* find(int32_t id) {
    if (id <= 0 || id > static_cast<int32_t>(g_entries.size())) return nullptr;
    Entry* entry = g_entries[static_cast<size_t>(id - 1)].get();
    return entry && entry->effect ? entry : nullptr;
}

int32_t copyOut(const std::string& text, char* out, int32_t capacity) {
    if (out && capacity > 0) {
        const size_t n = std::min(text.size(), static_cast<size_t>(capacity - 1));
        std::memcpy(out, text.data(), n);
        out[n] = '\0';
    }
    return static_cast<int32_t>(text.size() + 1);
}

const MOJOSHADER_effectPass* passOf(const MOJOSHADER_effect* e, int32_t technique, int32_t pass) {
    if (technique < 0 || technique >= e->technique_count) return nullptr;
    const MOJOSHADER_effectTechnique& t = e->techniques[technique];
    if (pass < 0 || pass >= static_cast<int32_t>(t.pass_count)) return nullptr;
    return &t.passes[pass];
}

const MOJOSHADER_effectShader* pixelShaderOf(const MOJOSHADER_effect* e, const MOJOSHADER_effectPass& pass) {
    for (unsigned i = 0; i < pass.state_count; ++i) {
        const MOJOSHADER_effectState& s = pass.states[i];
        if (s.type != MOJOSHADER_RS_PIXELSHADER || !s.value.valuesI) continue;
        const int object = s.value.valuesI[0];
        if (object < 0 || object >= e->object_count) return nullptr;
        return &e->objects[object].shader;
    }
    return nullptr;
}

int floatRegisterCount(const MOJOSHADER_parseData* pd) {
    int count = 0;
    for (int i = 0; i < pd->uniform_count; ++i) {
        const MOJOSHADER_uniform& u = pd->uniforms[i];
        if (u.type != MOJOSHADER_UNIFORM_FLOAT) continue;
        count = std::max(count, u.index + std::max(u.array_count, 1));
    }
    return count;
}

bool startsWith(const std::string& s, const char* prefix) {
    return s.compare(0, std::strlen(prefix), prefix) == 0;
}

std::string lastWord(const std::string& line) {
    std::string s = line;
    while (!s.empty() && (s.back() == ';' || s.back() == ' ' || s.back() == '\r')) s.pop_back();
    const size_t space = s.find_last_of(' ');
    return space == std::string::npos ? s : s.substr(space + 1);
}

std::string inputValue(const std::string& name, bool sprite) {
    int usage = -1, index = 0;
    if (std::sscanf(name.c_str(), "io_%d_%d", &usage, &index) != 2) return "vec4(0.0)";
    if (usage == MOJOSHADER_USAGE_TEXCOORD) {
        return sprite ? "vec4(BL_SPRITE_UV.x, 1.0 - BL_SPRITE_UV.y, 0.0, 0.0)" : "vec4(vUV.x, 1.0 - vUV.y, 0.0, 0.0)";
    }
    if (usage == MOJOSHADER_USAGE_COLOR) return sprite ? "BL_SPRITE_COLOR" : "vec4(1.0)";
    return "vec4(0.0)";
}

const char* kHelpers = R"(#undef texture2D
#define texture2D blFxcTexture
#define texture2DLod blFxcTextureLod
#define texture2DProj blFxcTextureProj
#define texture2DGrad blFxcTextureGrad
#define texture3D texture
#define texture3DLod textureLod
#define textureCube texture
#define textureCubeLod textureLod
vec4 blFxcTexture(sampler2D s, vec2 uv) {
    return texture(s, vec2(uv.x, 1.0 - uv.y));
}
vec4 blFxcTexture(sampler2D s, vec2 uv, float bias) {
    return texture(s, vec2(uv.x, 1.0 - uv.y), bias);
}
vec4 blFxcTextureLod(sampler2D s, vec2 uv, float lod) {
    return textureLod(s, vec2(uv.x, 1.0 - uv.y), lod);
}
vec4 blFxcTextureProj(sampler2D s, vec4 uv) {
    vec2 p = uv.xy / uv.w;
    return texture(s, vec2(p.x, 1.0 - p.y));
}
vec4 blFxcTextureProj(sampler2D s, vec3 uv) {
    vec2 p = uv.xy / uv.z;
    return texture(s, vec2(p.x, 1.0 - p.y));
}
vec4 blFxcTextureGrad(sampler2D s, vec2 uv, vec2 dx, vec2 dy) {
    return textureGrad(s, vec2(uv.x, 1.0 - uv.y), vec2(dx.x, -dx.y), vec2(dy.x, -dy.y));
}
)";

bool convert(const MOJOSHADER_parseData* pd, std::string& out, std::string& error, bool sprite = false) {
    std::istringstream in(pd->output ? pd->output : "");
    std::ostringstream body;
    std::vector<std::string> inputs;
    std::string line;
    bool hasMain = false;
    body << kHelpers;
    while (std::getline(in, line)) {
        if (!line.empty() && line.back() == '\r') line.pop_back();
        if (startsWith(line, "#version") || startsWith(line, "#extension") || startsWith(line, "precision ")) continue;
        if (startsWith(line, "#define texture")) continue;
        if (!sprite && startsWith(line, "uniform sampler2D ps_s0;")) {
            body << "#define ps_s0 uScreen\n";
            continue;
        }
        if (startsWith(line, "in ")) {
            const std::string name = lastWord(line);
            inputs.push_back(name);
            body << "vec4 " << name << ";\n";
            continue;
        }
        if (startsWith(line, "layout(location = ") && line.find(" out ") != std::string::npos) {
            const std::string name = lastWord(line);
            if (startsWith(line, "layout(location = 0)")) body << "#define " << name << " fragColor\n";
            else body << "vec4 " << name << ";\n";
            continue;
        }
        if (line == "void main()") {
            body << "void blFxcMain()\n";
            hasMain = true;
            continue;
        }
        body << line << '\n';
    }
    if (!hasMain) {
        error = "the converted pixel shader has no main()";
        return false;
    }
    body << "void main() {\n";
    for (const std::string& name : inputs) body << "    " << name << " = " << inputValue(name, sprite) << ";\n";
    body << "    blFxcMain();\n}\n";
    out = body.str();
    return true;
}

}

int32_t fxcLoad(const uint8_t* data, int32_t length, char* error, int32_t capacity) {
    if (!data || length <= 0) {
        copyOut("empty effect", error, capacity);
        return 0;
    }
    uint32_t token = 0;
    if (length >= 8) std::memcpy(&token, data, sizeof token);
    if (token != 0xFEFF0901u && token != 0xBCF00BCFu) {
        copyOut("not a compiled effect (expected fxc /T fx_2_0 output)", error, capacity);
        return 0;
    }
    auto entry = std::make_unique<Entry>();
    MOJOSHADER_effectShaderContext ctx{};
    ctx.compileShader = compileShader;
    ctx.shaderAddRef = addRef;
    ctx.deleteShader = deleteShader;
    ctx.getParseData = parseData;
    ctx.bindShaders = bindShaders;
    ctx.getBoundShaders = boundShaders;
    ctx.mapUniformBufferMemory = mapRegisters;
    ctx.unmapUniformBufferMemory = unmapRegisters;
    ctx.getError = lastError;
    ctx.shaderContext = &entry->registers;
    MOJOSHADER_effect* e = MOJOSHADER_compileEffect(data, static_cast<unsigned>(length), nullptr, 0, nullptr, 0, &ctx);
    if (!e) {
        copyOut("MojoShader could not read the effect", error, capacity);
        return 0;
    }
    if (e->error_count > 0) {
        std::string text;
        for (int i = 0; i < e->error_count; ++i) {
            if (!text.empty()) text += '\n';
            text += e->errors[i].error ? e->errors[i].error : "unknown error";
        }
        copyOut(text, error, capacity);
        if (e->ctx.f) MOJOSHADER_deleteEffect(e);
        return 0;
    }
    if (!e->ctx.f || e->technique_count <= 0) {
        copyOut("the effect has no technique", error, capacity);
        if (e->ctx.f) MOJOSHADER_deleteEffect(e);
        return 0;
    }
    entry->effect = e;
    std::lock_guard<std::mutex> lock(g_mutex);
    g_entries.push_back(std::move(entry));
    copyOut("", error, capacity);
    return static_cast<int32_t>(g_entries.size());
}

int32_t fxcInfo(int32_t id, char* out, int32_t capacity) {
    std::lock_guard<std::mutex> lock(g_mutex);
    Entry* entry = find(id);
    if (!entry) return copyOut("", out, capacity);
    const MOJOSHADER_effect* e = entry->effect;
    std::ostringstream s;
    for (int i = 0; i < e->param_count; ++i) {
        const MOJOSHADER_effectValue& v = e->params[i].value;
        s << "A\t" << (v.name ? v.name : "") << '\t' << v.type.parameter_class << '\t' << v.type.parameter_type << '\t'
          << v.type.rows << '\t' << v.type.columns << '\t' << v.type.elements << '\n';
    }
    for (int t = 0; t < e->technique_count; ++t) {
        const MOJOSHADER_effectTechnique& tech = e->techniques[t];
        s << "T\t" << t << '\t' << (tech.name ? tech.name : "") << '\n';
        for (unsigned p = 0; p < tech.pass_count; ++p) {
            const MOJOSHADER_effectPass& pass = tech.passes[p];
            s << "P\t" << t << '\t' << p << '\t' << (pass.name ? pass.name : "") << '\n';
            const MOJOSHADER_effectShader* shader = pixelShaderOf(e, pass);
            if (!shader) continue;
            for (unsigned k = 0; k < shader->sampler_count; ++k) {
                const MOJOSHADER_samplerStateRegister& r = shader->samplers[k];
                s << "S\t" << t << '\t' << p << '\t' << r.sampler_register << '\t' << (r.sampler_name ? r.sampler_name : "") << '\n';
            }
        }
    }
    return copyOut(s.str(), out, capacity);
}

int32_t fxcPassSource(int32_t id, int32_t technique, int32_t pass, char* out, int32_t capacity) {
    std::lock_guard<std::mutex> lock(g_mutex);
    Entry* entry = find(id);
    if (!entry) return -copyOut("unknown effect", out, capacity);
    const MOJOSHADER_effectPass* p = passOf(entry->effect, technique, pass);
    if (!p) return -copyOut("unknown technique or pass", out, capacity);
    const MOJOSHADER_effectShader* shader = pixelShaderOf(entry->effect, *p);
    if (!shader) return -copyOut("the pass has no pixel shader", out, capacity);
    if (shader->is_preshader) return -copyOut("passes that pick their pixel shader with a preshader are not supported", out, capacity);
    const auto* pd = static_cast<const MOJOSHADER_parseData*>(shader->shader);
    if (!pd) return -copyOut("the pixel shader was not converted", out, capacity);
    if (pd->error_count > 0) {
        std::string text;
        for (int i = 0; i < pd->error_count; ++i) {
            if (!text.empty()) text += '\n';
            text += pd->errors[i].error ? pd->errors[i].error : "unknown error";
        }
        return -copyOut(text, out, capacity);
    }
    if (floatRegisterCount(pd) > kFloatRegisters) return -copyOut("the pixel shader uses more than 256 float registers", out, capacity);
    std::string source, error;
    if (!convert(pd, source, error)) return -copyOut(error, out, capacity);
    return copyOut(source, out, capacity);
}

int32_t fxcParamCount(int32_t id) {
    std::lock_guard<std::mutex> lock(g_mutex);
    Entry* entry = find(id);
    return entry ? entry->effect->param_count : 0;
}

int32_t fxcParamFloats(int32_t id, int32_t index) {
    std::lock_guard<std::mutex> lock(g_mutex);
    Entry* entry = find(id);
    if (!entry || index < 0 || index >= entry->effect->param_count) return 0;
    const MOJOSHADER_effectValue& v = entry->effect->params[index].value;
    const auto type = v.type.parameter_type;
    if (type != MOJOSHADER_SYMTYPE_FLOAT && type != MOJOSHADER_SYMTYPE_INT && type != MOJOSHADER_SYMTYPE_BOOL) return 0;
    return static_cast<int32_t>(std::max(v.type.rows, 1u) * std::max(v.type.columns, 1u) * std::max(v.type.elements, 1u));
}

bool fxcSpriteSource(int32_t id, int32_t technique, int32_t pass, std::string& out, std::string& error, int32_t& registers) {
    std::lock_guard<std::mutex> lock(g_mutex);
    Entry* entry = find(id);
    if (!entry) {
        error = "unknown effect";
        return false;
    }
    const MOJOSHADER_effectPass* p = passOf(entry->effect, technique, pass);
    const MOJOSHADER_effectShader* shader = p ? pixelShaderOf(entry->effect, *p) : nullptr;
    if (!shader || shader->is_preshader || !shader->shader) {
        error = "the pass has no usable pixel shader";
        return false;
    }
    const auto* pd = static_cast<const MOJOSHADER_parseData*>(shader->shader);
    if (pd->error_count > 0) {
        error = pd->errors[0].error ? pd->errors[0].error : "unknown error";
        return false;
    }
    registers = floatRegisterCount(pd);
    return convert(pd, out, error, true);
}

int32_t fxcSetParam(int32_t id, int32_t index, const float* values, int32_t count) {
    if (!values || count <= 0) return 0;
    std::lock_guard<std::mutex> lock(g_mutex);
    Entry* entry = find(id);
    if (!entry || index < 0 || index >= entry->effect->param_count) return 0;
    MOJOSHADER_effectValue& v = entry->effect->params[index].value;
    if (!v.values || v.value_count == 0) return 0;
    const bool isFloat = v.type.parameter_type == MOJOSHADER_SYMTYPE_FLOAT;
    if (!isFloat && v.type.parameter_type != MOJOSHADER_SYMTYPE_INT && v.type.parameter_type != MOJOSHADER_SYMTYPE_BOOL) return 0;
    const int columns = static_cast<int>(v.type.columns);
    const int rows = static_cast<int>(std::max(v.type.rows, 1u) * std::max(v.type.elements, 1u));
    const int slots = static_cast<int>(v.value_count);
    auto put = [&](int slot, float value) {
        if (slot < 0 || slot >= slots) return;
        if (isFloat) v.valuesF[slot] = value;
        else v.valuesI[slot] = static_cast<int>(value);
    };
    if (columns >= 1 && columns < 4 && count == rows * columns) {
        for (int r = 0; r < rows; ++r)
            for (int c = 0; c < columns; ++c) put(r * 4 + c, values[r * columns + c]);
    } else {
        for (int k = 0; k < std::min(count, slots); ++k) put(k, values[k]);
    }
    return 1;
}

int32_t fxcCommit(int32_t id, int32_t technique, int32_t pass, float* out, int32_t capacity) {
    std::lock_guard<std::mutex> lock(g_mutex);
    Entry* entry = find(id);
    if (!entry) return 0;
    MOJOSHADER_effect* e = entry->effect;
    const MOJOSHADER_effectPass* p = passOf(e, technique, pass);
    if (!p) return 0;
    const MOJOSHADER_effectShader* shader = pixelShaderOf(e, *p);
    if (!shader || shader->is_preshader || !shader->shader) return 0;
    const int registers = floatRegisterCount(static_cast<const MOJOSHADER_parseData*>(shader->shader));
    MOJOSHADER_effectSetTechnique(e, &e->techniques[technique]);
    unsigned passes = 0;
    MOJOSHADER_effectBegin(e, &passes, 0, &entry->changes);
    MOJOSHADER_effectBeginPass(e, static_cast<unsigned>(pass));
    MOJOSHADER_effectEndPass(e);
    MOJOSHADER_effectEnd(e);
    const int32_t floats = std::min(registers * 4, capacity);
    if (out && floats > 0) std::memcpy(out, entry->registers.pf, sizeof(float) * static_cast<size_t>(floats));
    return registers * 4;
}

int32_t fxcFree(int32_t id) {
    std::lock_guard<std::mutex> lock(g_mutex);
    Entry* entry = find(id);
    if (!entry) return 0;
    MOJOSHADER_deleteEffect(entry->effect);
    g_entries[static_cast<size_t>(id - 1)].reset();
    return 1;
}

}
