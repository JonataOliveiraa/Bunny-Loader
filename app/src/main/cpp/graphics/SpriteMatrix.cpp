#include "graphics/SpriteMatrix.h"
#include "core/Log.h"
#include "hook/HookManager.h"
#include "il2cpp/Api.h"
#include "il2cpp/Resolver.h"

#include <cstring>
#include <mutex>

namespace bl::graphics {

namespace {

struct Slot {
    Il2CppObject* batcher = nullptr;
    float m11 = 1, m12 = 0, m21 = 0, m22 = 1, m41 = 0, m42 = 0;
    int32_t start = 0;
    bool immediate = false;
};

constexpr int kSlots = 16;
constexpr int32_t kArrayLength = 0x18;
constexpr int32_t kArrayData = 0x20;

Slot g_slots[kSlots];
int32_t g_pending = 0;
std::mutex g_mutex;

struct Offsets {
    int32_t batcher = -1;
    int32_t itemList = -1;
    int32_t itemCount = -1;
    int32_t vertex[4] = {-1, -1, -1, -1};
} g_off;

using EndFn = void (*)(Il2CppObject* self, const MethodInfo*);
using ImmediateFn = void (*)(Il2CppObject* self, Il2CppObject* effect, const MethodInfo*);

EndFn g_origEnd = nullptr;
ImmediateFn g_origImmediate = nullptr;

template <typename T>
T& at(void* object, int32_t offset) {
    return *reinterpret_cast<T*>(static_cast<char*>(object) + offset);
}

int32_t pending() { return __atomic_load_n(&g_pending, __ATOMIC_ACQUIRE); }

Il2CppObject* batcherOf(void* batch) { return batch ? at<Il2CppObject*>(batch, g_off.batcher) : nullptr; }

int find(Il2CppObject* batcher) {
    for (int i = 0; i < kSlots; ++i)
        if (g_slots[i].batcher == batcher) return i;
    return -1;
}

bool take(Il2CppObject* batcher, Slot& out) {
    std::lock_guard<std::mutex> lock(g_mutex);
    int i = find(batcher);
    if (i < 0) return false;
    out = g_slots[i];
    g_slots[i].batcher = nullptr;
    __atomic_sub_fetch(&g_pending, 1, __ATOMIC_RELEASE);
    return true;
}

bool peek(Il2CppObject* batcher, Slot& out) {
    std::lock_guard<std::mutex> lock(g_mutex);
    int i = find(batcher);
    if (i < 0) return false;
    out = g_slots[i];
    return true;
}

void transform(Il2CppObject* batcher, int32_t first, int32_t end, const Slot& s) {
    char* items = at<char*>(batcher, g_off.itemList);
    if (!items) return;

    int32_t length = static_cast<int32_t>(at<int64_t>(items, kArrayLength));
    if (end > length) end = length;
    auto** data = reinterpret_cast<char**>(items + kArrayData);

    for (int32_t i = first; i < end; ++i) {
        char* item = data[i];
        if (!item) continue;

        for (int32_t offset : g_off.vertex) {
            auto* p = reinterpret_cast<float*>(item + offset);
            float x = p[0], y = p[1];
            p[0] = x * s.m11 + y * s.m21 + s.m41;
            p[1] = x * s.m12 + y * s.m22 + s.m42;
        }
    }
}

void hkEnd(Il2CppObject* self, const MethodInfo* method) {
    if (pending() != 0 && self) {
        Il2CppObject* batcher = batcherOf(self);
        Slot s;
        if (batcher && take(batcher, s) && !s.immediate) {
            int32_t count = at<int32_t>(batcher, g_off.itemCount);
            transform(batcher, s.start <= count ? s.start : 0, count, s);
        }
    }

    g_origEnd(self, method);
}

void hkImmediate(Il2CppObject* self, Il2CppObject* effect, const MethodInfo* method) {
    if (pending() != 0 && self) {
        Slot s;
        if (peek(self, s) && s.immediate) transform(self, 0, 1, s);
    }

    g_origImmediate(self, effect, method);
}

const MethodInfo* findMethod(Il2CppClass* cls, const char* name, uint32_t params) {
    auto& a = il2cpp::api();
    void* iter = nullptr;

    while (const MethodInfo* m = a.class_get_methods(cls, &iter))
        if (std::strcmp(a.method_get_name(m), name) == 0 && a.method_get_param_count(m) == params) return m;

    return nullptr;
}

bool setup() {
    static int state = 0;
    if (state != 0) return state > 0;
    state = -1;

    const char* xna = "Microsoft.Xna.Framework.Graphics";
    Il2CppClass* batch = il2cpp::findClass({xna, "SpriteBatch", {}});
    Il2CppClass* batcher = il2cpp::findClass({xna, "SpriteBatcher", {}});
    Il2CppClass* item = il2cpp::findClass({xna, "SpriteBatchItem", {}});
    if (!batch || !batcher || !item) {
        BL_ERROR("sprite matrix: game classes missing");
        return false;
    }

    g_off.batcher = il2cpp::fieldOffset(batch, "_batcher");
    g_off.itemList = il2cpp::fieldOffset(batcher, "_batchItemList");
    g_off.itemCount = il2cpp::fieldOffset(batcher, "_batchItemCount");
    g_off.vertex[0] = il2cpp::fieldOffset(item, "vertexTL");
    g_off.vertex[1] = il2cpp::fieldOffset(item, "vertexTR");
    g_off.vertex[2] = il2cpp::fieldOffset(item, "vertexBL");
    g_off.vertex[3] = il2cpp::fieldOffset(item, "vertexBR");

    const MethodInfo* end = findMethod(batch, "End", 0);
    const MethodInfo* immediate = findMethod(batcher, "DrawImmediateBatch", 1);

    bool fields = g_off.batcher >= 0 && g_off.itemList >= 0 && g_off.itemCount >= 0;
    for (int32_t offset : g_off.vertex) fields = fields && offset >= 0;
    if (!fields || !end || !immediate) {
        BL_ERROR("sprite matrix: game methods or fields missing");
        return false;
    }

    if (!hook::install(end, hkEnd, &g_origEnd) || !hook::install(immediate, hkImmediate, &g_origImmediate)) {
        BL_ERROR("sprite matrix: game methods not hooked");
        return false;
    }

    state = 1;
    return true;
}

}

int32_t spriteMatrixBegin(void* batch, const float* matrix, int32_t immediate) {
    if (!batch || !matrix || !setup()) return 0;
    Il2CppObject* batcher = batcherOf(batch);
    if (!batcher) return 0;

    Slot s;
    s.batcher = batcher;
    s.m11 = matrix[0];
    s.m12 = matrix[1];
    s.m21 = matrix[2];
    s.m22 = matrix[3];
    s.m41 = matrix[4];
    s.m42 = matrix[5];
    s.start = at<int32_t>(batcher, g_off.itemCount);
    s.immediate = immediate != 0;

    std::lock_guard<std::mutex> lock(g_mutex);
    int i = find(batcher);
    if (i < 0) {
        i = find(nullptr);
        if (i < 0) return -1;
        __atomic_add_fetch(&g_pending, 1, __ATOMIC_RELEASE);
    }

    g_slots[i] = s;
    return 1;
}

int32_t spriteMatrixGet(void* batch, float* matrix) {
    if (pending() == 0 || !batch || !matrix) return 0;
    Slot s;
    if (!peek(batcherOf(batch), s)) return 0;

    matrix[0] = s.m11;
    matrix[1] = s.m12;
    matrix[2] = s.m21;
    matrix[3] = s.m22;
    matrix[4] = s.m41;
    matrix[5] = s.m42;
    return 1;
}

void spriteMatrixDrop(void* batch) {
    if (pending() == 0 || !batch) return;
    Slot s;
    take(batcherOf(batch), s);
}

int32_t* spriteMatrixPending() { return &g_pending; }

}
