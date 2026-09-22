#pragma once
#include <android/log.h>

namespace bl::log {

void open(const char* path);
void write(int level, const char* fmt, ...);

} // namespace bl::log

#define BL_INFO(...)  bl::log::write(ANDROID_LOG_INFO,  __VA_ARGS__)
#define BL_WARN(...)  bl::log::write(ANDROID_LOG_WARN,  __VA_ARGS__)
#define BL_ERROR(...) bl::log::write(ANDROID_LOG_ERROR, __VA_ARGS__)
