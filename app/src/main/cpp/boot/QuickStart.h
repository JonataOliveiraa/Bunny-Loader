#pragma once
#include <chrono>

namespace bl::runtime {

void installSplashHook();

void markProbeFinished();
bool gameInitialized();

void waitForGameLoaded(std::chrono::steady_clock::time_point deadline);

void markCoreReady();
bool isCoreReady();

void tickQuickStart();

}
