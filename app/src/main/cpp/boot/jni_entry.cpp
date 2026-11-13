#include <jni.h>
#include <string>
#include "core/AppJni.h"
#include "core/Config.h"
#include "core/Log.h"
#include "boot/LibWatcher.h"

namespace {

std::string g_lastError;

std::string readString(JNIEnv* env, jobject obj, jclass cls, const char* name) {
    jfieldID id = env->GetFieldID(cls, name, "Ljava/lang/String;");
    auto js = reinterpret_cast<jstring>(env->GetObjectField(obj, id));
    if (!js) return {};
    const char* chars = env->GetStringUTFChars(js, nullptr);
    std::string out(chars ? chars : "");
    if (chars) env->ReleaseStringUTFChars(js, chars);
    return out;
}

}

extern "C" JNIEXPORT jint JNICALL JNI_OnLoad(JavaVM* vm, void*) {
    bl::setAppJavaVM(vm);
    return JNI_VERSION_1_6;
}

extern "C" JNIEXPORT jboolean JNICALL
Java_dev_bunnyloader_nativebridge_NativeBridge_init(JNIEnv* env, jobject, jobject cfg) {
    jclass cls = env->GetObjectClass(cfg);
    auto& c = bl::config();

    c.gameLibDir = readString(env, cfg, cls, "gameLibDir");
    c.modsDir    = readString(env, cfg, cls, "modsDir");
    c.logPath    = readString(env, cfg, cls, "logPath");
    c.cmdPath    = readString(env, cfg, cls, "cmdPath");
    c.showErrors = env->GetBooleanField(cfg, env->GetFieldID(cls, "showErrors", "Z"));
    c.verboseLog = env->GetBooleanField(cfg, env->GetFieldID(cls, "verboseLog", "Z"));
    c.gameVersion = env->GetLongField(cfg, env->GetFieldID(cls, "gameVersion", "J"));
    c.fastBoot   = env->GetBooleanField(cfg, env->GetFieldID(cls, "fastBoot", "Z"));
    c.dotnetDir  = readString(env, cfg, cls, "dotnetDir");
    c.quickPlayer = readString(env, cfg, cls, "quickPlayer");
    c.quickWorld  = readString(env, cfg, cls, "quickWorld");
    c.joinAddress = readString(env, cfg, cls, "joinAddress");
    c.joinPort    = env->GetIntField(cfg, env->GetFieldID(cls, "joinPort", "I"));
    c.joinPassword = readString(env, cfg, cls, "joinPassword");
    c.joinPlayer  = readString(env, cfg, cls, "joinPlayer");
    c.gameTexts   = readString(env, cfg, cls, "gameTexts");

    auto arr = reinterpret_cast<jobjectArray>(
        env->GetObjectField(cfg, env->GetFieldID(cls, "enabledMods", "[Ljava/lang/String;")));
    if (arr) {
        jsize count = env->GetArrayLength(arr);
        c.enabledMods.clear();
        for (jsize i = 0; i < count; ++i) {
            auto js = reinterpret_cast<jstring>(env->GetObjectArrayElement(arr, i));
            const char* chars = env->GetStringUTFChars(js, nullptr);
            if (chars) {
                c.enabledMods.push_back(bl::parseModSpec(chars));
                env->ReleaseStringUTFChars(js, chars);
            }
            env->DeleteLocalRef(js);
        }
    }

    bl::log::setVerbose(c.verboseLog);
    bl::log::open(c.logPath.c_str());
    BL_INFO("Bunny Loader starting, game version %lld (log: %s)", (long long)c.gameVersion,
            c.verboseLog ? "detailed" : "short; details under Settings > Detailed log");

    if (!bl::loader::installWatcher()) {
        g_lastError = "failed to install the il2cpp_init watcher";
        return JNI_FALSE;
    }
    return JNI_TRUE;
}

extern "C" JNIEXPORT jstring JNICALL
Java_dev_bunnyloader_nativebridge_NativeBridge_lastError(JNIEnv* env, jobject) {
    return env->NewStringUTF(g_lastError.c_str());
}
