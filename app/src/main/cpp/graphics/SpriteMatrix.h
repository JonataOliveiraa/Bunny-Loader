#pragma once
#include <cstdint>

namespace bl::graphics {

int32_t spriteMatrixBegin(void* batch, const float* matrix, int32_t immediate);

int32_t spriteMatrixGet(void* batch, float* matrix);

void spriteMatrixDrop(void* batch);

int32_t* spriteMatrixPending();

}
