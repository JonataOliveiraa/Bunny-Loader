#pragma once
#include <chrono>

namespace bl::runtime {

void installSplashHook();

void markProbeFinished();
bool gameInitialized();

void waitForGameLoaded(std::chrono::milliseconds max);

void markCoreReady();
bool isCoreReady();

void tickQuickStart();

}
