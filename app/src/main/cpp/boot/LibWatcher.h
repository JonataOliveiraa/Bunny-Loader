#pragma once
#include <chrono>

namespace bl::loader {

bool installWatcher();
bool watcherInstalled();
bool waitForIl2cppInit(std::chrono::steady_clock::time_point deadline);
void waitForIl2cppInit();

}
