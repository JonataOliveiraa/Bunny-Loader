#include "menu/CheatButton.h"
#include "content/common/ModNet.h"
#include "core/AppJni.h"
#include "core/Config.h"
#include "core/Log.h"
#include <iterator>
#include <string>
#include <vector>

#include "menu/Cheats.h"
#include "menu/MenuCatalog.h"
#include "content/buffs/ModBuffs.h"
#include "content/items/ModItems.h"
#include "menu/ModMenu.h"
#include "content/npcs/ModNpcs.h"
#include "menu/Powers.h"
#include "menu/CheatBridgeDex.h"
#include "menu/DevTools.h"
#include "script/api/Console.h"

#include <android/api-level.h>
#include <atomic>
#include <cerrno>
#include <cstdio>
#include <mutex>
#include <unordered_set>
#include <dlfcn.h>
#include <unistd.h>
#include <initializer_list>
#include <jni.h>
#include <iterator>
#include <string>

namespace bl::ui {

namespace {

bool checkExc(JNIEnv* env, const char* where) {
    if (!env->ExceptionCheck()) return false;
    BL_ERROR("botao: excecao Java em %s", where);
    env->ExceptionDescribe();
    env->ExceptionClear();
    return true;
}

jclass g_bridge = nullptr;
jmethodID g_showError = nullptr;
std::mutex g_errorSeenLock;
std::unordered_set<size_t> g_errorSeen;

void onNativeError(const char* line) {
    {
        std::lock_guard<std::mutex> guard(g_errorSeenLock);
        if (!g_errorSeen.insert(std::hash<std::string>()(line ? line : "")).second) return;
    }
    JavaVM* vm = appJavaVM();
    if (!vm || !g_bridge || !g_showError) return;

    JNIEnv* env = nullptr;
    bool attached = false;
    if (vm->GetEnv(reinterpret_cast<void**>(&env), JNI_VERSION_1_6) != JNI_OK) {
        if (vm->AttachCurrentThread(&env, nullptr) != JNI_OK) return;
        attached = true;
    }
    std::string text = log::errorsSoFar();
    if (line && *line && *line != '(' && text.find(line) == std::string::npos) {
        text += "\n(...)\n" + std::string(line) + "\n";
    }
    jstring js = env->NewStringUTF(text.c_str());
    env->CallStaticVoidMethod(g_bridge, g_showError, js);
    env->ExceptionClear();
    env->DeleteLocalRef(js);
    if (attached) vm->DetachCurrentThread();
}

std::string fromJava(JNIEnv* env, jstring s) {
    std::string out;
    if (!s) return out;
    const jsize n = env->GetStringLength(s);
    const jchar* c = env->GetStringChars(s, nullptr);
    if (!c) return out;
    out.reserve(static_cast<size_t>(n));
    for (jsize i = 0; i < n; ++i) {
        uint32_t cp = c[i];
        if (cp >= 0xD800 && cp <= 0xDBFF && i + 1 < n && c[i + 1] >= 0xDC00 && c[i + 1] <= 0xDFFF) {
            cp = 0x10000 + ((cp - 0xD800) << 10) + (c[i + 1] - 0xDC00);
            ++i;
        }
        if (cp < 0x80) {
            out += static_cast<char>(cp);
        } else if (cp < 0x800) {
            out += static_cast<char>(0xC0 | (cp >> 6));
            out += static_cast<char>(0x80 | (cp & 0x3F));
        } else if (cp < 0x10000) {
            out += static_cast<char>(0xE0 | (cp >> 12));
            out += static_cast<char>(0x80 | ((cp >> 6) & 0x3F));
            out += static_cast<char>(0x80 | (cp & 0x3F));
        } else {
            out += static_cast<char>(0xF0 | (cp >> 18));
            out += static_cast<char>(0x80 | ((cp >> 12) & 0x3F));
            out += static_cast<char>(0x80 | ((cp >> 6) & 0x3F));
            out += static_cast<char>(0x80 | (cp & 0x3F));
        }
    }
    env->ReleaseStringChars(s, c);
    return out;
}

jstring toJava(JNIEnv* env, const std::string& s) {
    std::u16string u;
    u.reserve(s.size());
    for (size_t i = 0; i < s.size();) {
        const auto b = static_cast<unsigned char>(s[i]);
        uint32_t cp = 0xFFFD;
        size_t len = 1;
        if (b < 0x80) { cp = b; }
        else if ((b >> 5) == 0x6 && i + 1 < s.size()) { cp = ((b & 0x1F) << 6) | (s[i + 1] & 0x3F); len = 2; }
        else if ((b >> 4) == 0xE && i + 2 < s.size()) {
            cp = ((b & 0x0F) << 12) | ((s[i + 1] & 0x3F) << 6) | (s[i + 2] & 0x3F); len = 3;
        } else if ((b >> 3) == 0x1E && i + 3 < s.size()) {
            cp = ((b & 0x07) << 18) | ((s[i + 1] & 0x3F) << 12) | ((s[i + 2] & 0x3F) << 6) | (s[i + 3] & 0x3F);
            len = 4;
        }
        i += len;
        if (cp >= 0x10000) {
            cp -= 0x10000;
            u += static_cast<char16_t>(0xD800 + (cp >> 10));
            u += static_cast<char16_t>(0xDC00 + (cp & 0x3FF));
        } else {
            u += static_cast<char16_t>(cp);
        }
    }
    return env->NewString(reinterpret_cast<const jchar*>(u.data()), static_cast<jsize>(u.size()));
}

void JNICALL jni_consoleRun(JNIEnv* env, jclass, jstring code) {
    if (bl::runtime::cheatsBlocked()) return;
    bl::script::consoleSubmit(fromJava(env, code));
}

jobjectArray JNICALL jni_consoleTake(JNIEnv* env, jclass) {
    const std::vector<std::string> entries = bl::script::consoleTake();
    jclass sc = env->FindClass("java/lang/String");
    if (!sc) return nullptr;
    jobjectArray out = env->NewObjectArray(static_cast<jsize>(entries.size()), sc, nullptr);
    if (!out) return nullptr;
    for (size_t i = 0; i < entries.size(); ++i) {
        jstring js = toJava(env, entries[i]);
        env->SetObjectArrayElement(out, static_cast<jsize>(i), js);
        env->DeleteLocalRef(js);
    }
    return out;
}

void JNICALL jni_requestRestart(JNIEnv*, jclass, jboolean save) {
    bl::runtime::requestRestart(save == JNI_TRUE);
}

void JNICALL jni_setGameFrozen(JNIEnv*, jclass, jboolean on) {
    bl::runtime::setGameFrozen(on == JNI_TRUE);
}

jboolean JNICALL jni_restartReady(JNIEnv*, jclass) {
    return bl::runtime::restartReady() ? JNI_TRUE : JNI_FALSE;
}

void JNICALL jni_onGive(JNIEnv*, jclass, jint type, jint stack) {
    if (bl::runtime::cheatsBlocked()) return;
    bl::runtime::requestGive(type, stack);
}

jobjectArray toJavaStringArray(JNIEnv* env, const std::vector<std::u16string>& names,
                               runtime::MenuKind kind) {
    if (!runtime::namesReady()) return nullptr;
    const std::unordered_set<int> hidden = runtime::hiddenFromModMenu(kind);
    jclass sc = env->FindClass("java/lang/String");
    if (!sc) return nullptr;
    std::lock_guard<std::mutex> guard(runtime::namesLock());
    jobjectArray out = env->NewObjectArray(static_cast<jsize>(names.size()), sc, nullptr);
    if (!out) return nullptr;
    for (size_t i = 0; i < names.size(); ++i) {
        if (names[i].empty() || hidden.count(static_cast<int>(i))) continue;
        jstring js = env->NewString(reinterpret_cast<const jchar*>(names[i].data()),
                                    static_cast<jsize>(names[i].size()));
        env->SetObjectArrayElement(out, static_cast<jsize>(i), js);
        env->DeleteLocalRef(js);
    }
    return out;
}

jobjectArray JNICALL jni_itemNames(JNIEnv* env, jclass) {
    return toJavaStringArray(env, runtime::itemNames(), runtime::MenuKind::Item);
}

jobjectArray JNICALL jni_npcNames(JNIEnv* env, jclass) {
    return toJavaStringArray(env, runtime::npcNames(), runtime::MenuKind::Npc);
}

jint JNICALL jni_namesGeneration(JNIEnv*, jclass) {
    return runtime::namesGeneration();
}

jintArray JNICALL jni_npcFrames(JNIEnv* env, jclass) {
    const std::vector<int>& v = runtime::npcFrames();
    if (v.empty()) return nullptr;
    jintArray out = env->NewIntArray(static_cast<jsize>(v.size()));
    if (out) env->SetIntArrayRegion(out, 0, static_cast<jsize>(v.size()), v.data());
    return out;
}

void JNICALL jni_onSpawn(JNIEnv*, jclass, jint type, jint count) {
    if (bl::runtime::cheatsBlocked()) return;
    bl::runtime::requestSpawn(type, count);
}

jbyteArray JNICALL jni_itemClasses(JNIEnv* env, jclass) {
    if (!runtime::namesReady()) return nullptr;
    const std::vector<uint8_t>& v = runtime::itemClasses();
    if (v.empty()) return nullptr;
    jbyteArray out = env->NewByteArray(static_cast<jsize>(v.size()));
    if (out) {
        env->SetByteArrayRegion(out, 0, static_cast<jsize>(v.size()),
                                reinterpret_cast<const jbyte*>(v.data()));
    }
    return out;
}

jbyteArray toJavaBytes(JNIEnv* env, const std::vector<uint8_t>& v) {
    if (!runtime::namesReady() || v.empty()) return nullptr;
    jbyteArray out = env->NewByteArray(static_cast<jsize>(v.size()));
    if (out) {
        env->SetByteArrayRegion(out, 0, static_cast<jsize>(v.size()),
                                reinterpret_cast<const jbyte*>(v.data()));
    }
    return out;
}

jbyteArray JNICALL jni_itemSubClasses(JNIEnv* env, jclass) {
    return toJavaBytes(env, runtime::itemSubClasses());
}

jbyteArray JNICALL jni_npcClasses(JNIEnv* env, jclass) {
    return toJavaBytes(env, runtime::npcClasses());
}

jobjectArray JNICALL jni_buffNames(JNIEnv* env, jclass) {
    return toJavaStringArray(env, runtime::buffNames(), runtime::MenuKind::Buff);
}

jbyteArray JNICALL jni_buffClasses(JNIEnv* env, jclass) {
    return toJavaBytes(env, runtime::buffClasses());
}

void JNICALL jni_onBuff(JNIEnv*, jclass, jint type, jint seconds) {
    if (bl::runtime::cheatsBlocked()) return;
    bl::runtime::requestBuff(type, seconds);
}

void JNICALL jni_setPower(JNIEnv*, jclass, jint id, jint level) {
    if (bl::runtime::cheatsBlocked()) return;
    bl::runtime::setPower(id, level);
}

void JNICALL jni_setTimeOfDay(JNIEnv*, jclass, jint which) {
    if (bl::runtime::cheatsBlocked()) return;
    bl::runtime::setTimeOfDay(which);
}

jintArray JNICALL jni_worldState(JNIEnv* env, jclass) {
    const jint v[] = {bl::runtime::worldHardmode(), bl::runtime::worldGameMode(),
                      bl::runtime::worldClockMinute(), bl::runtime::worldRainPosition(),
                      bl::runtime::worldWindPosition(), bl::runtime::worldInfectionStopped()};
    const jsize n = static_cast<jsize>(std::size(v));
    jintArray out = env->NewIntArray(n);
    if (out) env->SetIntArrayRegion(out, 0, n, v);
    return out;
}

jboolean JNICALL jni_cheatsBlocked(JNIEnv*, jclass) {
    return bl::runtime::cheatsBlocked() ? JNI_TRUE : JNI_FALSE;
}

jboolean JNICALL jni_inWorld(JNIEnv*, jclass) {
    return runtime::inWorld() ? JNI_TRUE : JNI_FALSE;
}

jobjectArray toJavaStrings(JNIEnv* env, const std::vector<std::string>& v) {
    jclass sc = env->FindClass("java/lang/String");
    jobjectArray out = sc ? env->NewObjectArray(static_cast<jsize>(v.size()), sc, nullptr) : nullptr;
    if (!out) return nullptr;
    for (size_t i = 0; i < v.size(); ++i) {
        jstring js = env->NewStringUTF(v[i].c_str());
        env->SetObjectArrayElement(out, static_cast<jsize>(i), js);
        env->DeleteLocalRef(js);
    }
    return out;
}

jint JNICALL jni_vanillaItemCount(JNIEnv*, jclass) { return runtime::kVanillaItemCount; }

jobjectArray JNICALL jni_modItemTextures(JNIEnv* env, jclass) {
    std::vector<std::string> v;
    for (const auto& i : runtime::modItems()) v.push_back(i.texture);
    return toJavaStrings(env, v);
}

jint JNICALL jni_vanillaNpcCount(JNIEnv*, jclass) { return runtime::kVanillaNpcCount; }

jint JNICALL jni_vanillaBuffCount(JNIEnv*, jclass) { return runtime::kVanillaBuffCount; }

jobjectArray JNICALL jni_modBuffTextures(JNIEnv* env, jclass) {
    std::vector<std::string> v;
    for (const auto& b : runtime::modBuffs()) v.push_back(b.texture);
    return toJavaStrings(env, v);
}

jobjectArray JNICALL jni_modNpcTextures(JNIEnv* env, jclass) {
    std::vector<std::string> v;
    for (const auto& n : runtime::modNpcs()) v.push_back(n.texture);
    return toJavaStrings(env, v);
}

jobjectArray JNICALL jni_modCategories(JNIEnv* env, jclass) {
    std::vector<std::string> v;
    for (const auto& f : runtime::modMenuFolders()) {
        v.push_back(f.mod);
        v.push_back(f.modName);
        v.push_back(f.modIcon);
        v.push_back(f.name);
        v.push_back(f.icon);
        v.push_back(f.buff ? "buff" : f.npc ? "npc" : "item");
    }
    return toJavaStrings(env, v);
}

jintArray JNICALL jni_modCategoryItems(JNIEnv* env, jclass, jint i) {
    const auto cats = runtime::modMenuFolders();
    if (i < 0 || static_cast<size_t>(i) >= cats.size()) return nullptr;
    const std::vector<int>& t = cats[static_cast<size_t>(i)].types;
    jintArray out = env->NewIntArray(static_cast<jsize>(t.size()));
    if (out) env->SetIntArrayRegion(out, 0, static_cast<jsize>(t.size()), t.data());
    return out;
}

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
    return loader;
}

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

jobject loadDexInMemory(JNIEnv* env, jobject parent) {
    jobject buffer = env->NewDirectByteBuffer(
        const_cast<unsigned char*>(bl_cheatbridge_dex),
        static_cast<jlong>(bl_cheatbridge_dex_len));
    jclass cls = env->FindClass("dalvik/system/InMemoryDexClassLoader");
    if (checkExc(env, "InMemoryDexClassLoader") || !cls) return nullptr;
    jmethodID ctor = env->GetMethodID(cls, "<init>", "(Ljava/nio/ByteBuffer;Ljava/lang/ClassLoader;)V");
    jobject loader = env->NewObject(cls, ctor, buffer, parent);
    if (checkExc(env, "new InMemoryDexClassLoader")) return nullptr;
    return loader;
}

jobject loadDexFromFile(JNIEnv* env, jobject activity, jobject parent) {
    jclass ctx = env->FindClass("android/content/Context");
    jmethodID mDir = env->GetMethodID(ctx, "getCodeCacheDir", "()Ljava/io/File;");
    jobject dir = env->CallObjectMethod(activity, mDir);
    if (checkExc(env, "getCodeCacheDir") || !dir) return nullptr;
    jclass file = env->FindClass("java/io/File");
    jmethodID mPath = env->GetMethodID(file, "getAbsolutePath", "()Ljava/lang/String;");
    auto jdir = static_cast<jstring>(env->CallObjectMethod(dir, mPath));
    if (checkExc(env, "getAbsolutePath") || !jdir) return nullptr;
    const char* cdir = env->GetStringUTFChars(jdir, nullptr);
    const std::string path = std::string(cdir) + "/bunny_menu.dex";
    env->ReleaseStringUTFChars(jdir, cdir);

    unlink(path.c_str());
    FILE* f = std::fopen(path.c_str(), "wb");
    const bool written = f && std::fwrite(bl_cheatbridge_dex, 1, bl_cheatbridge_dex_len, f) == bl_cheatbridge_dex_len;
    if (f) std::fclose(f);
    if (!written) {
        BL_ERROR("botao: nao gravou o dex em %s (errno %d)", path.c_str(), errno);
        return nullptr;
    }

    jclass cls = env->FindClass("dalvik/system/DexClassLoader");
    if (checkExc(env, "DexClassLoader") || !cls) return nullptr;
    jmethodID ctor = env->GetMethodID(
        cls, "<init>", "(Ljava/lang/String;Ljava/lang/String;Ljava/lang/String;Ljava/lang/ClassLoader;)V");
    jstring jpath = env->NewStringUTF(path.c_str());
    jobject loader = env->NewObject(cls, ctor, jpath, jdir, nullptr, parent);
    if (checkExc(env, "new DexClassLoader")) return nullptr;
    BL_INFO("botao: dex carregado do arquivo (Android %d)", android_get_device_api_level());
    return loader;
}

}

void installCheatButton() {
    JavaVM* vm = appJavaVM();
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

    jobject appLoader = getAppClassLoader(env);
    if (!appLoader) { if (attached) vm->DetachCurrentThread(); return; }

    jclass unity = loadClass(env, appLoader, "com.unity3d.player.UnityPlayer");
    if (!unity) { if (attached) vm->DetachCurrentThread(); return; }
    jfieldID fAct = env->GetStaticFieldID(unity, "currentActivity", "Landroid/app/Activity;");
    jobject activity = fAct ? env->GetStaticObjectField(unity, fAct) : nullptr;
    if (checkExc(env, "currentActivity") || !activity) {
        BL_ERROR("botao: currentActivity nula (jogo ainda nao criou a Activity?)");
        if (attached) vm->DetachCurrentThread();
        return;
    }

    jobject dexLoader = android_get_device_api_level() >= 26
        ? loadDexInMemory(env, appLoader)
        : loadDexFromFile(env, activity, appLoader);
    if (!dexLoader) {
        if (attached) vm->DetachCurrentThread();
        return;
    }

    jclass bridge = loadClass(env, dexLoader, "bunny.CheatBridge");
    if (!bridge) { if (attached) vm->DetachCurrentThread(); return; }

    JNINativeMethod nm[] = {
        {"nOnGive", "(II)V", reinterpret_cast<void*>(&jni_onGive)},
        {"nOnSpawn", "(II)V", reinterpret_cast<void*>(&jni_onSpawn)},
        {"nItemNames", "()[Ljava/lang/String;", reinterpret_cast<void*>(&jni_itemNames)},
        {"nNpcNames", "()[Ljava/lang/String;", reinterpret_cast<void*>(&jni_npcNames)},
        {"nNpcFrames", "()[I", reinterpret_cast<void*>(&jni_npcFrames)},
        {"nNamesGeneration", "()I", reinterpret_cast<void*>(&jni_namesGeneration)},
        {"nItemClasses", "()[B", reinterpret_cast<void*>(&jni_itemClasses)},
        {"nSetPower", "(II)V", reinterpret_cast<void*>(&jni_setPower)},
        {"nInWorld", "()Z", reinterpret_cast<void*>(&jni_inWorld)},
        {"nSetTimeOfDay", "(I)V", reinterpret_cast<void*>(&jni_setTimeOfDay)},
        {"nWorldState", "()[I", reinterpret_cast<void*>(&jni_worldState)},
        {"nVanillaItemCount", "()I", reinterpret_cast<void*>(&jni_vanillaItemCount)},
        {"nModItemTextures", "()[Ljava/lang/String;", reinterpret_cast<void*>(&jni_modItemTextures)},
        {"nModCategories", "()[Ljava/lang/String;", reinterpret_cast<void*>(&jni_modCategories)},
        {"nModCategoryItems", "(I)[I", reinterpret_cast<void*>(&jni_modCategoryItems)},
        {"nVanillaNpcCount", "()I", reinterpret_cast<void*>(&jni_vanillaNpcCount)},
        {"nModNpcTextures", "()[Ljava/lang/String;", reinterpret_cast<void*>(&jni_modNpcTextures)},
        {"nItemSubClasses", "()[B", reinterpret_cast<void*>(&jni_itemSubClasses)},
        {"nNpcClasses", "()[B", reinterpret_cast<void*>(&jni_npcClasses)},
        {"nBuffNames", "()[Ljava/lang/String;", reinterpret_cast<void*>(&jni_buffNames)},
        {"nBuffClasses", "()[B", reinterpret_cast<void*>(&jni_buffClasses)},
        {"nOnBuff", "(II)V", reinterpret_cast<void*>(&jni_onBuff)},
        {"nVanillaBuffCount", "()I", reinterpret_cast<void*>(&jni_vanillaBuffCount)},
        {"nModBuffTextures", "()[Ljava/lang/String;", reinterpret_cast<void*>(&jni_modBuffTextures)},
        {"nConsoleRun", "(Ljava/lang/String;)V", reinterpret_cast<void*>(&jni_consoleRun)},
        {"nConsoleTake", "()[Ljava/lang/String;", reinterpret_cast<void*>(&jni_consoleTake)},
        {"nRequestRestart", "(Z)V", reinterpret_cast<void*>(&jni_requestRestart)},
        {"nRestartReady", "()Z", reinterpret_cast<void*>(&jni_restartReady)},
        {"nSetGameFrozen", "(Z)V", reinterpret_cast<void*>(&jni_setGameFrozen)},
        {"nCheatsBlocked", "()Z", reinterpret_cast<void*>(&jni_cheatsBlocked)},
    };
    if (env->RegisterNatives(bridge, nm, sizeof(nm) / sizeof(nm[0])) != JNI_OK) {
        checkExc(env, "RegisterNatives");
        if (attached) vm->DetachCurrentThread();
        return;
    }

    jmethodID mInstall = env->GetStaticMethodID(bridge, "install", "(Landroid/app/Activity;)V");
    if (!mInstall) {
        checkExc(env, "install id");
        if (attached) vm->DetachCurrentThread();
        return;
    }
    env->CallStaticVoidMethod(bridge, mInstall, activity);
    if (!checkExc(env, "install()")) {
        BL_DEBUG("botao: instalado na Activity do jogo");
    }

    g_bridge = reinterpret_cast<jclass>(env->NewGlobalRef(bridge));
    g_showError = env->GetStaticMethodID(g_bridge, "showError", "(Ljava/lang/String;)V");
    if (g_showError && config().showErrors) log::onError(&onNativeError);

    if (attached) vm->DetachCurrentThread();
}

}
