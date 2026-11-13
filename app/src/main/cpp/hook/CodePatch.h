#pragma once
#include <cstdint>
#include <string>

#include "il2cpp/Types.h"

namespace bl::runtime {

int patchCompareLimit(const MethodInfo* m, uint32_t oldLimit, uint32_t newLimit, bool belowToo = false,
                      bool shifted = false);

std::string describeCompareMiss(const MethodInfo* m, uint32_t oldLimit, uint32_t newLimit,
                                bool belowToo = false, bool shifted = false);

std::string describeMethodCode(const MethodInfo* m);

int patchLoopEnd(const MethodInfo* m, uint32_t oldEnd, uint32_t newEnd, bool exitOnEqual = false);

int patchRegisterLimit(const MethodInfo* m, uint32_t oldLimit, uint32_t newLimit);

int patchMovImmediate(const MethodInfo* m, uint32_t oldValue, uint32_t newValue);

bool replaceInstruction(const void* code, size_t offset, uint32_t expected, uint32_t replacement);

}
