#include <dlfcn.h>
#include <jni.h>
#include <cstdint>
#include <cstring>
#include <string>
#include "core/Log.h"

namespace {

struct Handle { void* opaque; };
struct DString { uint8_t* ptr; size_t size; };

using FreeFn = void (*)(void*);
using ResultCallback = void (*)(Handle* result, void* userData);
using LogCallback = void (*)(DString message, int severity, void* userData);

constexpr int ActivityPlaying = 0;
constexpr int PlatformAndroid = 16;
constexpr int SeverityError = 4;

struct Api {
    void (*runCallbacks)();
    void (*clientInit)(Handle*);
    void (*clientSetApplicationId)(Handle*, uint64_t);
    void (*clientAddLogCallback)(Handle*, LogCallback, FreeFn, void*, int);
    void (*clientUpdateRichPresence)(Handle*, Handle*, ResultCallback, FreeFn, void*);
    void (*clientClearRichPresence)(Handle*);
    void (*activityInit)(Handle*);
    void (*activityDrop)(Handle*);
    void (*activitySetName)(Handle*, DString);
    void (*activitySetType)(Handle*, int);
    void (*activitySetState)(Handle*, DString*);
    void (*activitySetAssets)(Handle*, Handle*);
    void (*activitySetTimestamps)(Handle*, Handle*);
    void (*activitySetSupportedPlatforms)(Handle*, int);
    void (*assetsInit)(Handle*);
    void (*assetsDrop)(Handle*);
    void (*assetsSetLargeImage)(Handle*, DString*);
    void (*assetsSetLargeText)(Handle*, DString*);
    void (*timestampsInit)(Handle*);
    void (*timestampsDrop)(Handle*);
    void (*timestampsSetStart)(Handle*, uint64_t);
    bool (*resultSuccessful)(Handle*);
    void (*resultError)(Handle*, DString*);
    void (*free)(void*);
};

Api g_api{};
Handle g_client{};
bool g_ready = false;

template <typename T>
bool resolve(void* lib, const char* name, T& out) {
    out = reinterpret_cast<T>(dlsym(lib, name));
    if (!out) BL_WARN("discord: missing symbol %s", name);
    return out != nullptr;
}

bool loadApi() {
    void* lib = dlopen("libdiscord_partner_sdk.so", RTLD_NOW | RTLD_NOLOAD);
    if (!lib) lib = dlopen("libdiscord_partner_sdk.so", RTLD_NOW);
    if (!lib) {
        BL_WARN("discord: SDK library not loaded (%s)", dlerror());
        return false;
    }
    auto& a = g_api;
    return resolve(lib, "Discord_RunCallbacks", a.runCallbacks)
        && resolve(lib, "Discord_Client_Init", a.clientInit)
        && resolve(lib, "Discord_Client_SetApplicationId", a.clientSetApplicationId)
        && resolve(lib, "Discord_Client_AddLogCallback", a.clientAddLogCallback)
        && resolve(lib, "Discord_Client_UpdateRichPresence", a.clientUpdateRichPresence)
        && resolve(lib, "Discord_Client_ClearRichPresence", a.clientClearRichPresence)
        && resolve(lib, "Discord_Activity_Init", a.activityInit)
        && resolve(lib, "Discord_Activity_Drop", a.activityDrop)
        && resolve(lib, "Discord_Activity_SetName", a.activitySetName)
        && resolve(lib, "Discord_Activity_SetType", a.activitySetType)
        && resolve(lib, "Discord_Activity_SetState", a.activitySetState)
        && resolve(lib, "Discord_Activity_SetAssets", a.activitySetAssets)
        && resolve(lib, "Discord_Activity_SetTimestamps", a.activitySetTimestamps)
        && resolve(lib, "Discord_Activity_SetSupportedPlatforms", a.activitySetSupportedPlatforms)
        && resolve(lib, "Discord_ActivityAssets_Init", a.assetsInit)
        && resolve(lib, "Discord_ActivityAssets_Drop", a.assetsDrop)
        && resolve(lib, "Discord_ActivityAssets_SetLargeImage", a.assetsSetLargeImage)
        && resolve(lib, "Discord_ActivityAssets_SetLargeText", a.assetsSetLargeText)
        && resolve(lib, "Discord_ActivityTimestamps_Init", a.timestampsInit)
        && resolve(lib, "Discord_ActivityTimestamps_Drop", a.timestampsDrop)
        && resolve(lib, "Discord_ActivityTimestamps_SetStart", a.timestampsSetStart)
        && resolve(lib, "Discord_ClientResult_Successful", a.resultSuccessful)
        && resolve(lib, "Discord_ClientResult_Error", a.resultError)
        && resolve(lib, "Discord_Free", a.free);
}

std::string utf8(JNIEnv* env, jstring js) {
    if (!js) return {};
    const char* chars = env->GetStringUTFChars(js, nullptr);
    std::string out(chars ? chars : "");
    if (chars) env->ReleaseStringUTFChars(js, chars);
    return out;
}

DString view(std::string& s) {
    return DString{reinterpret_cast<uint8_t*>(s.data()), s.size()};
}

void onSdkLog(DString message, int severity, void*) {
    BL_WARN("discord: [%d] %.*s", severity, (int)message.size, reinterpret_cast<const char*>(message.ptr));
}

void onPresenceResult(Handle* result, void*) {
    if (g_api.resultSuccessful(result)) {
        BL_INFO("discord: presence updated");
        return;
    }
    DString error{};
    g_api.resultError(result, &error);
    BL_WARN("discord: presence not sent, is the Discord app installed and signed in? %.*s",
            (int)error.size, reinterpret_cast<const char*>(error.ptr));
    if (error.ptr) g_api.free(error.ptr);
}

}

extern "C" JNIEXPORT jboolean JNICALL
Java_dev_bunnyloader_game_DiscordPresence_nativeStart(JNIEnv*, jobject, jlong applicationId) {
    if (g_ready) return JNI_TRUE;
    if (!loadApi()) return JNI_FALSE;
    g_api.clientInit(&g_client);
    g_api.clientAddLogCallback(&g_client, onSdkLog, nullptr, nullptr, SeverityError);
    g_api.clientSetApplicationId(&g_client, static_cast<uint64_t>(applicationId));
    g_ready = true;
    BL_INFO("discord: client ready");
    return JNI_TRUE;
}

extern "C" JNIEXPORT void JNICALL
Java_dev_bunnyloader_game_DiscordPresence_nativeUpdate(JNIEnv* env, jobject, jstring jName,
                                                       jstring jState, jstring jImage,
                                                       jstring jImageText, jlong startMs) {
    if (!g_ready) return;
    std::string name = utf8(env, jName);
    std::string state = utf8(env, jState);
    std::string image = utf8(env, jImage);
    std::string imageText = utf8(env, jImageText);

    Handle activity{}, assets{}, timestamps{};
    g_api.activityInit(&activity);
    g_api.activitySetName(&activity, view(name));
    g_api.activitySetType(&activity, ActivityPlaying);
    g_api.activitySetSupportedPlatforms(&activity, PlatformAndroid);
    DString stateView = view(state);
    g_api.activitySetState(&activity, &stateView);

    g_api.assetsInit(&assets);
    if (!image.empty()) {
        DString imageView = view(image);
        g_api.assetsSetLargeImage(&assets, &imageView);
    }
    if (!imageText.empty()) {
        DString textView = view(imageText);
        g_api.assetsSetLargeText(&assets, &textView);
    }
    g_api.activitySetAssets(&activity, &assets);

    g_api.timestampsInit(&timestamps);
    g_api.timestampsSetStart(&timestamps, static_cast<uint64_t>(startMs));
    g_api.activitySetTimestamps(&activity, &timestamps);

    g_api.clientUpdateRichPresence(&g_client, &activity, onPresenceResult, nullptr, nullptr);

    g_api.timestampsDrop(&timestamps);
    g_api.assetsDrop(&assets);
    g_api.activityDrop(&activity);
}

extern "C" JNIEXPORT void JNICALL
Java_dev_bunnyloader_game_DiscordPresence_nativeClear(JNIEnv*, jobject) {
    if (g_ready) g_api.clientClearRichPresence(&g_client);
}

extern "C" JNIEXPORT void JNICALL
Java_dev_bunnyloader_game_DiscordPresence_nativeRunCallbacks(JNIEnv*, jobject) {
    if (g_ready) g_api.runCallbacks();
}
