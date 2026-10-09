#include "script/api/ItemDrawHooks.h"
#include "script/bridge/ScriptEngine.h"
#if BL_HAVE_QUICKJS && defined(__aarch64__)
#include "script/bridge/Bridge.h"
#include "script/bridge/Value.h"
#include "il2cpp/Resolver.h"
#include "il2cpp/Signature.h"
#include "hook/HookManager.h"
#include "hook/Arm64Frame.h"
#include "core/Log.h"
#include "quickjs.h"
#include <cmath>
#include <cstring>

namespace {
struct Stage {
    size_t offset;
    uint32_t expected[4];
    void (*bridge)();
    void* original = nullptr;
    void* entry = nullptr;
};
struct Pair {
    const char* name;
    const char* space;
    const char* owner;
    const char* signature;
    Stage pre, post;
    std::atomic<uint8_t>* active = nullptr;
    JSContext* context = nullptr;
    JSValue before = JS_UNDEFINED, after = JS_UNDEFINED;
};
extern Pair pairs[2];
Il2CppClass *colorClass = nullptr, *vectorClass = nullptr, *rectangleClass = nullptr;
thread_local bool inCallback[2] = {};

float scalar(const RegisterFrame* frame, unsigned index) {
    float value; std::memcpy(&value, frame->vectors[index], sizeof(value)); return value;
}
void scalar(RegisterFrame* frame, unsigned index, float value) { std::memcpy(frame->vectors[index], &value, sizeof(value)); }
JSValue copy(JSContext* ctx, Il2CppClass* cls, const void* data, size_t size) {
    return bl::script::makeStructCopy(ctx, cls, data, size);
}
void report(JSContext* ctx, const char* name) {
    JSValue error = JS_GetException(ctx);
    const char* text = JS_ToCString(ctx, error);
    BL_ERROR("ItemDraw.%s: %s", name, text ? text : "erro no callback");
    if (text) JS_FreeCString(ctx, text);
    JS_FreeValue(ctx, error);
}

// Em ambos os métodos o tML insere Pre após calcular luz/quadro/escala,
// e Post antes do epílogo. false salta diretamente para Post, pulando
// também partículas, glowmasks e notificações do desenho vanilla.
extern "C" __attribute__((visibility("hidden"))) void* blItemDrawDispatch(unsigned index, RegisterFrame* frame) {
    Pair& pair = pairs[index / 2];
    const bool post = index & 1;
    Stage& stage = post ? pair.post : pair.pre;
    if (!pair.context || !pair.active->load(std::memory_order_relaxed) || inCallback[index / 2]) return stage.original;
    bl::script::JsLock lock;
    if (!lock.held()) return stage.original;
    struct CallbackGuard {
        bool& active;
        explicit CallbackGuard(bool& value) : active(value) { active = true; }
        ~CallbackGuard() { active = false; }
    } guard(inCallback[index / 2]);
    JSContext* ctx = pair.context;
    if (post) {
        JSValue result = JS_Call(ctx, pair.after, JS_UNDEFINED, 0, nullptr);
        if (JS_IsException(result)) report(ctx, pair.name);
        JS_FreeValue(ctx, result);
        return stage.original;
    }
    auto* stack = reinterpret_cast<uint8_t*>(frame) + sizeof(RegisterFrame);
    JSValue args[8];
    int count;
    if (index == 0) {
        args[0] = copy(ctx, colorClass, &frame->general[23], 4);
        args[1] = copy(ctx, colorClass, stack + 0x248, 4);
        args[2] = JS_NewFloat64(ctx, scalar(frame, 11));
        float scale; std::memcpy(&scale, stack + 0x24c, 4);
        args[3] = JS_NewFloat64(ctx, scale);
        count = 4;
    } else {
        const float position[2] = {scalar(frame, 10), scalar(frame, 9)};
        const float origin[2] = {scalar(frame, 15), scalar(frame, 8)};
        args[0] = bl::script::makeNativeObject(ctx, reinterpret_cast<Il2CppObject*>(frame->general[21]));
        args[1] = bl::script::makeNativeObject(ctx, reinterpret_cast<Il2CppObject*>(frame->general[29]));
        args[2] = copy(ctx, vectorClass, position, 8);
        args[3] = copy(ctx, rectangleClass, stack + 0x118, 16);
        args[4] = copy(ctx, colorClass, stack + 0x114, 4);
        args[5] = copy(ctx, colorClass, &frame->general[22], 4);
        args[6] = copy(ctx, vectorClass, origin, 8);
        float scale; std::memcpy(&scale, stack + 0x110, 4);
        args[7] = JS_NewFloat64(ctx, scale);
        count = 8;
    }
    JSValue result = JS_Call(ctx, pair.before, JS_UNDEFINED, count, args);
    bool go = true;
    if (JS_IsException(result)) report(ctx, pair.name);
    else if (index == 0 && JS_IsArray(result)) {
        JSValue allowed = JS_GetPropertyUint32(ctx, result, 0);
        go = !JS_IsBool(allowed) || JS_ToBool(ctx, allowed);
        JS_FreeValue(ctx, allowed);
        for (unsigned i = 1; i <= 2; i++) {
            JSValue value = JS_GetPropertyUint32(ctx, result, i);
            double number;
            if (JS_IsNumber(value) && !JS_ToFloat64(ctx, &number, value) && std::isfinite(number)) {
                const float changed = static_cast<float>(number);
                if (std::isfinite(changed)) {
                    if (i == 1) scalar(frame, 11, changed);
                    else std::memcpy(stack + 0x24c, &changed, 4);
                }
            }
            JS_FreeValue(ctx, value);
        }
    } else go = !JS_IsBool(result) || JS_ToBool(ctx, result);
    JS_FreeValue(ctx, result);
    for (int i = 0; i < count; i++) JS_FreeValue(ctx, args[i]);
    if (!go && index == 2) {
        // O corpo normal carrega o retorno em s13 mais tarde. Ao saltá-lo,
        // devolvemos a mesma finalDrawScale calculada antes de Pre.
        float scale; std::memcpy(&scale, stack + 0x110, 4);
        scalar(frame, 13, scale);
    }
    return go ? stage.original : pair.post.entry;
}

#define BL_ITEM_BRIDGE(name, index) \
__attribute__((naked)) void name() { \
    __asm__ volatile(BL_ARM64_SAVE_FRAME \
        "mov x1, sp\n" "mov w0, #" #index "\n" "bl blItemDrawDispatch\n" \
        "str x0, [sp, #128]\n" BL_ARM64_RESTORE_FRAME "br x16\n"); \
}
BL_ITEM_BRIDGE(worldBefore, 0)
BL_ITEM_BRIDGE(worldAfter, 1)
BL_ITEM_BRIDGE(inventoryBefore, 2)
BL_ITEM_BRIDGE(inventoryAfter, 3)
#undef BL_ITEM_BRIDGE

Pair pairs[2] = {
    {"World", "Terraria", "Main", "void DrawItem(WorldItem item, int whoami)",
        {0x418, {0x79c14108, 0xd000fdd9, 0xaa1f03e0, 0xb9001be8}, worldBefore}, {0x20a8, {0x910a03ff, 0xa9497bfd, 0xa9484ff4, 0xa94757f6}, worldAfter}},
    {"Inventory", "Terraria.UI", "ItemSlot", "float DrawItemIcon(Item item, int context, SpriteBatch spriteBatch, Vector2 screenPositionForItemCenter, float scale, float sizeLimit, Color environmentColor, float itemFade, bool flip)",
        {0x380, {0x910363e0, 0xaa1703e1, 0xaa1803e2, 0xa90dffff}, inventoryBefore}, {0x1198, {0x4ead1da0, 0xa95c7bfd, 0xa95b4ff4, 0xa95a57f6}, inventoryAfter}}
};

JSValue install(JSContext* ctx, JSValueConst, int argc, JSValueConst* argv) {
    if (argc != 3 || !JS_IsString(argv[0]) || !JS_IsFunction(ctx, argv[1]) || !JS_IsFunction(ctx, argv[2]))
        return JS_ThrowTypeError(ctx, "installItemDrawHooks(World|Inventory, pre, post)");
    const char* name = JS_ToCString(ctx, argv[0]);
    if (!name) return JS_EXCEPTION;
    Pair* pair = nullptr;
    for (auto& candidate : pairs) if (!std::strcmp(candidate.name, name)) pair = &candidate;
    JS_FreeCString(ctx, name);
    if (!pair || pair->context) return JS_ThrowTypeError(ctx, "ItemDraw: etapa desconhecida ou já instalada");
    using namespace bl::il2cpp;
    auto* cls = findClass({pair->space, pair->owner, ""});
    auto* method = cls ? findMethodBySignature(cls, parseSignature(pair->signature)) : nullptr;
    auto* entry = method ? static_cast<uint8_t*>(methodPointer(method)) : nullptr;
    for (Stage* stage : {&pair->pre, &pair->post}) {
        if (!entry || std::memcmp(entry + stage->offset, stage->expected, sizeof(stage->expected)))
            return JS_ThrowInternalError(ctx, "ItemDraw.%s: corpo nativo incompatível", pair->name);
        stage->entry = entry + stage->offset;
    }
    colorClass = findClass({"Microsoft.Xna.Framework.Graphics", "Color", ""});
    vectorClass = findClass({"Microsoft.Xna.Framework", "Vector2", ""});
    rectangleClass = findClass({"Microsoft.Xna.Framework", "Rectangle", ""});
    if (!colorClass || !vectorClass || !rectangleClass) return JS_ThrowInternalError(ctx, "ItemDraw: estruturas nativas ausentes");
    for (Stage* stage : {&pair->post, &pair->pre}) {
        if (!bl::hook::install(stage->entry, reinterpret_cast<void*>(stage->bridge), &stage->original))
            return JS_ThrowInternalError(ctx, "ItemDraw.%s: hook nativo recusado", pair->name);
    }
    pair->active = bl::script::hookFlag(std::string("item.Draw") + pair->name);
    pair->before = JS_DupValue(ctx, argv[1]);
    pair->after = JS_DupValue(ctx, argv[2]);
    pair->context = ctx;
    return JS_UNDEFINED;
}
}
namespace bl::script {
void installItemDrawBindings(void* context) {
    auto* ctx = static_cast<JSContext*>(context);
    JSValue global = JS_GetGlobalObject(ctx), api = JS_GetPropertyStr(ctx, global, "bl");
    JS_SetPropertyStr(ctx, api, "installItemDrawHooks", JS_NewCFunction(ctx, install, "installItemDrawHooks", 3));
    JS_FreeValue(ctx, api); JS_FreeValue(ctx, global);
}
}
#else
namespace bl::script { void installItemDrawBindings(void*) {} }
#endif
