#include "script/api/SystemHooks.h"
#include "script/bridge/ScriptEngine.h"
#if BL_HAVE_QUICKJS && defined(__aarch64__)
#include "il2cpp/Resolver.h"
#include "il2cpp/Signature.h"
#include "hook/HookManager.h"
#include "core/Log.h"
#include "quickjs.h"
#include <cmath>
#include <cstdint>
#include <cstring>
extern "C" {
__attribute__((visibility("hidden"))) void* g_blSystemOriginal0 = nullptr;
__attribute__((visibility("hidden"))) void* g_blSystemOriginal1 = nullptr;
__attribute__((visibility("hidden"))) void* g_blSystemOriginal2 = nullptr;
__attribute__((visibility("hidden"))) void* g_blSystemOriginal3 = nullptr;
__attribute__((visibility("hidden"))) void* g_blSystemOriginal4 = nullptr;
__attribute__((visibility("hidden"))) void* g_blSystemOriginal5 = nullptr;
__attribute__((visibility("hidden"))) void* g_blSystemOriginal6 = nullptr;
__attribute__((visibility("hidden"))) void* g_blSystemOriginal7 = nullptr;
__attribute__((visibility("hidden"))) void* g_blSystemOriginal8 = nullptr;
}
namespace {
struct alignas(16) RegisterFrame {
    uint64_t general[31];
    uint64_t flags;
    alignas(16) uint8_t vectors[32][16];
};
static_assert(sizeof(RegisterFrame) == 768);
struct Stage {
    const char* name;
    const char* signature;
    size_t offset;
    uint32_t expected[4];
    void (*bridge)();
    void** original;
    JSContext* context = nullptr;
    JSValue callback = JS_UNDEFINED;
};
extern Stage stages[9];
extern "C" __attribute__((visibility("hidden"))) void blSystemStageDispatch(unsigned index, RegisterFrame* frame) {
    if (index >= 9) return;
    bl::script::JsLock lock;
    Stage& stage = stages[index];
    if (!stage.context) return;
    JSValue result = JS_Call(stage.context, stage.callback, JS_UNDEFINED, 0, nullptr);
    if (JS_IsException(result)) {
        JSValue exception = JS_GetException(stage.context);
        const char* message = JS_ToCString(stage.context, exception);
        BL_ERROR("ModSystem.%s: %s", stage.name, message ? message : "erro no callback");
        if (message) JS_FreeCString(stage.context, message);
        JS_FreeValue(stage.context, exception);
    } else if (index == 8) {
        double value;
        if (!JS_ToFloat64(stage.context, &value, result)) {
            if (std::isfinite(value) && value >= 0) std::memcpy(frame->vectors[0], &value, sizeof(value));
        } else {
            JSValue exception = JS_GetException(stage.context);
            JS_FreeValue(stage.context, exception);
        }
    }
    JS_FreeValue(stage.context, result);
}
#define BL_SYSTEM_SAVE_FRAME \
"sub sp, sp, #768\n" \
"stp x0, x1, [sp, #0]\n" \
"stp x2, x3, [sp, #16]\n" \
"stp x4, x5, [sp, #32]\n" \
"stp x6, x7, [sp, #48]\n" \
"stp x8, x9, [sp, #64]\n" \
"stp x10, x11, [sp, #80]\n" \
"stp x12, x13, [sp, #96]\n" \
"stp x14, x15, [sp, #112]\n" \
"stp x16, x17, [sp, #128]\n" \
"stp x18, x19, [sp, #144]\n" \
"stp x20, x21, [sp, #160]\n" \
"stp x22, x23, [sp, #176]\n" \
"stp x24, x25, [sp, #192]\n" \
"stp x26, x27, [sp, #208]\n" \
"stp x28, x29, [sp, #224]\n" \
"str x30, [sp, #240]\n" \
"mrs x9, nzcv\n" \
"str x9, [sp, #248]\n" \
"stp q0, q1, [sp, #256]\n" \
"stp q2, q3, [sp, #288]\n" \
"stp q4, q5, [sp, #320]\n" \
"stp q6, q7, [sp, #352]\n" \
"stp q8, q9, [sp, #384]\n" \
"stp q10, q11, [sp, #416]\n" \
"stp q12, q13, [sp, #448]\n" \
"stp q14, q15, [sp, #480]\n" \
"stp q16, q17, [sp, #512]\n" \
"stp q18, q19, [sp, #544]\n" \
"stp q20, q21, [sp, #576]\n" \
"stp q22, q23, [sp, #608]\n" \
"stp q24, q25, [sp, #640]\n" \
"stp q26, q27, [sp, #672]\n" \
"stp q28, q29, [sp, #704]\n" \
"stp q30, q31, [sp, #736]\n"

#define BL_SYSTEM_RESTORE_FRAME \
"ldp q0, q1, [sp, #256]\n" \
"ldp q2, q3, [sp, #288]\n" \
"ldp q4, q5, [sp, #320]\n" \
"ldp q6, q7, [sp, #352]\n" \
"ldp q8, q9, [sp, #384]\n" \
"ldp q10, q11, [sp, #416]\n" \
"ldp q12, q13, [sp, #448]\n" \
"ldp q14, q15, [sp, #480]\n" \
"ldp q16, q17, [sp, #512]\n" \
"ldp q18, q19, [sp, #544]\n" \
"ldp q20, q21, [sp, #576]\n" \
"ldp q22, q23, [sp, #608]\n" \
"ldp q24, q25, [sp, #640]\n" \
"ldp q26, q27, [sp, #672]\n" \
"ldp q28, q29, [sp, #704]\n" \
"ldp q30, q31, [sp, #736]\n" \
"ldr x9, [sp, #248]\n" \
"msr nzcv, x9\n" \
"ldp x0, x1, [sp, #0]\n" \
"ldp x2, x3, [sp, #16]\n" \
"ldp x4, x5, [sp, #32]\n" \
"ldp x6, x7, [sp, #48]\n" \
"ldp x8, x9, [sp, #64]\n" \
"ldp x10, x11, [sp, #80]\n" \
"ldp x12, x13, [sp, #96]\n" \
"ldp x14, x15, [sp, #112]\n" \
"ldp x16, x17, [sp, #128]\n" \
"ldp x18, x19, [sp, #144]\n" \
"ldp x20, x21, [sp, #160]\n" \
"ldp x22, x23, [sp, #176]\n" \
"ldp x24, x25, [sp, #192]\n" \
"ldp x26, x27, [sp, #208]\n" \
"ldp x28, x29, [sp, #224]\n" \
"ldr x30, [sp, #240]\n" \
"add sp, sp, #768\n"

#define BL_SYSTEM_BRIDGE(name, stage, target) \
__attribute__((naked)) void name() { \
    __asm__ volatile(BL_SYSTEM_SAVE_FRAME \
        "mov x1, sp\n" "mov w0, #" #stage "\n" "bl blSystemStageDispatch\n" \
        BL_SYSTEM_RESTORE_FRAME \
        "adrp x16, " #target "\n" "ldr x16, [x16, :lo12:" #target "]\n" "br x16\n"); \
}

BL_SYSTEM_BRIDGE(bridge0, 0, g_blSystemOriginal0)
BL_SYSTEM_BRIDGE(bridge1, 1, g_blSystemOriginal1)
BL_SYSTEM_BRIDGE(bridge2, 2, g_blSystemOriginal2)
BL_SYSTEM_BRIDGE(bridge3, 3, g_blSystemOriginal3)
BL_SYSTEM_BRIDGE(bridge4, 4, g_blSystemOriginal4)
BL_SYSTEM_BRIDGE(bridge5, 5, g_blSystemOriginal5)
BL_SYSTEM_BRIDGE(bridge6, 6, g_blSystemOriginal6)
BL_SYSTEM_BRIDGE(bridge7, 7, g_blSystemOriginal7)
BL_SYSTEM_BRIDGE(bridge8, 8, g_blSystemOriginal8)

#undef BL_SYSTEM_BRIDGE
#undef BL_SYSTEM_RESTORE_FRAME
#undef BL_SYSTEM_SAVE_FRAME
Stage stages[9] = {
    {"PlayersBegin", "void DoUpdateInWorld()", 0x1b8, {0xd00100bd, 0xd001011a, 0xf00100f9, 0xd000ffdc}, bridge0, &g_blSystemOriginal0},
    {"PlayersEnd", "void DoUpdateInWorld()", 0x57c, {0xd0010028, 0xf940b108, 0x2a1f03f5, 0xf9400108}, bridge1, &g_blSystemOriginal1},
    {"NPCsBegin", "void DoUpdateInWorld()", 0x884, {0xaa1f03e0, 0x3900811f, 0x94063b0a, 0xaa1f03e0}, bridge2, &g_blSystemOriginal2},
    {"NPCsEnd", "void DoUpdateInWorld()", 0xf40, {0xd0010028, 0xf940b108, 0x120002c9, 0xd00100f5}, bridge3, &g_blSystemOriginal3},
    {"GoresEnd", "void DoUpdateInWorld()", 0x114c, {0x2a1f03f5, 0xf94003a0, 0xb940e008, 0x35000068}, bridge4, &g_blSystemOriginal4},
    {"ItemsBegin", "void DoUpdateInWorld()", 0x1740, {0xd000feb7, 0xf000fed8, 0xd000ff99, 0x9001017c}, bridge5, &g_blSystemOriginal5},
    {"ItemsEnd", "void DoUpdateInWorld()", 0x1a14, {0x9000ffc8, 0xf9437908, 0xb0010037, 0xd00100b6}, bridge6, &g_blSystemOriginal6},
    {"Transform", "void DoDraw(GameTime gameTime)", 0x2468, {0x97fc20b9, 0x37000220, 0xf9400360, 0xb940e008}, bridge7, &g_blSystemOriginal7},
    {"ClockAdvance", "void UpdateTime()", 0xb8c, {0x1e602960, 0xfd024500, 0x97efb85d, 0xf94002e0}, bridge8, &g_blSystemOriginal8}
};
JSValue installSystemStage(JSContext* context, JSValueConst, int argc, JSValueConst* argv) {
    if (argc != 2 || !JS_IsString(argv[0]) || !JS_IsFunction(context, argv[1])) return JS_ThrowTypeError(context, "installSystemStage(nome, funcao)");
    const char* name = JS_ToCString(context, argv[0]);
    if (!name) return JS_EXCEPTION;
    Stage* selected = nullptr;
    for (Stage& stage : stages) if (std::strcmp(stage.name, name) == 0) selected = &stage;
    JS_FreeCString(context, name);
    if (!selected) return JS_ThrowTypeError(context, "ModSystem: etapa desconhecida");
    if (selected->context) return JS_ThrowTypeError(context, "%s ja instalado", selected->name);
    if (!*selected->original) {
        auto* main = bl::il2cpp::findClass({"Terraria", "Main", ""});
        const MethodInfo* method = main ? bl::il2cpp::findMethodBySignature(main, bl::il2cpp::parseSignature(selected->signature)) : nullptr;
        const auto* entry = method ? static_cast<const uint8_t*>(bl::il2cpp::methodPointer(method)) : nullptr;
        if (!entry || std::memcmp(entry + selected->offset, selected->expected, sizeof(selected->expected)) != 0) {
            return JS_ThrowInternalError(context, "%s: corpo nativo incompativel", selected->name);
        }
        if (!bl::hook::install(const_cast<uint8_t*>(entry + selected->offset), reinterpret_cast<void*>(selected->bridge), selected->original)) {
            return JS_ThrowInternalError(context, "%s: hook nativo recusado", selected->name);
        }
    }
    selected->callback = JS_DupValue(context, argv[1]);
    selected->context = context;
    return JS_UNDEFINED;
}
}
namespace bl::script {
void installSystemBindings(void* context) {
    auto* ctx = static_cast<JSContext*>(context);
    JSValue global = JS_GetGlobalObject(ctx);
    JSValue api = JS_GetPropertyStr(ctx, global, "bl");
    JS_SetPropertyStr(ctx, api, "installSystemStage", JS_NewCFunction(ctx, installSystemStage, "installSystemStage", 2));
    JS_FreeValue(ctx, api);
    JS_FreeValue(ctx, global);
}
void releaseSystemBindings(void* context) {
    for (Stage& stage : stages) {
        if (stage.context != context) continue;
        JS_FreeValue(stage.context, stage.callback);
        stage.callback = JS_UNDEFINED;
        stage.context = nullptr;
    }
}
}
#else
namespace bl::script {
void installSystemBindings(void*) {}
void releaseSystemBindings(void*) {}
}
#endif
