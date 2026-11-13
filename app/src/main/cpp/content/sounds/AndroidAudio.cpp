#include "content/sounds/AndroidAudio.h"

#include "core/AppJni.h"
#include "core/Log.h"

#include <dlfcn.h>
#include <jni.h>
#include <pthread.h>

#include <mutex>

namespace bl::runtime::content {

namespace {

struct Jni {
    JavaVM* vm = nullptr;
    jclass cls = nullptr;
    jmethodID load = nullptr;
    jmethodID enqueue = nullptr;
    jmethodID cancel = nullptr;
    jmethodID playbackState = nullptr;
    jmethodID activeSound = nullptr;
    jmethodID musicRegister = nullptr;
    jmethodID musicVolume = nullptr;
    jmethodID musicState = nullptr;
};

Jni g_jni;
std::once_flag g_once;
bool g_ok = false;
pthread_key_t g_detachKey;

void detachOnExit(void*) {
    if (g_jni.vm) g_jni.vm->DetachCurrentThread();
}

JNIEnv* envOfThisThread() {
    JNIEnv* env = nullptr;
    if (g_jni.vm->GetEnv(reinterpret_cast<void**>(&env), JNI_VERSION_1_6) == JNI_OK) return env;
    if (g_jni.vm->AttachCurrentThread(&env, nullptr) != JNI_OK) return nullptr;
    pthread_setspecific(g_detachKey, env);
    return env;
}

bool cleared(JNIEnv* env, const char* where) {
    if (!env->ExceptionCheck()) return true;
    BL_ERROR("sons: excecao Java em %s", where);
    env->ExceptionDescribe();
    env->ExceptionClear();
    return false;
}

jclass appClass(JNIEnv* env, const char* dotted) {
    jclass at = env->FindClass("android/app/ActivityThread");
    if (!cleared(env, "ActivityThread") || !at) return nullptr;
    jmethodID current = env->GetStaticMethodID(at, "currentApplication", "()Landroid/app/Application;");
    jobject app = current ? env->CallStaticObjectMethod(at, current) : nullptr;
    if (!cleared(env, "currentApplication") || !app) return nullptr;
    jclass ctx = env->FindClass("android/content/Context");
    jmethodID getLoader = env->GetMethodID(ctx, "getClassLoader", "()Ljava/lang/ClassLoader;");
    jobject loader = env->CallObjectMethod(app, getLoader);
    if (!cleared(env, "getClassLoader") || !loader) return nullptr;
    jmethodID loadClass = env->GetMethodID(env->GetObjectClass(loader), "loadClass",
                                           "(Ljava/lang/String;)Ljava/lang/Class;");
    jstring name = env->NewStringUTF(dotted);
    auto cls = static_cast<jclass>(env->CallObjectMethod(loader, loadClass, name));
    env->DeleteLocalRef(name);
    if (!cleared(env, dotted) || !cls) return nullptr;
    return cls;
}

bool init() {
    std::call_once(g_once, [] {
        pthread_key_create(&g_detachKey, detachOnExit);
        g_jni.vm = appJavaVM();
        if (!g_jni.vm) {
            BL_ERROR("sons: nenhuma JavaVM");
            return;
        }
        JNIEnv* env = envOfThisThread();
        if (!env) return;
        jclass cls = appClass(env, "dev.bunnyloader.game.ModAudio");
        if (!cls) return;
        g_jni.cls = static_cast<jclass>(env->NewGlobalRef(cls));
        g_jni.load = env->GetStaticMethodID(cls, "load", "(Ljava/lang/String;)I");
        g_jni.enqueue = env->GetStaticMethodID(cls, "enqueue", "(IIFFFIZ)I");
        g_jni.cancel = env->GetStaticMethodID(cls, "cancel", "(I)V");
        g_jni.playbackState = env->GetStaticMethodID(cls, "playbackState", "(I)I");
        g_jni.activeSound = env->GetStaticMethodID(cls, "activeSound", "(I)I");
        g_jni.musicRegister = env->GetStaticMethodID(cls, "musicRegister", "(Ljava/lang/String;)I");
        g_jni.musicVolume = env->GetStaticMethodID(cls, "musicVolume", "(IF)V");
        g_jni.musicState = env->GetStaticMethodID(cls, "musicState", "(I)I");
        g_ok = cleared(env, "metodos do ModAudio") && g_jni.load && g_jni.enqueue && g_jni.cancel && g_jni.playbackState &&
               g_jni.activeSound && g_jni.musicRegister && g_jni.musicVolume && g_jni.musicState;
        if (!g_ok) BL_ERROR("sons: dev.bunnyloader.game.ModAudio sem os metodos esperados");
    });
    return g_ok;
}

}

int loadSound(const std::string& path) {
    if (!init()) return 0;
    JNIEnv* env = envOfThisThread();
    if (!env) return 0;
    jstring js = env->NewStringUTF(path.c_str());
    jint id = env->CallStaticIntMethod(g_jni.cls, g_jni.load, js);
    env->DeleteLocalRef(js);
    return cleared(env, "ModAudio.load") ? id : 0;
}

int enqueueSound(int group, int id, float left, float right, float rate, int max, bool ignore) {
    if (!init()) return 0;
    JNIEnv* env = envOfThisThread();
    if (!env) return 0;
    jint handle = env->CallStaticIntMethod(g_jni.cls, g_jni.enqueue, group, id, left, right, rate, max,
                                          static_cast<jboolean>(ignore));
    return cleared(env, "ModAudio.enqueue") ? handle : 0;
}

void cancelSound(int handle) {
    if (!init()) return;
    JNIEnv* env = envOfThisThread();
    if (!env) return;
    env->CallStaticVoidMethod(g_jni.cls, g_jni.cancel, handle);
    cleared(env, "ModAudio.cancel");
}

int playbackState(int handle) {
    if (!init()) return -1;
    JNIEnv* env = envOfThisThread();
    if (!env) return -1;
    jint state = env->CallStaticIntMethod(g_jni.cls, g_jni.playbackState, handle);
    return cleared(env, "ModAudio.playbackState") ? state : -1;
}

int activeSound(int group) {
    if (!init()) return 0;
    JNIEnv* env = envOfThisThread();
    if (!env) return 0;
    jint handle = env->CallStaticIntMethod(g_jni.cls, g_jni.activeSound, group);
    return cleared(env, "ModAudio.activeSound") ? handle : 0;
}

int registerMusic(const std::string& path) {
    if (!init()) return 0;
    JNIEnv* env = envOfThisThread();
    if (!env) return 0;
    jstring js = env->NewStringUTF(path.c_str());
    jint id = env->CallStaticIntMethod(g_jni.cls, g_jni.musicRegister, js);
    env->DeleteLocalRef(js);
    return cleared(env, "ModAudio.musicRegister") ? id : 0;
}

void setMusicVolume(int id, float volume) {
    if (!init()) return;
    JNIEnv* env = envOfThisThread();
    if (!env) return;
    env->CallStaticVoidMethod(g_jni.cls, g_jni.musicVolume, id, volume);
    cleared(env, "ModAudio.musicVolume");
}

int musicState(int id) {
    if (!init()) return -1;
    JNIEnv* env = envOfThisThread();
    if (!env) return -1;
    jint s = env->CallStaticIntMethod(g_jni.cls, g_jni.musicState, id);
    return cleared(env, "ModAudio.musicState") ? s : -1;
}

}
