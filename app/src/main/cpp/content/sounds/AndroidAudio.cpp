#include "content/sounds/AndroidAudio.h"

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
    jmethodID state = nullptr;
    jmethodID duration = nullptr;
    jmethodID play = nullptr;
    jmethodID stop = nullptr;
    jmethodID musicRegister = nullptr;
    jmethodID musicVolume = nullptr;
    jmethodID musicState = nullptr;
};

Jni g_jni;
std::once_flag g_once;
bool g_ok = false;
pthread_key_t g_detachKey;

JavaVM* findJavaVM() {
    using GetVMs = jint (*)(JavaVM**, jsize, jsize*);
    for (const char* so : {"libnativehelper.so", "libart.so", "libandroid_runtime.so"}) {
        void* h = dlopen(so, RTLD_NOW | RTLD_NOLOAD);
        if (!h) h = dlopen(so, RTLD_NOW);
        auto fn = h ? reinterpret_cast<GetVMs>(dlsym(h, "JNI_GetCreatedJavaVMs")) : nullptr;
        JavaVM* vm = nullptr;
        jsize n = 0;
        if (fn && fn(&vm, 1, &n) == JNI_OK && n > 0) return vm;
    }
    return nullptr;
}

// A thread nativa anexada tem de se desanexar antes de acabar, senao o ART
// aborta o processo. O destrutor da chave roda quando ela termina.
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

/**
 * A classe do app, pelo classloader do app: o FindClass de uma thread anexada
 * so enxerga as classes do sistema.
 */
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
        g_jni.vm = findJavaVM();
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
        g_jni.state = env->GetStaticMethodID(cls, "state", "(I)I");
        g_jni.duration = env->GetStaticMethodID(cls, "duration", "(I)I");
        g_jni.play = env->GetStaticMethodID(cls, "play", "(IFFF)I");
        g_jni.stop = env->GetStaticMethodID(cls, "stop", "(I)V");
        g_jni.musicRegister = env->GetStaticMethodID(cls, "musicRegister", "(Ljava/lang/String;)I");
        g_jni.musicVolume = env->GetStaticMethodID(cls, "musicVolume", "(IF)V");
        g_jni.musicState = env->GetStaticMethodID(cls, "musicState", "(I)I");
        g_ok = cleared(env, "metodos do ModAudio") && g_jni.load && g_jni.state && g_jni.duration &&
               g_jni.play && g_jni.stop && g_jni.musicRegister && g_jni.musicVolume && g_jni.musicState;
        if (!g_ok) BL_ERROR("sons: dev.bunnyloader.game.ModAudio sem os metodos esperados");
    });
    return g_ok;
}

} // namespace

int loadSound(const std::string& path) {
    if (!init()) return 0;
    JNIEnv* env = envOfThisThread();
    if (!env) return 0;
    jstring js = env->NewStringUTF(path.c_str());
    jint id = env->CallStaticIntMethod(g_jni.cls, g_jni.load, js);
    env->DeleteLocalRef(js);
    return cleared(env, "ModAudio.load") ? id : 0;
}

int soundState(int id) {
    if (!init()) return -1;
    JNIEnv* env = envOfThisThread();
    if (!env) return -1;
    jint s = env->CallStaticIntMethod(g_jni.cls, g_jni.state, id);
    return cleared(env, "ModAudio.state") ? s : -1;
}

int playSound(int id, float left, float right, float rate) {
    if (!init()) return 0;
    JNIEnv* env = envOfThisThread();
    if (!env) return 0;
    jint stream = env->CallStaticIntMethod(g_jni.cls, g_jni.play, id, left, right, rate);
    return cleared(env, "ModAudio.play") ? stream : 0;
}

void stopSound(int stream) {
    if (!init()) return;
    JNIEnv* env = envOfThisThread();
    if (!env) return;
    env->CallStaticVoidMethod(g_jni.cls, g_jni.stop, stream);
    cleared(env, "ModAudio.stop");
}

int soundDuration(int id) {
    if (!init()) return 0;
    JNIEnv* env = envOfThisThread();
    if (!env) return 0;
    jint ms = env->CallStaticIntMethod(g_jni.cls, g_jni.duration, id);
    return cleared(env, "ModAudio.duration") ? ms : 0;
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

} // namespace bl::runtime::content
