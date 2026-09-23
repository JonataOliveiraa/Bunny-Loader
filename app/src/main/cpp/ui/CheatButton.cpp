#include "ui/CheatButton.h"
#include "core/Config.h"
#include "core/Log.h"
#include <string>
#include <vector>

#include "runtime/Cheats.h"
#include "ui/CheatBridgeDex.h"

#include <atomic>
#include <dlfcn.h>
#include <initializer_list>
#include <jni.h>
#include <string>

namespace bl::ui {

namespace {

// --- JavaVM da libnativehelper/libart (estamos injetados; nao temos OnLoad) ---
JavaVM* getJavaVM() {
    using GetVMs = jint (*)(JavaVM**, jsize, jsize*);
    GetVMs fn = nullptr;
    for (const char* so : {"libnativehelper.so", "libart.so", "libandroid_runtime.so"}) {
        void* h = dlopen(so, RTLD_NOW | RTLD_NOLOAD);
        if (!h) h = dlopen(so, RTLD_NOW);
        if (!h) continue;
        fn = reinterpret_cast<GetVMs>(dlsym(h, "JNI_GetCreatedJavaVMs"));
        if (fn) break;
    }
    if (!fn) { BL_ERROR("botao: JNI_GetCreatedJavaVMs nao encontrado"); return nullptr; }

    JavaVM* vm = nullptr;
    jsize n = 0;
    if (fn(&vm, 1, &n) != JNI_OK || n == 0) {
        BL_ERROR("botao: nenhuma JavaVM criada");
        return nullptr;
    }
    return vm;
}

// Loga e limpa uma excecao Java pendente. Retorna true se havia excecao.
bool checkExc(JNIEnv* env, const char* where) {
    if (!env->ExceptionCheck()) return false;
    BL_ERROR("botao: excecao Java em %s", where);
    env->ExceptionDescribe();
    env->ExceptionClear();
    return true;
}

// --- painel de erro ---------------------------------------------------------
//
// Mostrado UMA vez, no primeiro erro, com tudo que ja se acumulou. Um hook que
// falha a cada frame dispararia milhares de chamadas; e o texto acumulado ja
// traz os erros seguintes de qualquer jeito.
jclass g_bridge = nullptr;
jmethodID g_showError = nullptr;
std::atomic<bool> g_errorShown{false};

void onNativeError(const char*) {
    if (g_errorShown.exchange(true)) return;
    JavaVM* vm = getJavaVM();
    if (!vm || !g_bridge || !g_showError) return;

    JNIEnv* env = nullptr;
    bool attached = false;
    if (vm->GetEnv(reinterpret_cast<void**>(&env), JNI_VERSION_1_6) != JNI_OK) {
        if (vm->AttachCurrentThread(&env, nullptr) != JNI_OK) return;
        attached = true;
    }
    std::string text = log::errorsSoFar();
    jstring js = env->NewStringUTF(text.c_str());
    env->CallStaticVoidMethod(g_bridge, g_showError, js);
    env->ExceptionClear();
    env->DeleteLocalRef(js);
    if (attached) vm->DetachCurrentThread();
}

// Metodos nativos ligados ao CheatBridge.
void JNICALL jni_onGive(JNIEnv*, jclass, jint type, jint stack) {
    bl::runtime::requestGive(type, stack);
}

/**
 * As tabelas de nome, do nativo para o dex. null enquanto ainda moem.
 *
 * Uma copia so, quando o menu abre: e String[] de ~6800 posicoes, e o Java
 * segura a referencia depois disso. Passar nome a nome por JNI, por linha
 * visivel, custaria uma travessia por rolagem — e pior, viria da thread de UI,
 * que nao pode tocar no il2cpp.
 */
jobjectArray comoArray(JNIEnv* env, const std::vector<std::u16string>& nomes) {
    if (!runtime::namesReady()) return nullptr;
    jclass sc = env->FindClass("java/lang/String");
    if (!sc) return nullptr;
    jobjectArray out = env->NewObjectArray(static_cast<jsize>(nomes.size()), sc, nullptr);
    if (!out) return nullptr;
    for (size_t i = 0; i < nomes.size(); ++i) {
        if (nomes[i].empty()) continue;   // fica null: o menu mostra so o id
        jstring js = env->NewString(reinterpret_cast<const jchar*>(nomes[i].data()),
                                    static_cast<jsize>(nomes[i].size()));
        env->SetObjectArrayElement(out, static_cast<jsize>(i), js);
        env->DeleteLocalRef(js);
    }
    return out;
}

jobjectArray JNICALL jni_itemNames(JNIEnv* env, jclass) {
    return comoArray(env, runtime::itemNames());
}

jobjectArray JNICALL jni_npcNames(JNIEnv* env, jclass) {
    return comoArray(env, runtime::npcNames());
}

jintArray JNICALL jni_npcFrames(JNIEnv* env, jclass) {
    const std::vector<int>& v = runtime::npcFrames();
    if (v.empty()) return nullptr;
    jintArray out = env->NewIntArray(static_cast<jsize>(v.size()));
    if (out) env->SetIntArrayRegion(out, 0, static_cast<jsize>(v.size()), v.data());
    return out;
}

void JNICALL jni_onSpawn(JNIEnv*, jclass, jint type, jint count) {
    bl::runtime::requestSpawn(type, count);
}

// Classloader do app (acha classes do app, ao contrario do FindClass de uma
// thread anexada). Via android.app.ActivityThread.currentApplication().
jobject getAppClassLoader(JNIEnv* env) {
    jclass at = env->FindClass("android/app/ActivityThread");
    if (checkExc(env, "FindClass ActivityThread") || !at) return nullptr;
    jmethodID mApp = env->GetStaticMethodID(
        at, "currentApplication", "()Landroid/app/Application;");
    if (checkExc(env, "currentApplication id") || !mApp) return nullptr;
    jobject app = env->CallStaticObjectMethod(at, mApp);
    if (checkExc(env, "currentApplication()") || !app) {
        BL_ERROR("botao: Application ainda nula");
        return nullptr;
    }
    jclass ctx = env->FindClass("android/content/Context");
    jmethodID mCl = env->GetMethodID(ctx, "getClassLoader", "()Ljava/lang/ClassLoader;");
    jobject loader = env->CallObjectMethod(app, mCl);
    checkExc(env, "getClassLoader");
    return loader;  // ref local
}

// loader.loadClass(name) -> jclass (ref local). name com pontos.
jclass loadClass(JNIEnv* env, jobject loader, const char* dotted) {
    jclass clCls = env->GetObjectClass(loader);
    jmethodID mLoad = env->GetMethodID(
        clCls, "loadClass", "(Ljava/lang/String;)Ljava/lang/Class;");
    jstring name = env->NewStringUTF(dotted);
    auto cls = static_cast<jclass>(env->CallObjectMethod(loader, mLoad, name));
    env->DeleteLocalRef(name);
    if (checkExc(env, dotted) || !cls) return nullptr;
    return cls;
}

} // namespace

void installCheatButton() {
    JavaVM* vm = getJavaVM();
    if (!vm) return;

    JNIEnv* env = nullptr;
    bool attached = false;
    if (vm->GetEnv(reinterpret_cast<void**>(&env), JNI_VERSION_1_6) != JNI_OK) {
        if (vm->AttachCurrentThread(&env, nullptr) != JNI_OK || !env) {
            BL_ERROR("botao: AttachCurrentThread falhou");
            return;
        }
        attached = true;
    }

    // 1. Classloader do app.
    jobject appLoader = getAppClassLoader(env);
    if (!appLoader) { if (attached) vm->DetachCurrentThread(); return; }

    // 2. Activity atual do Unity: UnityPlayer.currentActivity (carregada pelo
    //    classloader do app).
    jclass unity = loadClass(env, appLoader, "com.unity3d.player.UnityPlayer");
    if (!unity) { if (attached) vm->DetachCurrentThread(); return; }
    jfieldID fAct = env->GetStaticFieldID(unity, "currentActivity", "Landroid/app/Activity;");
    jobject activity = fAct ? env->GetStaticObjectField(unity, fAct) : nullptr;
    if (checkExc(env, "currentActivity") || !activity) {
        BL_ERROR("botao: currentActivity nula (jogo ainda nao criou a Activity?)");
        if (attached) vm->DetachCurrentThread();
        return;
    }

    // 3. Carrega o dex embutido (CheatBridge) com o classloader do app como pai.
    jobject dexBuf = env->NewDirectByteBuffer(
        const_cast<unsigned char*>(bl_cheatbridge_dex),
        static_cast<jlong>(bl_cheatbridge_dex_len));
    jclass imdcl = env->FindClass("dalvik/system/InMemoryDexClassLoader");
    if (checkExc(env, "InMemoryDexClassLoader") || !imdcl) {
        if (attached) vm->DetachCurrentThread();
        return;
    }
    jmethodID ctor = env->GetMethodID(
        imdcl, "<init>", "(Ljava/nio/ByteBuffer;Ljava/lang/ClassLoader;)V");
    jobject dexLoader = env->NewObject(imdcl, ctor, dexBuf, appLoader);
    if (checkExc(env, "new InMemoryDexClassLoader") || !dexLoader) {
        if (attached) vm->DetachCurrentThread();
        return;
    }

    jclass bridge = loadClass(env, dexLoader, "bunny.CheatBridge");
    if (!bridge) { if (attached) vm->DetachCurrentThread(); return; }

    // 4. Liga o metodo nativo.
    JNINativeMethod nm[] = {
        {"nOnGive", "(II)V", reinterpret_cast<void*>(&jni_onGive)},
        {"nOnSpawn", "(II)V", reinterpret_cast<void*>(&jni_onSpawn)},
        {"nItemNames", "()[Ljava/lang/String;", reinterpret_cast<void*>(&jni_itemNames)},
        {"nNpcNames", "()[Ljava/lang/String;", reinterpret_cast<void*>(&jni_npcNames)},
        {"nNpcFrames", "()[I", reinterpret_cast<void*>(&jni_npcFrames)},
    };
    if (env->RegisterNatives(bridge, nm, 5) != JNI_OK) {
        checkExc(env, "RegisterNatives");
        if (attached) vm->DetachCurrentThread();
        return;
    }

    // 5. CheatBridge.install(activity) — monta o botao na UI thread.
    jmethodID mInstall = env->GetStaticMethodID(bridge, "install", "(Landroid/app/Activity;)V");
    if (!mInstall) {
        checkExc(env, "install id");
        if (attached) vm->DetachCurrentThread();
        return;
    }
    env->CallStaticVoidMethod(bridge, mInstall, activity);
    if (!checkExc(env, "install()")) {
        BL_INFO("botao: instalado na Activity do jogo");
    }

    // 6. Painel de erro: guarda a classe e passa a escutar BL_ERROR. A partir
    // daqui um erro do nucleo aparece DENTRO do jogo, com texto e botao de
    // copiar — quem joga no celular nao tem logcat.
    g_bridge = reinterpret_cast<jclass>(env->NewGlobalRef(bridge));
    g_showError = env->GetStaticMethodID(g_bridge, "showError", "(Ljava/lang/String;)V");
    // Desligado nas Configuracoes, o erro fica so no logcat.
    if (g_showError && config().showErrors) log::onError(&onNativeError);

    if (attached) vm->DetachCurrentThread();
}

} // namespace bl::ui
