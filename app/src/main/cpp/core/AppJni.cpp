#include "core/AppJni.h"
#include "core/Log.h"
#include <dlfcn.h>
#include <mutex>
#include <pthread.h>

namespace bl {

namespace {

JavaVM* g_vm = nullptr;
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
// aborta o processo.
void detachOnExit(void*) {
    if (g_vm) g_vm->DetachCurrentThread();
}

} // namespace

JNIEnv* appJniEnv() {
    static std::once_flag once;
    std::call_once(once, [] {
        pthread_key_create(&g_detachKey, detachOnExit);
        g_vm = findJavaVM();
        if (!g_vm) BL_ERROR("jni: nenhuma JavaVM");
    });
    if (!g_vm) return nullptr;
    JNIEnv* env = nullptr;
    if (g_vm->GetEnv(reinterpret_cast<void**>(&env), JNI_VERSION_1_6) == JNI_OK) return env;
    if (g_vm->AttachCurrentThread(&env, nullptr) != JNI_OK) return nullptr;
    pthread_setspecific(g_detachKey, env);
    return env;
}

bool appJniCleared(JNIEnv* env, const char* where) {
    if (!env->ExceptionCheck()) return true;
    BL_ERROR("jni: excecao Java em %s", where);
    env->ExceptionDescribe();
    env->ExceptionClear();
    return false;
}

jclass appJniClass(JNIEnv* env, const char* dotted) {
    jclass at = env->FindClass("android/app/ActivityThread");
    if (!appJniCleared(env, "ActivityThread") || !at) return nullptr;
    jmethodID current = env->GetStaticMethodID(at, "currentApplication", "()Landroid/app/Application;");
    jobject app = current ? env->CallStaticObjectMethod(at, current) : nullptr;
    if (!appJniCleared(env, "currentApplication") || !app) return nullptr;
    jclass ctx = env->FindClass("android/content/Context");
    jmethodID getLoader = env->GetMethodID(ctx, "getClassLoader", "()Ljava/lang/ClassLoader;");
    jobject loader = env->CallObjectMethod(app, getLoader);
    if (!appJniCleared(env, "getClassLoader") || !loader) return nullptr;
    jmethodID loadClass = env->GetMethodID(env->GetObjectClass(loader), "loadClass",
                                           "(Ljava/lang/String;)Ljava/lang/Class;");
    jstring name = env->NewStringUTF(dotted);
    auto cls = static_cast<jclass>(env->CallObjectMethod(loader, loadClass, name));
    env->DeleteLocalRef(name);
    if (!appJniCleared(env, dotted) || !cls) return nullptr;
    return cls;
}

} // namespace bl
