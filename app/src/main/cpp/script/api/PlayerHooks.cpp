#include "script/api/PlayerHooks.h"
#include "script/bridge/ScriptEngine.h"

#if BL_HAVE_QUICKJS && defined(__aarch64__)
#include "script/bridge/Bridge.h"
#include "script/bridge/Value.h"
#include "il2cpp/Resolver.h"
#include "il2cpp/Signature.h"
#include "hook/HookManager.h"
#include "core/Log.h"
#include "quickjs.h"
#include <cmath>
#include <cstdint>
#include <cstring>

extern "C" {
__attribute__((visibility("hidden"))) void* g_blNaturalRegenOriginal = nullptr;
__attribute__((visibility("hidden"))) void* g_blConsumeBaitOriginal = nullptr;
__attribute__((visibility("hidden"))) void* g_blDyeRewardOriginal = nullptr;
__attribute__((visibility("hidden"))) void* g_blFishingConditionsOriginal = nullptr;
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
extern Stage stages[4];
Il2CppClass* fishingConditionsClass = nullptr;

struct FishingConditions {
    int32_t polePower;
    int32_t poleType;
    int32_t baitPower;
    int32_t baitType;
    float multiplier;
    int32_t level;
};
static_assert(sizeof(FishingConditions) == 24);

FishingConditions fishingConditionsBridge(Il2CppObject* player, const MethodInfo* method) {
    using Original = FishingConditions (*)(Il2CppObject*, const MethodInfo*);
    auto result = reinterpret_cast<Original>(g_blFishingConditionsOriginal)(player, method);
    bl::script::JsLock lock(0);
    Stage& stage = stages[3];
    if (!lock.held() || !stage.context || !fishingConditionsClass) return result;
    JSValue args[] = {
        bl::script::makeNativeObject(stage.context, player),
        bl::script::makeStructCopy(stage.context, fishingConditionsClass, &result, sizeof(result))
    };
    JSValue changed = JS_Call(stage.context, stage.callback, JS_UNDEFINED, 2, args);
    if (JS_IsException(changed)) {
        JSValue exception = JS_GetException(stage.context);
        const char* message = JS_ToCString(stage.context, exception);
        BL_ERROR("GetFishingLevel: %s", message ? message : "erro no callback");
        if (message) JS_FreeCString(stage.context, message);
        JS_FreeValue(stage.context, exception);
    } else {
        Il2CppClass* cls = nullptr;
        size_t size = 0;
        void* data = bl::script::structDataOf(changed, &cls, &size);
        if (data && cls == fishingConditionsClass && size == sizeof(result)) std::memcpy(&result, data, size);
    }
    JS_FreeValue(stage.context, changed);
    for (JSValue arg : args) JS_FreeValue(stage.context, arg);
    return result;
}

extern "C" __attribute__((visibility("hidden"))) void blPlayerStageDispatch(unsigned index, RegisterFrame* frame) {
    bl::script::JsLock lock(0);
    if (!lock.held() || index >= 3) return;
    Stage& stage = stages[index];
    if (!stage.context) return;
    float value = 0;
    if (index == 0) std::memcpy(&value, frame->vectors[9], sizeof(value));
    JSValue args[] = {
        bl::script::makeNativeObject(stage.context, reinterpret_cast<Il2CppObject*>(frame->general[19])),
        index == 0 ? JS_NewFloat64(stage.context, value) :
            bl::script::makeNativeObject(stage.context, reinterpret_cast<Il2CppObject*>(frame->general[21]))
    };
    JSValue result = JS_Call(stage.context, stage.callback, JS_UNDEFINED, 2, args);
    if (JS_IsException(result)) {
        JSValue exception = JS_GetException(stage.context);
        const char* message = JS_ToCString(stage.context, exception);
        BL_ERROR("%s: %s", stage.name, message ? message : "erro no callback");
        if (message) JS_FreeCString(stage.context, message);
        JS_FreeValue(stage.context, exception);
    } else if (index == 0) {
        double changed;
        if (!JS_ToFloat64(stage.context, &changed, result)) {
            value = static_cast<float>(changed);
            if (std::isfinite(value)) std::memcpy(frame->vectors[9], &value, sizeof(value));
        } else {
            JSValue exception = JS_GetException(stage.context);
            JS_FreeValue(stage.context, exception);
        }
    } else if (index == 1 && JS_IsBool(result)) {
        const bool consume = JS_ToBool(stage.context, result);
        frame->general[27] = consume ? 1 : 0;
        if (!consume) frame->general[8] = 0;
    }
    JS_FreeValue(stage.context, result);
    for (JSValue arg : args) JS_FreeValue(stage.context, arg);
}

#define BL_PLAYER_SAVE_FRAME \
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

#define BL_PLAYER_RESTORE_FRAME \
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

#define BL_PLAYER_BRIDGE(name, stage, target) \
__attribute__((naked)) void name() { \
    __asm__ volatile(BL_PLAYER_SAVE_FRAME \
        "mov x1, sp\n" "mov w0, #" #stage "\n" "bl blPlayerStageDispatch\n" \
        BL_PLAYER_RESTORE_FRAME \
        "adrp x16, " #target "\n" "ldr x16, [x16, :lo12:" #target "]\n" "br x16\n"); \
}

BL_PLAYER_BRIDGE(naturalRegenBridge, 0, g_blNaturalRegenOriginal)
BL_PLAYER_BRIDGE(consumeBaitBridge, 1, g_blConsumeBaitOriginal)
BL_PLAYER_BRIDGE(dyeRewardBridge, 2, g_blDyeRewardOriginal)

#undef BL_PLAYER_BRIDGE
#undef BL_PLAYER_RESTORE_FRAME
#undef BL_PLAYER_SAVE_FRAME

Stage stages[4] = {
    {"NaturalLifeRegen", "void UpdateLifeRegen()", 0x828,
        {0xbd481e60, 0x9000a269, 0xf9400300, 0x9000a26a}, naturalRegenBridge, &g_blNaturalRegenOriginal},
    {"CanConsumeBait", "bool ItemCheck_CheckFishingBobber_ConsumeBait(Projectile bobber, out int baitTypeUsed)", 0x2bc,
        {0x3700007b, 0x7129c51f, 0x54000421, 0xb9404ea8}, consumeBaitBridge, &g_blConsumeBaitOriginal},
    {"GetDyeTraderReward", "void GetDyeTraderReward(NPC dyeTrader)", 0xef0,
        {0xb9401aa1, 0xd000e2b6, 0xb000e238, 0xf94202d6}, dyeRewardBridge, &g_blDyeRewardOriginal},
    {"GetFishingLevel", "PlayerFishingConditions GetFishingConditions()", 0,
        {0xd10183ff, 0xf90013f8, 0xa9035bf7, 0xa90453f5}, reinterpret_cast<void (*)()>(fishingConditionsBridge), &g_blFishingConditionsOriginal}
};

JSValue installPlayerStage(JSContext* context, JSValueConst, int argc, JSValueConst* argv) {
    if (argc != 2 || !JS_IsString(argv[0]) || !JS_IsFunction(context, argv[1])) return JS_ThrowTypeError(context, "installPlayerStage(nome, funcao)");
    const char* name = JS_ToCString(context, argv[0]);
    if (!name) return JS_EXCEPTION;
    Stage* selected = nullptr;
    for (Stage& stage : stages) if (std::strcmp(stage.name, name) == 0) selected = &stage;
    JS_FreeCString(context, name);
    if (!selected) return JS_ThrowTypeError(context, "Player: etapa desconhecida");
    if (selected->context) return JS_ThrowTypeError(context, "%s ja instalado", selected->name);
    if (selected == &stages[3]) {
        fishingConditionsClass = bl::il2cpp::findClass({"Terraria.DataStructures", "PlayerFishingConditions", ""});
        if (!fishingConditionsClass) return JS_ThrowInternalError(context, "GetFishingLevel: estrutura nativa ausente");
    }
    auto* player = bl::il2cpp::findClass({"Terraria", "Player", ""});
    const MethodInfo* method = player ? bl::il2cpp::findMethodBySignature(player, bl::il2cpp::parseSignature(selected->signature)) : nullptr;
    const auto* entry = method ? static_cast<const uint8_t*>(bl::il2cpp::methodPointer(method)) : nullptr;
    if (!entry || std::memcmp(entry + selected->offset, selected->expected, sizeof(selected->expected)) != 0) {
        return JS_ThrowInternalError(context, "%s: corpo nativo incompativel", selected->name);
    }
    if (!bl::hook::install(const_cast<uint8_t*>(entry + selected->offset), reinterpret_cast<void*>(selected->bridge), selected->original)) {
        return JS_ThrowInternalError(context, "%s: hook nativo recusado", selected->name);
    }
    selected->callback = JS_DupValue(context, argv[1]);
    selected->context = context;
    return JS_UNDEFINED;
}
}

namespace bl::script {
void installPlayerBindings(void* context) {
    auto* ctx = static_cast<JSContext*>(context);
    JSValue global = JS_GetGlobalObject(ctx);
    JSValue api = JS_GetPropertyStr(ctx, global, "bl");
    JS_SetPropertyStr(ctx, api, "installPlayerStage", JS_NewCFunction(ctx, installPlayerStage, "installPlayerStage", 2));
    JS_FreeValue(ctx, api);
    JS_FreeValue(ctx, global);
}
}
#else
namespace bl::script {
void installPlayerBindings(void*) {}
}
#endif
