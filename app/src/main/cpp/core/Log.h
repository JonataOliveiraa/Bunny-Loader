#pragma once
#include <android/log.h>
#include <string>

namespace bl::log {

void open(const char* path);

void setVerbose(bool on);

void write(int level, const char* fmt, ...) __attribute__((format(printf, 2, 3)));

std::string errorsSoFar();
int errorCount();

void onError(void (*fn)(const char* line));

void onLine(void (*fn)(int level, const char* line));

}

#define BL_DEBUG(...) bl::log::write(ANDROID_LOG_DEBUG, __VA_ARGS__)
#define BL_INFO(...)  bl::log::write(ANDROID_LOG_INFO,  __VA_ARGS__)
#define BL_WARN(...)  bl::log::write(ANDROID_LOG_WARN,  __VA_ARGS__)
#define BL_ERROR(...) bl::log::write(ANDROID_LOG_ERROR, __VA_ARGS__)
