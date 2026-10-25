#pragma once
#include <jni.h>

namespace bl {

/** Guardada pelo JNI_OnLoad da libbunny (boot/jni_entry.cpp). */
void setAppJavaVM(JavaVM* vm);

/**
 * A JavaVM do app. A do JNI_OnLoad; sem ela (libbunny carregada por fora do
 * System.loadLibrary), JNI_GetCreatedJavaVMs, que so se alcanca no Android 12+.
 * nullptr se nenhuma das duas.
 */
JavaVM* appJavaVM();

/**
 * O JNIEnv desta thread, anexando-a a JavaVM se preciso (e desanexando quando
 * a thread acaba). nullptr sem JavaVM.
 */
JNIEnv* appJniEnv();

/**
 * Uma classe do app pelo classloader do app (`dev.bunnyloader.game.X`): o
 * FindClass de uma thread anexada so enxerga as do sistema. Referencia local;
 * nullptr (e a excecao Java limpa) se nao achar.
 */
jclass appJniClass(JNIEnv* env, const char* dotted);

/** Limpa uma excecao Java pendente, logando onde foi. true se nao havia. */
bool appJniCleared(JNIEnv* env, const char* where);

} // namespace bl
