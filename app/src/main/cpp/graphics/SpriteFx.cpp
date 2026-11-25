#include "graphics/SpriteFx.h"
#include "graphics/FxcEffect.h"
#include "graphics/GlApi.h"
#include "graphics/GlHooks.h"
#include "core/Log.h"
#include "hook/HookManager.h"
#include "il2cpp/Api.h"
#include "il2cpp/Resolver.h"

#include <algorithm>
#include <atomic>
#include <cstring>
#include <initializer_list>
#include <map>
#include <mutex>
#include <sstream>
#include <string>
#include <unordered_map>
#include <vector>

namespace bl::graphics {

namespace {

constexpr int kMaxSlots = 64;
constexpr int kMaxFloats = 256;
constexpr int kRing = 2048;
constexpr int kIdBase = 2048;
constexpr float kMagic = -1771.0f;

enum Stat {
    StatRecords,
    StatHits,
    StatMisses,
    StatLinked,
    StatLinkErrors,
    StatProbed,
    StatColorPrograms,
    StatVertexFallback,
    StatCount,
};

std::atomic<int64_t> g_stats[StatCount];

struct Slot {
    Il2CppObject* pass = nullptr;
    int32_t fxc = 0, technique = 0, passIndex = 0;
    int32_t registers = 0;
    std::string body;
    std::vector<int32_t> paramFloats;
};

struct Record {
    uint32_t serial = 0;
    int32_t slot = 0;
    int32_t count = 0;
    float values[kMaxFloats] = {};
};

std::mutex g_mutex;
Slot g_slots[kMaxSlots];
std::atomic<int> g_slotCount{0};
Il2CppObject* g_passes[kMaxSlots] = {};
std::atomic<bool> g_active{false};
uint32_t g_serial = 0;
Record g_ring[kRing];

using ApplyFn = void (*)(Il2CppObject* self, Il2CppObject* material, const MethodInfo*);
using SelfFn = void (*)(Il2CppObject* self, const MethodInfo*);
ApplyFn g_origApply = nullptr;
SelfFn g_origPassApply = nullptr;
SelfFn g_origShaderApply = nullptr;
SelfFn g_origEnd = nullptr;

const MethodInfo* g_setVector = nullptr;
const MethodInfo* g_hasProperty = nullptr;
int32_t g_colorId = 0;

struct Offsets {
    int32_t passEffect = -1;
    int32_t effectParameters = -1;
    int32_t collectionParameters = -1;
    int32_t parameterData = -1;
    int32_t batchSortMode = -1;
    int32_t batchBegun = -1;
    int32_t batchOverload = -1;
    FieldInfo* boundEffect = nullptr;
    FieldInfo* mainSpriteBatch = nullptr;
};
Offsets g_off;

constexpr int32_t kImmediate = 1;

const MethodInfo* findMethod(Il2CppClass* cls, const char* name, std::initializer_list<const char*> types) {
    auto& a = il2cpp::api();
    void* iter = nullptr;
    while (const MethodInfo* m = a.class_get_methods(cls, &iter)) {
        if (std::strcmp(a.method_get_name(m), name) != 0) continue;
        if (a.method_get_param_count(m) != types.size()) continue;
        bool same = true;
        uint32_t i = 0;
        for (const char* type : types) {
            char* got = a.type_get_name(a.method_get_param(m, i++));
            same = same && got && std::strcmp(got, type) == 0;
            a.il2cpp_free(got);
        }
        if (same) return m;
    }
    return nullptr;
}

int slotOf(Il2CppObject* pass) {
    if (!pass) return -1;
    const int n = g_slotCount.load(std::memory_order_acquire);
    for (int i = 0; i < n; ++i)
        if (g_passes[i] == pass) return i;
    return -1;
}

template <typename T>
T& at(Il2CppObject* obj, int32_t offset) {
    return *reinterpret_cast<T*>(reinterpret_cast<char*>(obj) + offset);
}

struct ArrayView {
    Il2CppObject** items = nullptr;
    float* floats = nullptr;
    size_t length = 0;
};

ArrayView arrayOf(Il2CppObject* array) {
    ArrayView v;
    if (!array) return v;
    v.length = static_cast<size_t>(at<uintptr_t>(array, 0x18));
    v.items = reinterpret_cast<Il2CppObject**>(reinterpret_cast<char*>(array) + 0x20);
    v.floats = reinterpret_cast<float*>(reinterpret_cast<char*>(array) + 0x20);
    return v;
}

int32_t commitFromGame(const Slot& slot, float* out) {
    Il2CppObject* effect = at<Il2CppObject*>(slot.pass, g_off.passEffect);
    Il2CppObject* collection = effect ? at<Il2CppObject*>(effect, g_off.effectParameters) : nullptr;
    ArrayView params = arrayOf(collection ? at<Il2CppObject*>(collection, g_off.collectionParameters) : nullptr);
    const size_t n = std::min(params.length, slot.paramFloats.size());
    for (size_t i = 0; i < n; ++i) {
        if (slot.paramFloats[i] <= 0 || !params.items[i]) continue;
        ArrayView data = arrayOf(at<Il2CppObject*>(params.items[i], g_off.parameterData));
        if (data.length == 0) continue;
        const int32_t count = std::min(static_cast<int32_t>(data.length), slot.paramFloats[i]);
        fxcSetParam(slot.fxc, static_cast<int32_t>(i), data.floats, count);
    }
    const int32_t floats = fxcCommit(slot.fxc, slot.technique, slot.passIndex, out, kMaxFloats);
    return std::min(floats, kMaxFloats);
}

void hkApply(Il2CppObject* self, Il2CppObject* material, const MethodInfo* method) {
    g_origApply(self, material, method);
    const int index = slotOf(self);
    if (index < 0 || !material || !g_setVector) return;
    float values[kMaxFloats];
    const int32_t count = commitFromGame(g_slots[index], values);
    uint32_t serial;
    {
        std::lock_guard<std::mutex> lock(g_mutex);
        serial = ++g_serial;
        if (serial >= static_cast<uint32_t>(kIdBase * kIdBase)) serial = g_serial = 1;
        Record& r = g_ring[serial % kRing];
        r.serial = serial;
        r.slot = index;
        r.count = std::max(count, 0);
        if (r.count > 0) std::memcpy(r.values, values, sizeof(float) * static_cast<size_t>(r.count));
    }
    g_stats[StatRecords]++;
    float vec[4] = {kMagic, static_cast<float>(serial % kIdBase), static_cast<float>(serial / kIdBase), 1.0f};
    int32_t id = g_colorId;
    void* args[2] = {&id, vec};
    Il2CppObject* exc = nullptr;
    il2cpp::api().runtime_invoke(g_setVector, material, args, &exc);
}

Il2CppObject* staticObject(FieldInfo* field) {
    Il2CppObject* value = nullptr;
    if (field) il2cpp::api().field_static_get_value(field, &value);
    return value;
}

void overloadBound() {
    Il2CppObject* bound = staticObject(g_off.boundEffect);
    if (slotOf(bound) < 0) return;
    Il2CppObject* batch = staticObject(g_off.mainSpriteBatch);
    if (!batch || !at<bool>(batch, g_off.batchBegun) || at<int32_t>(batch, g_off.batchSortMode) != kImmediate) return;
    at<Il2CppObject*>(batch, g_off.batchOverload) = bound;
}

void hkPassApply(Il2CppObject* self, const MethodInfo* method) {
    g_origPassApply(self, method);
    if (slotOf(self) >= 0) overloadBound();
}

void hkShaderApply(Il2CppObject* self, const MethodInfo* method) {
    g_origShaderApply(self, method);
    overloadBound();
}

void hkEnd(Il2CppObject* self, const MethodInfo* method) {
    g_origEnd(self, method);
    if (self && slotOf(at<Il2CppObject*>(self, g_off.batchOverload)) >= 0) at<Il2CppObject*>(self, g_off.batchOverload) = nullptr;
}

bool setupGameSide() {
    static int state = 0;
    if (state != 0) return state > 0;
    state = -1;
    auto& a = il2cpp::api();
    const char* xna = "Microsoft.Xna.Framework.Graphics";
    Il2CppClass* pass = il2cpp::findClass({xna, "EffectPass", {}});
    Il2CppClass* effect = il2cpp::findClass({xna, "Effect", {}});
    Il2CppClass* collection = il2cpp::findClass({xna, "EffectParameterCollection", {}});
    Il2CppClass* parameter = il2cpp::findClass({xna, "EffectParameter", {}});
    Il2CppClass* batch = il2cpp::findClass({xna, "SpriteBatch", {}});
    Il2CppClass* shaderData = il2cpp::findClass({"Terraria.Graphics.Shaders", "ShaderData", {}});
    Il2CppClass* main = il2cpp::findClass({"Terraria", "Main", {}});
    Il2CppClass* material = il2cpp::findClass({"UnityEngine", "Material", {}});
    Il2CppClass* shader = il2cpp::findClass({"UnityEngine", "Shader", {}});
    if (!pass || !effect || !collection || !parameter || !batch || !shaderData || !main || !material || !shader) {
        BL_ERROR("sprite effects: game classes missing");
        return false;
    }
    const MethodInfo* apply = findMethod(pass, "ApplyValuesToMaterial", {"UnityEngine.Material"});
    const MethodInfo* passApply = findMethod(pass, "Apply", {});
    const MethodInfo* shaderApply = findMethod(shaderData, "Apply", {});
    const MethodInfo* end = findMethod(batch, "End", {});
    g_setVector = findMethod(material, "SetVector", {"System.Int32", "UnityEngine.Vector4"});
    g_hasProperty = findMethod(material, "HasProperty", {"System.String"});
    const MethodInfo* toId = findMethod(shader, "PropertyToID", {"System.String"});
    g_off.passEffect = il2cpp::fieldOffset(pass, "_effect");
    g_off.effectParameters = il2cpp::fieldOffset(effect, "<Parameters>k__BackingField");
    g_off.collectionParameters = il2cpp::fieldOffset(collection, "_parameters");
    g_off.parameterData = il2cpp::fieldOffset(parameter, "Data");
    g_off.batchSortMode = il2cpp::fieldOffset(batch, "_sortMode");
    g_off.batchBegun = il2cpp::fieldOffset(batch, "_beginCalled");
    g_off.batchOverload = il2cpp::fieldOffset(batch, "OverloadedEffectPass");
    g_off.boundEffect = il2cpp::findField(pass, "BoundEffect");
    g_off.mainSpriteBatch = il2cpp::findField(main, "spriteBatch");
    if (!apply || !passApply || !shaderApply || !end || !g_setVector || !toId || g_off.passEffect < 0 || g_off.effectParameters < 0
        || g_off.collectionParameters < 0 || g_off.parameterData < 0 || g_off.batchSortMode < 0 || g_off.batchBegun < 0
        || g_off.batchOverload < 0 || !g_off.boundEffect || !g_off.mainSpriteBatch) {
        BL_ERROR("sprite effects: game methods or fields missing");
        return false;
    }
    void* args[1] = {a.string_new("_Color")};
    Il2CppObject* exc = nullptr;
    Il2CppObject* boxed = a.runtime_invoke(toId, nullptr, args, &exc);
    if (exc || !boxed) return false;
    g_colorId = *reinterpret_cast<int32_t*>(reinterpret_cast<char*>(boxed) + sizeof(Il2CppObject));
    const bool hooked = hook::install(apply, hkApply, &g_origApply) && hook::install(passApply, hkPassApply, &g_origPassApply)
        && hook::install(shaderApply, hkShaderApply, &g_origShaderApply) && hook::install(end, hkEnd, &g_origEnd);
    if (!hooked) {
        BL_ERROR("sprite effects: game methods not hooked");
        return false;
    }
    state = 1;
    return true;
}

struct Copy {
    GLint src = -1, dst = -1;
    GLenum type = 0;
};

struct Block {
    GLuint src = 0, dst = 0;
};

struct Linked {
    bool failed = false;
    GLuint program = 0;
    GLint registers = -1;
    GLint samplers[6] = {-1, -1, -1, -1, -1, -1};
    std::vector<Copy> copies;
    std::vector<Block> blocks;
};

constexpr int kTextureCount = 6;
const char* const kTextures[kTextureCount] = {"_MainTex", "_SecondaryTex", "_ThirdTex", "_ForthTex", "_MainAlphaTex", "_MainPalTex"};
const char* const kSamplers[kTextureCount] = {"ps_s0", "ps_s1", "ps_s2", "ps_s3", "blMainAlpha", "blMainPal"};

struct UnityProgram {
    GLint color = -1;
    GLint textures[kTextureCount] = {-1, -1, -1, -1, -1, -1};
    bool marker = false;
};

std::unordered_map<GLuint, UnityProgram> g_programs;

std::mutex g_sourceMutex;
std::unordered_map<GLuint, std::string> g_sources;
std::unordered_map<GLuint, std::vector<std::string>> g_attached;

struct Captured {
    std::string vertex, fragment;
};

Captured captured(GLuint program) {
    Captured c;
    std::lock_guard<std::mutex> lock(g_sourceMutex);
    auto found = g_attached.find(program);
    if (found == g_attached.end()) return c;
    for (const std::string& t : found->second) {
        const bool vertex = t.find("#define VERTEX") != std::string::npos
            || (t.find("#define FRAGMENT") == std::string::npos && t.find("gl_Position") != std::string::npos);
        (vertex ? c.vertex : c.fragment) = t;
    }
    return c;
}

void logChunks(const char* label, const std::string& text) {
    for (size_t at = 0; at < text.size(); at += 900)
        BL_INFO("sprite effects: %s [%zu] %s", label, at / 900, text.substr(at, 900).c_str());
}
std::map<std::pair<int, GLuint>, Linked> g_linked;

const char* kOwnVertex = R"(#version 300 es
in highp vec4 in_POSITION0;
in highp vec4 in_COLOR0;
in highp vec2 in_TEXCOORD0;
uniform highp vec4 hlslcc_mtx4x4unity_ObjectToWorld[4];
uniform highp vec4 hlslcc_mtx4x4unity_MatrixVP[4];
out highp vec4 vs_COLOR0;
out highp vec2 vs_TEXCOORD0;
void main() {
    vec4 p = hlslcc_mtx4x4unity_ObjectToWorld[0] * in_POSITION0.x + hlslcc_mtx4x4unity_ObjectToWorld[1] * in_POSITION0.y
        + hlslcc_mtx4x4unity_ObjectToWorld[2] * in_POSITION0.z + hlslcc_mtx4x4unity_ObjectToWorld[3];
    gl_Position = hlslcc_mtx4x4unity_MatrixVP[0] * p.x + hlslcc_mtx4x4unity_MatrixVP[1] * p.y
        + hlslcc_mtx4x4unity_MatrixVP[2] * p.z + hlslcc_mtx4x4unity_MatrixVP[3] * p.w;
    vs_COLOR0 = in_COLOR0;
    vs_TEXCOORD0 = in_TEXCOORD0;
}
)";

std::string shaderSource(GLuint shader) {
    auto& g = gl();
    GLint length = 0;
    g.GetShaderiv(shader, GL_SHADER_SOURCE_LENGTH, &length);
    if (length <= 1) return {};
    std::string text(static_cast<size_t>(length), '\0');
    GLsizei got = 0;
    g.GetShaderSource(shader, length, &got, text.data());
    text.resize(static_cast<size_t>(got));
    return text;
}

std::string infoLog(GLuint object, bool program) {
    auto& g = gl();
    GLint length = 0;
    if (program) g.GetProgramiv(object, GL_INFO_LOG_LENGTH, &length);
    else g.GetShaderiv(object, GL_INFO_LOG_LENGTH, &length);
    if (length <= 1) return {};
    std::string text(static_cast<size_t>(length), '\0');
    GLsizei got = 0;
    if (program) g.GetProgramInfoLog(object, length, &got, text.data());
    else g.GetShaderInfoLog(object, length, &got, text.data());
    text.resize(static_cast<size_t>(got));
    return text;
}

GLuint compile(GLenum type, const std::string& source, std::string& error) {
    auto& g = gl();
    GLuint shader = g.CreateShader(type);
    const char* text = source.c_str();
    g.ShaderSource(shader, 1, &text, nullptr);
    g.CompileShader(shader);
    GLint ok = 0;
    g.GetShaderiv(shader, GL_COMPILE_STATUS, &ok);
    if (!ok) {
        error = infoLog(shader, false);
        g.DeleteShader(shader);
        return 0;
    }
    return shader;
}

struct Varyings {
    std::string version = "#version 300 es", decls, uv, color;
};

Varyings readVaryings(const std::string& source) {
    Varyings v;
    std::istringstream in(source);
    std::string line;
    while (std::getline(in, line)) {
        if (!line.empty() && line.back() == '\r') line.pop_back();
        if (line.compare(0, 8, "#version") == 0) {
            v.version = line;
            continue;
        }
        const size_t out = line.find("out ");
        if (out == std::string::npos) continue;
        const size_t semi = line.find(';', out);
        if (semi == std::string::npos) continue;
        std::string decl = line.substr(out + 4, semi - out - 4);
        const size_t space = decl.find_last_of(' ');
        if (space == std::string::npos) continue;
        const std::string name = decl.substr(space + 1);
        if (name.compare(0, 3, "vs_") != 0) continue;
        if (v.decls.find(" " + name + ";") != std::string::npos) continue;
        v.decls += "in " + decl + ";\n";
        if (v.uv.empty() && name.find("TEXCOORD0") != std::string::npos) v.uv = name;
        if (v.color.empty() && name.find("COLOR") != std::string::npos) v.color = name;
    }
    return v;
}

GLuint unityVertexShader(GLuint program) {
    auto& g = gl();
    GLuint shaders[4] = {};
    GLsizei count = 0;
    g.GetAttachedShaders(program, 4, &count, shaders);
    for (GLsizei i = 0; i < count; ++i) {
        GLint type = 0;
        g.GetShaderiv(shaders[i], GL_SHADER_TYPE, &type);
        if (type == GL_VERTEX_SHADER) return shaders[i];
    }
    return 0;
}

struct MainTexture {
    bool palette = false, alpha = false, trim = false;
    std::string index = "w", alphaChannel = "w";
};

std::string swizzleAfter(const std::string& source, const char* call) {
    const size_t at = source.find(call);
    if (at == std::string::npos) return {};
    int depth = 0;
    for (size_t i = at + std::strlen(call) - 1; i < source.size(); ++i) {
        if (source[i] == '(') depth++;
        else if (source[i] == ')' && --depth == 0) {
            if (i + 2 < source.size() && source[i + 1] == '.' && std::strchr("xyzwrgba", source[i + 2])) return std::string(1, source[i + 2]);
            return {};
        }
    }
    return {};
}

MainTexture readMainTexture(GLuint unity, const std::string& fragment) {
    auto& g = gl();
    MainTexture m;
    m.palette = g.GetUniformLocation(unity, "_MainPalTex") >= 0;
    m.alpha = g.GetUniformLocation(unity, "_MainAlphaTex") >= 0;
    m.trim = g.GetUniformLocation(unity, "_TrimMin") >= 0 && g.GetUniformLocation(unity, "_TrimMax") >= 0;
    const std::string index = swizzleAfter(fragment, "texture(_MainTex,");
    const std::string alpha = swizzleAfter(fragment, "texture(_MainAlphaTex,");
    if (!index.empty()) m.index = index;
    if (!alpha.empty()) m.alphaChannel = alpha;
    return m;
}

std::string mainSampler(const MainTexture& m) {
    std::string s = "uniform mediump sampler2D blMainAlpha;\nuniform mediump sampler2D blMainPal;\n";
    if (m.palette) s += "uniform mediump float _palScaleSize;\n";
    if (m.trim) s += "uniform vec2 _TrimMin;\nuniform vec2 _TrimMax;\n";
    s += "vec4 blMainSample(vec2 uv) {\n    uv = vec2(uv.x, 1.0 - uv.y);\n";
    if (m.trim) s += "    uv = min(max(uv, _TrimMin), _TrimMax);\n";
    if (m.palette) {
        s += "    vec2 cell = floor(min(uv, vec2(0.99)) * _palScaleSize);\n";
        s += "    float row = (cell.y * _palScaleSize + cell.x) / (_palScaleSize * _palScaleSize);\n";
        s += "    float index = floor(texture(ps_s0, uv)." + m.index + " * 255.0 + 0.5) * 0.00392156886;\n";
        s += "    vec4 pal = texture(blMainPal, vec2(index, row));\n";
        s += "    vec3 rgb = pal.xyz;\n";
        s += m.alpha ? "    float a = texture(blMainAlpha, uv)." + m.alphaChannel + ";\n" : "    float a = pal.w;\n";
    } else if (m.alpha) {
        s += "    vec3 rgb = texture(ps_s0, uv).xyz;\n    float a = texture(blMainAlpha, uv)." + m.alphaChannel + ";\n";
    } else {
        s += "    vec4 t = texture(ps_s0, uv);\n    vec3 rgb = t.xyz;\n    float a = t.w;\n";
    }
    s += "    return vec4(rgb * a, a);\n}\n";
    s += "vec4 blMainSampleLod(vec2 uv, float lod) {\n    return blMainSample(uv);\n}\n";
    return s;
}

void replaceAll(std::string& text, const std::string& from, const std::string& to) {
    for (size_t at = text.find(from); at != std::string::npos; at = text.find(from, at + to.size())) text.replace(at, from.size(), to);
}

std::string fragmentSource(const Varyings& v, const std::string& body, const MainTexture& texture) {
    std::string s = v.version + "\nprecision highp float;\nprecision highp int;\n";
    s += v.decls;
    s += "#define BL_SPRITE_UV " + (v.uv.empty() ? std::string("vec2(0.0)") : "(" + v.uv + ").xy") + "\n";
    s += "#define BL_SPRITE_COLOR " + (v.color.empty() ? std::string("vec4(1.0)") : "vec4(" + v.color + ")") + "\n";
    s += "out vec4 fragColor;\n";
    std::string code = body;
    replaceAll(code, "texture2DLod(ps_s0,", "blMainSampleLod(");
    replaceAll(code, "texture2D(ps_s0,", "blMainSample(");
    const size_t at = code.find("void blFxcMain()");
    if (at != std::string::npos) code.insert(at, mainSampler(texture));
    s += code;
    return s;
}

bool isSampler(GLenum type) {
    return type == GL_SAMPLER_2D || type == GL_SAMPLER_3D || type == GL_SAMPLER_CUBE || type == GL_SAMPLER_2D_SHADOW
        || type == GL_SAMPLER_2D_ARRAY || type == GL_INT_SAMPLER_2D || type == GL_UNSIGNED_INT_SAMPLER_2D;
}

void buildCopies(GLuint unity, Linked& l) {
    auto& g = gl();
    GLint count = 0;
    g.GetProgramiv(unity, GL_ACTIVE_UNIFORMS, &count);
    for (GLint i = 0; i < count; ++i) {
        char name[256];
        GLsizei length = 0;
        GLint size = 0;
        GLenum type = 0;
        g.GetActiveUniform(unity, static_cast<GLuint>(i), sizeof name, &length, &size, &type, name);
        if (isSampler(type)) continue;
        std::string base(name, static_cast<size_t>(length));
        const size_t bracket = base.find('[');
        if (bracket != std::string::npos) base.resize(bracket);
        for (GLint k = 0; k < std::max(size, 1); ++k) {
            const std::string element = size > 1 || bracket != std::string::npos ? base + "[" + std::to_string(k) + "]" : base;
            Copy c;
            c.src = g.GetUniformLocation(unity, element.c_str());
            c.dst = g.GetUniformLocation(l.program, element.c_str());
            c.type = type;
            if (c.src >= 0 && c.dst >= 0) l.copies.push_back(c);
        }
    }
    GLint blocks = 0;
    g.GetProgramiv(unity, GL_ACTIVE_UNIFORM_BLOCKS, &blocks);
    for (GLint i = 0; i < blocks; ++i) {
        char name[256];
        GLsizei length = 0;
        g.GetActiveUniformBlockName(unity, static_cast<GLuint>(i), sizeof name, &length, name);
        const GLuint dst = g.GetUniformBlockIndex(l.program, name);
        if (dst != GL_INVALID_INDEX) l.blocks.push_back({static_cast<GLuint>(i), dst});
    }
}

bool linkWith(GLuint unity, GLuint vertex, const std::string& fragment, Linked& l, std::string& error) {
    auto& g = gl();
    GLuint fs = compile(GL_FRAGMENT_SHADER, fragment, error);
    if (!fs) return false;
    GLuint program = g.CreateProgram();
    g.AttachShader(program, vertex);
    g.AttachShader(program, fs);
    GLint attribs = 0;
    g.GetProgramiv(unity, GL_ACTIVE_ATTRIBUTES, &attribs);
    for (GLint i = 0; i < attribs; ++i) {
        char name[128];
        GLsizei length = 0;
        GLint size = 0;
        GLenum type = 0;
        g.GetActiveAttrib(unity, static_cast<GLuint>(i), sizeof name, &length, &size, &type, name);
        const GLint location = g.GetAttribLocation(unity, name);
        if (location >= 0) g.BindAttribLocation(program, static_cast<GLuint>(location), name);
    }
    g.LinkProgram(program);
    g.DeleteShader(fs);
    GLint ok = 0;
    g.GetProgramiv(program, GL_LINK_STATUS, &ok);
    if (!ok) {
        error = infoLog(program, true);
        g.DeleteProgram(program);
        return false;
    }
    l.program = program;
    return true;
}

Linked& linked(int slot, GLuint unity) {
    Linked& l = g_linked[{slot, unity}];
    if (l.program || l.failed) return l;
    std::string body;
    {
        std::lock_guard<std::mutex> lock(g_mutex);
        body = g_slots[slot].body;
    }
    std::string error;
    GLuint vertex = unityVertexShader(unity);
    bool ok = false;
    const Captured source = captured(unity);
    const MainTexture texture = readMainTexture(unity, source.fragment);
    BL_INFO("sprite effects: Unity program %u: attached vertex %u, captured vertex %zu chars, fragment %zu chars", unity, vertex,
            source.vertex.size(), source.fragment.size());
    static std::vector<GLuint> logged;
    if (!source.fragment.empty() && std::find(logged.begin(), logged.end(), unity) == logged.end()) {
        logged.push_back(unity);
        logChunks("unity fragment", source.fragment);
    }
    if (!vertex && !source.vertex.empty()) {
        const Varyings v = readVaryings(source.vertex);
        std::string vsError;
        GLuint vs = compile(GL_VERTEX_SHADER, source.vertex, vsError);
        ok = vs && !v.uv.empty() && linkWith(unity, vs, fragmentSource(v, body, texture), l, error);
        if (vs) gl().DeleteShader(vs);
        if (!ok) BL_ERROR("sprite effects: Unity's captured vertex shader failed: %s %s", vsError.c_str(), error.c_str());
    }
    if (!ok && vertex) {
        const std::string vsText = shaderSource(vertex);
        const Varyings v = readVaryings(vsText);
        BL_INFO("sprite effects: Unity program %u vertex shader %u (%zu chars), uv '%s', color '%s'", unity, vertex, vsText.size(),
                v.uv.c_str(), v.color.c_str());
        ok = !v.uv.empty() && linkWith(unity, vertex, fragmentSource(v, body, texture), l, error);
        if (!ok) BL_ERROR("sprite effects: linking with Unity's vertex shader failed: %s", error.c_str());
    }
    if (!ok) {
        g_stats[StatVertexFallback]++;
        Varyings own;
        own.decls = "in highp vec4 vs_COLOR0;\nin highp vec2 vs_TEXCOORD0;\n";
        own.uv = "vs_TEXCOORD0";
        own.color = "vs_COLOR0";
        std::string vsError;
        GLuint vs = compile(GL_VERTEX_SHADER, kOwnVertex, vsError);
        ok = vs && linkWith(unity, vs, fragmentSource(own, body, texture), l, error);
        if (vs) gl().DeleteShader(vs);
        if (!ok) BL_ERROR("sprite effects: own vertex shader failed: %s %s", vsError.c_str(), error.c_str());
    }
    if (!ok) {
        l.failed = true;
        g_stats[StatLinkErrors]++;
        return l;
    }
    auto& g = gl();
    l.registers = g.GetUniformLocation(l.program, "ps_uniforms_vec4");
    for (int i = 0; i < kTextureCount; ++i) l.samplers[i] = g.GetUniformLocation(l.program, kSamplers[i]);
    buildCopies(unity, l);
    g_stats[StatLinked]++;
    BL_INFO("sprite effects: slot %d linked over Unity program %u as %u (%zu uniform copies, %zu blocks, palette %d, alpha %d, trim %d)",
            slot, unity, l.program, l.copies.size(), l.blocks.size(), texture.palette, texture.alpha, texture.trim);
    return l;
}

void copyUniform(GLuint unity, const Copy& c) {
    auto& g = gl();
    float f[16];
    switch (c.type) {
        case GL_FLOAT: g.GetUniformfv(unity, c.src, f); g.Uniform1fv(c.dst, 1, f); break;
        case GL_FLOAT_VEC2: g.GetUniformfv(unity, c.src, f); g.Uniform2fv(c.dst, 1, f); break;
        case GL_FLOAT_VEC3: g.GetUniformfv(unity, c.src, f); g.Uniform3fv(c.dst, 1, f); break;
        case GL_FLOAT_VEC4: g.GetUniformfv(unity, c.src, f); g.Uniform4fv(c.dst, 1, f); break;
        case GL_FLOAT_MAT2: g.GetUniformfv(unity, c.src, f); g.UniformMatrix2fv(c.dst, 1, GL_FALSE, f); break;
        case GL_FLOAT_MAT3: g.GetUniformfv(unity, c.src, f); g.UniformMatrix3fv(c.dst, 1, GL_FALSE, f); break;
        case GL_FLOAT_MAT4: g.GetUniformfv(unity, c.src, f); g.UniformMatrix4fv(c.dst, 1, GL_FALSE, f); break;
        case GL_INT:
        case GL_BOOL: {
            GLint i = 0;
            g.GetUniformiv(unity, c.src, &i);
            g.Uniform1iv(c.dst, 1, &i);
            break;
        }
        default: break;
    }
}

UnityProgram& probe(GLuint program) {
    auto found = g_programs.find(program);
    if (found != g_programs.end()) return found->second;
    UnityProgram& p = g_programs[program];
    auto& g = gl();
    p.color = g.GetUniformLocation(program, "_Color");
    for (int i = 0; i < kTextureCount; ++i) p.textures[i] = g.GetUniformLocation(program, kTextures[i]);
    g_stats[StatProbed]++;
    if (p.color >= 0) g_stats[StatColorPrograms]++;
    return p;
}

}

void spriteFxNoteSource(GLuint shader, GLsizei count, const GLchar* const* strings, const GLint* lengths) {
    if (!strings || count <= 0) return;
    std::string text;
    for (GLsizei i = 0; i < count; ++i) {
        if (!strings[i]) continue;
        if (lengths && lengths[i] >= 0) text.append(strings[i], static_cast<size_t>(lengths[i]));
        else text.append(strings[i]);
    }
    std::lock_guard<std::mutex> lock(g_sourceMutex);
    g_sources[shader] = std::move(text);
}

void spriteFxNoteAttach(GLuint program, GLuint shader) {
    std::lock_guard<std::mutex> lock(g_sourceMutex);
    auto text = g_sources.find(shader);
    if (text == g_sources.end()) return;
    auto& list = g_attached[program];
    if (list.size() >= 2) list.clear();
    list.push_back(text->second);
}

void* spriteFxMaterial(void* source) {
    if (!source) return nullptr;
    auto& a = il2cpp::api();
    Il2CppClass* material = il2cpp::findClass({"UnityEngine", "Material", {}});
    const MethodInfo* ctor = material ? findMethod(material, ".ctor", {"UnityEngine.Material"}) : nullptr;
    if (!ctor) return nullptr;
    Il2CppObject* copy = a.object_new(material);
    void* args[1] = {source};
    Il2CppObject* exc = nullptr;
    a.runtime_invoke(ctor, copy, args, &exc);
    if (exc) return nullptr;
    a.gchandle_new(copy, false);
    return copy;
}

int32_t spriteFxRegister(void* pass, int32_t fxc, int32_t technique, int32_t passIndex, char* error, int32_t capacity) {
    auto fail = [&](const std::string& text) {
        if (error && capacity > 0) {
            const size_t n = std::min(text.size(), static_cast<size_t>(capacity - 1));
            std::memcpy(error, text.data(), n);
            error[n] = '\0';
        }
        return 0;
    };
    if (!pass) return fail("no pass");
    if (!gl().load()) return fail("OpenGL ES functions are missing");
    if (!setupGameSide()) return fail("the game's material methods could not be hooked");
    std::string body, convertError;
    int32_t registers = 0;
    if (!fxcSpriteSource(fxc, technique, passIndex, body, convertError, registers)) return fail(convertError);
    if (registers * 4 > kMaxFloats) return fail("sprite passes can use at most 64 float registers");
    std::lock_guard<std::mutex> lock(g_mutex);
    const int n = g_slotCount.load();
    if (n >= kMaxSlots) return fail("too many sprite effects");
    Slot& slot = g_slots[n];
    slot.pass = static_cast<Il2CppObject*>(pass);
    slot.fxc = fxc;
    slot.technique = technique;
    slot.passIndex = passIndex;
    slot.registers = registers;
    slot.body = body;
    slot.paramFloats.clear();
    for (int32_t i = 0, count = fxcParamCount(fxc); i < count; ++i) slot.paramFloats.push_back(fxcParamFloats(fxc, i));
    g_passes[n] = slot.pass;
    g_slotCount.store(n + 1, std::memory_order_release);
    installGlHooks();
    g_active = true;
    if (error && capacity > 0) error[0] = '\0';
    return n + 1;
}

int32_t spriteFxHasProperty(void* material, const char* name) {
    if (!material || !name || !setupGameSide() || !g_hasProperty) return 0;
    auto& a = il2cpp::api();
    void* args[1] = {a.string_new(name)};
    Il2CppObject* exc = nullptr;
    Il2CppObject* boxed = a.runtime_invoke(g_hasProperty, static_cast<Il2CppObject*>(material), args, &exc);
    if (exc || !boxed) return 0;
    return *reinterpret_cast<bool*>(reinterpret_cast<char*>(boxed) + sizeof(Il2CppObject)) ? 1 : 0;
}

int32_t spriteFxStats(int64_t* out, int32_t count) {
    const int n = std::min<int>(count, StatCount);
    for (int i = 0; i < n; ++i) out[i] = g_stats[i].load();
    return StatCount;
}

bool spriteFxActive() {
    return g_active.load(std::memory_order_relaxed);
}

bool spriteFxDraw(GLuint program, GLenum mode, GLsizei count, GLenum type, const void* indices) {
    if (!program) return false;
    UnityProgram& p = probe(program);
    if (p.color < 0) return false;
    auto& g = gl();
    float color[4] = {};
    g.GetUniformfv(program, p.color, color);
    if (color[0] != kMagic) return false;
    if (!p.marker) {
        p.marker = true;
        BL_INFO("sprite effects: marker program is %u (_Color %d, textures %d %d %d %d)", program, p.color, p.textures[0], p.textures[1],
                p.textures[2], p.textures[3]);
    }
    const uint32_t serial = static_cast<uint32_t>(color[1]) + static_cast<uint32_t>(color[2]) * kIdBase;
    Record record;
    {
        std::lock_guard<std::mutex> lock(g_mutex);
        const Record& r = g_ring[serial % kRing];
        if (r.serial != serial) {
            g_stats[StatMisses]++;
            return false;
        }
        record.slot = r.slot;
        record.count = r.count;
        std::memcpy(record.values, r.values, sizeof(float) * static_cast<size_t>(r.count));
    }
    Linked& l = linked(record.slot, program);
    if (l.failed) return false;
    GLint units[kTextureCount] = {};
    for (int i = 0; i < kTextureCount; ++i) {
        if (p.textures[i] >= 0) g.GetUniformiv(program, p.textures[i], &units[i]);
        else units[i] = units[0];
    }
    g.UseProgram(l.program);
    for (const Copy& c : l.copies) copyUniform(program, c);
    for (const Block& b : l.blocks) {
        GLint binding = 0;
        g.GetActiveUniformBlockiv(program, b.src, GL_UNIFORM_BLOCK_BINDING, &binding);
        g.UniformBlockBinding(l.program, b.dst, static_cast<GLuint>(binding));
    }
    for (int i = 0; i < kTextureCount; ++i)
        if (l.samplers[i] >= 0) g.Uniform1i(l.samplers[i], units[i]);
    if (l.registers >= 0 && record.count >= 4) g.Uniform4fv(l.registers, record.count / 4, record.values);
    g.DrawElements(mode, count, type, indices);
    g.UseProgram(program);
    g_stats[StatHits]++;
    return true;
}

}
