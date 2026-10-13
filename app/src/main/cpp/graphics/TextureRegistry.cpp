#include "graphics/TextureRegistry.h"
#include "graphics/GlState.h"
#include "core/AppJni.h"
#include "core/Log.h"

#include <cstring>
#include <memory>
#include <mutex>
#include <vector>

namespace bl::graphics {

namespace {

struct Entry {
    std::string path;
    bool linear = true, repeat = false;
    std::vector<uint8_t> pixels;   // ate o envio; depois vazio
    LoadedTexture gl;
    bool uploaded = false, failed = false;
};

std::mutex g_mutex;
std::vector<std::unique_ptr<Entry>> g_entries;   // id = kFirstModTexture - indice

// [largura, altura] + RGBA de cima para baixo, pelo ModImages.decodeRgba.
bool decode(const std::string& path, std::vector<uint8_t>& out, int& width, int& height) {
    JNIEnv* env = appJniEnv();
    jclass cls = env ? appJniClass(env, "dev.bunnyloader.game.ModImages") : nullptr;
    jmethodID id = cls ? env->GetStaticMethodID(cls, "decodeRgba", "(Ljava/lang/String;)[B") : nullptr;
    if (!id || !appJniCleared(env, "decodeRgba")) {
        BL_ERROR("screen effects: ModImages.decodeRgba not found");
        if (cls) env->DeleteLocalRef(cls);
        return false;
    }
    jstring text = env->NewStringUTF(path.c_str());
    auto bytes = static_cast<jbyteArray>(env->CallStaticObjectMethod(cls, id, text));
    env->DeleteLocalRef(text);
    env->DeleteLocalRef(cls);
    if (!appJniCleared(env, "decodeRgba") || !bytes) return false;

    const jsize length = env->GetArrayLength(bytes);
    bool ok = false;
    if (length >= 8) {
        int32_t header[2] = {0, 0};
        env->GetByteArrayRegion(bytes, 0, 8, reinterpret_cast<jbyte*>(header));
        width = header[0];
        height = header[1];
        const size_t row = static_cast<size_t>(width) * 4;
        if (width > 0 && height > 0 && static_cast<size_t>(length) == 8 + row * height) {
            // Linhas de baixo para cima: no GL, v = 0 e o pe da imagem.
            out.resize(row * height);
            for (int y = 0; y < height; ++y) {
                env->GetByteArrayRegion(bytes, static_cast<jsize>(8 + row * y), static_cast<jsize>(row),
                                        reinterpret_cast<jbyte*>(out.data() + row * (height - 1 - y)));
            }
            ok = true;
        }
    }
    env->DeleteLocalRef(bytes);
    return ok;
}

bool upload(Entry& e) {
    auto& g = gl();
    GlStateGuard saved;
    g.GenTextures(1, &e.gl.texture);
    g.BindBuffer(GL_PIXEL_UNPACK_BUFFER, 0);
    g.ActiveTexture(kScreenUnit);
    g.BindTexture(GL_TEXTURE_2D, e.gl.texture);
    // O desempacotamento e da Unity (ela guarda em cache): zera o que o envio
    // precisa e devolve depois.
    const GLenum unpack[4] = {GL_UNPACK_ALIGNMENT, GL_UNPACK_ROW_LENGTH, GL_UNPACK_SKIP_ROWS, GL_UNPACK_SKIP_PIXELS};
    GLint previous[4] = {4, 0, 0, 0};
    for (int i = 0; i < 4; ++i) g.GetIntegerv(unpack[i], &previous[i]);
    g.PixelStorei(GL_UNPACK_ALIGNMENT, 4);
    for (int i = 1; i < 4; ++i) g.PixelStorei(unpack[i], 0);
    g.TexImage2D(GL_TEXTURE_2D, 0, GL_RGBA8, e.gl.width, e.gl.height, 0, GL_RGBA, GL_UNSIGNED_BYTE, e.pixels.data());
    for (int i = 0; i < 4; ++i) g.PixelStorei(unpack[i], previous[i]);

    const GLint filter = e.linear ? GL_LINEAR : GL_NEAREST;
    const GLint wrap = e.repeat ? GL_REPEAT : GL_CLAMP_TO_EDGE;
    g.GenSamplers(1, &e.gl.sampler);
    g.SamplerParameteri(e.gl.sampler, GL_TEXTURE_MIN_FILTER, filter);
    g.SamplerParameteri(e.gl.sampler, GL_TEXTURE_MAG_FILTER, filter);
    g.SamplerParameteri(e.gl.sampler, GL_TEXTURE_WRAP_S, wrap);
    g.SamplerParameteri(e.gl.sampler, GL_TEXTURE_WRAP_T, wrap);
    e.pixels.clear();
    e.pixels.shrink_to_fit();
    return true;
}

} // namespace

int TextureRegistry::Load(const std::string& path, bool linear, bool repeat, int* width, int* height) {
    {
        std::lock_guard<std::mutex> lock(g_mutex);
        for (size_t i = 0; i < g_entries.size(); ++i) {
            const Entry& e = *g_entries[i];
            if (e.path != path || e.linear != linear || e.repeat != repeat || e.failed) continue;
            if (width) *width = e.gl.width;
            if (height) *height = e.gl.height;
            return kFirstModTexture - static_cast<int>(i);
        }
    }

    auto e = std::make_unique<Entry>();
    e->path = path;
    e->linear = linear;
    e->repeat = repeat;
    int w = 0, h = 0;
    if (!decode(path, e->pixels, w, h)) {
        BL_ERROR("screen effects: texture '%s' could not be loaded (missing file or not an image)", path.c_str());
        return 0;
    }
    e->gl.width = w;
    e->gl.height = h;
    if (width) *width = w;
    if (height) *height = h;

    std::lock_guard<std::mutex> lock(g_mutex);
    g_entries.push_back(std::move(e));
    return kFirstModTexture - static_cast<int>(g_entries.size() - 1);
}

const LoadedTexture* TextureRegistry::Use(int id) {
    Entry* e = nullptr;
    {
        std::lock_guard<std::mutex> lock(g_mutex);
        const int index = kFirstModTexture - id;
        if (index < 0 || index >= static_cast<int>(g_entries.size())) return nullptr;
        e = g_entries[index].get();
        if (e->failed) return nullptr;
        if (e->uploaded) return &e->gl;
    }
    // O envio e da thread de render e so ela muda `uploaded`; os pixels nao
    // mudam mais depois do Load.
    e->uploaded = upload(*e);
    return e->uploaded ? &e->gl : nullptr;
}

bool TextureRegistry::SaveNeutralLut(const std::string& path, int size) {
    JNIEnv* env = appJniEnv();
    jclass cls = env ? appJniClass(env, "dev.bunnyloader.game.ModImages") : nullptr;
    jmethodID id = cls ? env->GetStaticMethodID(cls, "saveNeutralLut", "(Ljava/lang/String;I)Z") : nullptr;
    if (!id || !appJniCleared(env, "saveNeutralLut")) {
        if (cls) env->DeleteLocalRef(cls);
        return false;
    }
    jstring text = env->NewStringUTF(path.c_str());
    const bool ok = env->CallStaticBooleanMethod(cls, id, text, static_cast<jint>(size)) == JNI_TRUE;
    env->DeleteLocalRef(text);
    env->DeleteLocalRef(cls);
    return appJniCleared(env, "saveNeutralLut") && ok;
}

} // namespace bl::graphics
