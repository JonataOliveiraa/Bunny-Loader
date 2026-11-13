#pragma once

#include <cstdint>

namespace bl::runtime {

int gameThreadId();
void noteGameThread();

uint32_t gameUpdateTick();

void boot();

}
