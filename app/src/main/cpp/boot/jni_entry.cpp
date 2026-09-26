#include <jni.h>
#include <string>
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

} // namespace

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
    BL_INFO("Bunny Loader iniciando, versao do jogo %lld (log %s)", (long long)c.gameVersion,
            c.verboseLog ? "detalhado" : "resumido; detalhes em Configuracoes > Log detalhado");

    if (!bl::loader::installWatcher()) {
        g_lastError = "falha ao instalar o watcher de il2cpp_init";
        return JNI_FALSE;
    }
    return JNI_TRUE;
}

extern "C" JNIEXPORT jstring JNICALL
Java_dev_bunnyloader_nativebridge_NativeBridge_lastError(JNIEnv* env, jobject) {
    return env->NewStringUTF(g_lastError.c_str());
}
