#include "content/common/ServerSyncBridge.h"
#include "core/AppJni.h"
#include "core/Log.h"

namespace bl::runtime {

bool callServerSync(const char* method, const char* signature, const std::string* arg, std::string* out) {
    JNIEnv* env = appJniEnv();
    jclass cls = env ? appJniClass(env, "dev.bunnyloader.game.ServerSync") : nullptr;
    jmethodID id = cls ? env->GetStaticMethodID(cls, method, signature) : nullptr;
    if (!id || !appJniCleared(env, method)) {
        BL_ERROR("server sync: ServerSync.%s not found", method);
        if (cls) env->DeleteLocalRef(cls);
        return false;
    }
    jstring text = arg ? env->NewStringUTF(arg->c_str()) : nullptr;
    if (out) {
        auto result = static_cast<jstring>(text ? env->CallStaticObjectMethod(cls, id, text)
                                                : env->CallStaticObjectMethod(cls, id));
        if (result) {
            const char* chars = env->GetStringUTFChars(result, nullptr);
            if (chars) {
                *out = chars;
                env->ReleaseStringUTFChars(result, chars);
            }
            env->DeleteLocalRef(result);
        }
    } else if (text) {
        env->CallStaticVoidMethod(cls, id, text);
    } else {
        env->CallStaticVoidMethod(cls, id);
    }
    if (text) env->DeleteLocalRef(text);
    env->DeleteLocalRef(cls);
    return appJniCleared(env, method);
}

}
