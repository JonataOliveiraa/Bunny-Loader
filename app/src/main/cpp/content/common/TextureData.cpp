#include "content/common/TextureData.h"

#include <mutex>
#include <string>
#include <vector>

#include "content/common/ContentAssets.h"
#include "content/common/PngDecode.h"
#include "il2cpp/Api.h"
#include "il2cpp/Resolver.h"
#include "il2cpp/Signature.h"
#include "core/Log.h"

namespace bl::runtime::content {

namespace {

constexpr int kRgba32 = 4;
constexpr int kArgb32 = 5;
constexpr int kBgra32 = 14;

struct Refs {
    bool tried = false, ok = false;
    int32_t unity = -1, alpha = -1, palette = -1, atlas = -1, renderTexture = -1, width = -1, height = -1;
    const MethodInfo* isReadable = nullptr;
    const MethodInfo* format = nullptr;
    const MethodInfo* unityWidth = nullptr;
    const MethodInfo* unityHeight = nullptr;
    const MethodInfo* writableData = nullptr;
    const MethodInfo* rawDataSize = nullptr;
    const MethodInfo* apply = nullptr;
};

const Refs& refs() {
    static Refs r;
    static std::once_flag once;
    std::call_once(once, [] {
        using namespace il2cpp;
        auto& a = api();
        Il2CppClass* game = findClass({"Microsoft.Xna.Framework.Graphics", "Texture2D", {}});
        Il2CppClass* unityTexture = findClass({"UnityEngine", "Texture", {}});
        Il2CppClass* unityTexture2D = findClass({"UnityEngine", "Texture2D", {}});

        if (game) {
            r.unity = fieldOffset(game, "_unityTexture");
            r.alpha = fieldOffset(game, "_unityAlphaTexture");
            r.palette = fieldOffset(game, "_unityPalTexture");
            r.atlas = fieldOffset(game, "_textureAtlas");
            r.renderTexture = fieldOffset(game, "_unityRenderTexture");
            r.width = fieldOffset(game, "Width");
            r.height = fieldOffset(game, "Height");
        }
        if (unityTexture) {
            r.isReadable = a.class_get_method_from_name(unityTexture, "get_isReadable", 0);
            r.unityWidth = a.class_get_method_from_name(unityTexture, "get_width", 0);
            r.unityHeight = a.class_get_method_from_name(unityTexture, "get_height", 0);
        }
        if (unityTexture2D) {
            r.format = a.class_get_method_from_name(unityTexture2D, "get_format", 0);
            r.writableData = a.class_get_method_from_name(unityTexture2D, "GetWritableImageData", 1);
            r.rawDataSize = a.class_get_method_from_name(unityTexture2D, "GetRawImageDataSize", 0);
            r.apply = findMethodBySignature(unityTexture2D, parseSignature("void Apply(bool updateMipmaps, bool makeNoLongerReadable)"));
        }

        r.ok = r.unity >= 0 && r.alpha >= 0 && r.palette >= 0 && r.atlas >= 0 && r.renderTexture >= 0 &&
               r.width >= 0 && r.height >= 0 && r.isReadable && r.format && r.unityWidth && r.unityHeight &&
               r.writableData && r.rawDataSize && r.apply;
        if (!r.ok) BL_ERROR("texture data: game or Unity members missing, SetData and GetData are unavailable");
    });
    return r;
}

Il2CppObject* field(Il2CppObject* obj, int32_t offset) {
    return *reinterpret_cast<Il2CppObject**>(reinterpret_cast<char*>(obj) + offset);
}

int32_t intField(Il2CppObject* obj, int32_t offset) {
    return *reinterpret_cast<int32_t*>(reinterpret_cast<char*>(obj) + offset);
}

bool call(const MethodInfo* method, Il2CppObject* self, void** args, Il2CppObject** result) {
    Il2CppObject* error = nullptr;
    Il2CppObject* value = il2cpp::api().runtime_invoke(method, self, args, &error);
    if (error) return false;
    if (result) *result = value;
    return true;
}

template <typename T>
bool callValue(const MethodInfo* method, Il2CppObject* self, void** args, T* out) {
    Il2CppObject* boxed = nullptr;
    if (!call(method, self, args, &boxed) || !boxed) return false;
    *out = *reinterpret_cast<T*>(reinterpret_cast<char*>(boxed) + sizeof(Il2CppObject));
    return true;
}

struct Pixels {
    Il2CppObject* unity = nullptr;
    uint8_t* data = nullptr;
    int width = 0, height = 0, format = 0;
    bool mipmaps = false;
};

TextureDataResult pixelsOf(Il2CppObject* texture, Pixels* out) {
    const Refs& r = refs();
    if (!r.ok) return TextureDataResult::Failed;
    if (!texture) return TextureDataResult::NoTexture;
    if (field(texture, r.atlas) || field(texture, r.palette) || field(texture, r.alpha) || field(texture, r.renderTexture))
        return TextureDataResult::NotOwned;

    Il2CppObject* unity = field(texture, r.unity);
    if (!unity) return TextureDataResult::NotOwned;

    bool readable = false;
    if (!callValue(r.isReadable, unity, nullptr, &readable)) return TextureDataResult::Failed;
    if (!readable) return TextureDataResult::NotReadable;

    int format = 0, width = 0, height = 0;
    if (!callValue(r.format, unity, nullptr, &format) || !callValue(r.unityWidth, unity, nullptr, &width) ||
        !callValue(r.unityHeight, unity, nullptr, &height))
        return TextureDataResult::Failed;
    if (format != kRgba32 && format != kArgb32 && format != kBgra32) return TextureDataResult::UnsupportedFormat;
    if (width != intField(texture, r.width) || height != intField(texture, r.height)) return TextureDataResult::NotOwned;

    int frame = 0;
    void* args[1] = {&frame};
    void* data = nullptr;
    uint64_t size = 0;
    if (!callValue(r.writableData, unity, args, &data) || !callValue(r.rawDataSize, unity, nullptr, &size) || !data)
        return TextureDataResult::Failed;
    const uint64_t level0 = static_cast<uint64_t>(width) * static_cast<uint64_t>(height) * 4u;
    if (size < level0) return TextureDataResult::Failed;

    out->unity = unity;
    out->data = static_cast<uint8_t*>(data);
    out->width = width;
    out->height = height;
    out->format = format;
    out->mipmaps = size > level0;
    return TextureDataResult::Ok;
}

void toUnity(const uint8_t* rgba, uint8_t* dst, int format) {
    switch (format) {
        case kArgb32: dst[0] = rgba[3]; dst[1] = rgba[0]; dst[2] = rgba[1]; dst[3] = rgba[2]; break;
        case kBgra32: dst[0] = rgba[2]; dst[1] = rgba[1]; dst[2] = rgba[0]; dst[3] = rgba[3]; break;
        default: dst[0] = rgba[0]; dst[1] = rgba[1]; dst[2] = rgba[2]; dst[3] = rgba[3]; break;
    }
}

void fromUnity(const uint8_t* src, uint8_t* rgba, int format) {
    switch (format) {
        case kArgb32: rgba[0] = src[1]; rgba[1] = src[2]; rgba[2] = src[3]; rgba[3] = src[0]; break;
        case kBgra32: rgba[0] = src[2]; rgba[1] = src[1]; rgba[2] = src[0]; rgba[3] = src[3]; break;
        default: rgba[0] = src[0]; rgba[1] = src[1]; rgba[2] = src[2]; rgba[3] = src[3]; break;
    }
}

void toRgba(const uint8_t* src, uint8_t* rgba, PixelOrder order) {
    if (order == PixelOrder::Abgr) {
        rgba[0] = src[3]; rgba[1] = src[2]; rgba[2] = src[1]; rgba[3] = src[0];
    } else {
        rgba[0] = src[0]; rgba[1] = src[1]; rgba[2] = src[2]; rgba[3] = src[3];
    }
}

void fromRgba(const uint8_t* rgba, uint8_t* dst, PixelOrder order) {
    if (order == PixelOrder::Abgr) {
        dst[0] = rgba[3]; dst[1] = rgba[2]; dst[2] = rgba[1]; dst[3] = rgba[0];
    } else {
        dst[0] = rgba[0]; dst[1] = rgba[1]; dst[2] = rgba[2]; dst[3] = rgba[3];
    }
}

bool regionFits(const Pixels& p, int x, int y, int width, int height) {
    return x >= 0 && y >= 0 && width > 0 && height > 0 && x + width <= p.width && y + height <= p.height;
}

uint8_t* unityPixel(const Pixels& p, int x, int y) {
    const size_t row = static_cast<size_t>(p.height - 1 - y);
    return p.data + (row * static_cast<size_t>(p.width) + static_cast<size_t>(x)) * 4u;
}

std::mutex pinLock;
std::vector<uint32_t> pins;

Il2CppObject* pin(Il2CppObject* texture) {
    if (!texture) return nullptr;
    std::lock_guard<std::mutex> lock(pinLock);
    pins.push_back(il2cpp::api().gchandle_new(texture, false));
    return texture;
}

}

Il2CppObject* createTexture(int width, int height) {
    if (width <= 0 || height <= 0) return nullptr;
    std::vector<uint8_t> clear(static_cast<size_t>(width) * static_cast<size_t>(height) * 4u, 0);
    return pin(gameTextureFromPixels(clear.data(), width, height, "bl/Texture2D"));
}

Il2CppObject* textureFromPng(const uint8_t* data, size_t size) {
    if (!data || size == 0) return nullptr;
    int width = 0, height = 0;
    std::vector<uint8_t> rgba;
    std::string error;
    if (!decodePng(data, size, true, &width, &height, rgba, &error)) {
        BL_WARN("texture data: FromStream could not decode the image: %s", error.c_str());
        return nullptr;
    }
    return pin(gameTextureFromPixels(rgba.data(), width, height, "bl/Texture2D.FromStream"));
}

TextureDataResult writeTexture(Il2CppObject* texture, int x, int y, int width, int height, const uint8_t* pixels, PixelOrder order) {
    Pixels p;
    TextureDataResult result = pixelsOf(texture, &p);
    if (result != TextureDataResult::Ok) return result;
    if (!pixels || !regionFits(p, x, y, width, height)) return TextureDataResult::BadRegion;

    uint8_t rgba[4];
    for (int row = 0; row < height; ++row) {
        const uint8_t* src = pixels + static_cast<size_t>(row) * static_cast<size_t>(width) * 4u;
        uint8_t* dst = unityPixel(p, x, y + row);
        for (int col = 0; col < width; ++col) {
            toRgba(src + col * 4, rgba, order);
            toUnity(rgba, dst + col * 4, p.format);
        }
    }

    uint8_t updateMipmaps = p.mipmaps ? 1 : 0, keepReadable = 0;
    void* args[2] = {&updateMipmaps, &keepReadable};
    return call(refs().apply, p.unity, args, nullptr) ? TextureDataResult::Ok : TextureDataResult::Failed;
}

TextureDataResult readTexture(Il2CppObject* texture, int x, int y, int width, int height, uint8_t* pixels, PixelOrder order) {
    Pixels p;
    TextureDataResult result = pixelsOf(texture, &p);
    if (result != TextureDataResult::Ok) return result;
    if (!pixels || !regionFits(p, x, y, width, height)) return TextureDataResult::BadRegion;

    uint8_t rgba[4];
    for (int row = 0; row < height; ++row) {
        const uint8_t* src = unityPixel(p, x, y + row);
        uint8_t* dst = pixels + static_cast<size_t>(row) * static_cast<size_t>(width) * 4u;
        for (int col = 0; col < width; ++col) {
            fromUnity(src + col * 4, rgba, p.format);
            fromRgba(rgba, dst + col * 4, order);
        }
    }
    return TextureDataResult::Ok;
}

}
