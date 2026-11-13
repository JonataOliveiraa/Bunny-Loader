#pragma once
#include <jni.h>

namespace bl {

void setAppJavaVM(JavaVM* vm);

JavaVM* appJavaVM();

JNIEnv* appJniEnv();

jclass appJniClass(JNIEnv* env, const char* dotted);

bool appJniCleared(JNIEnv* env, const char* where);

}
