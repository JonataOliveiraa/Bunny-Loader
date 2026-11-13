#pragma once
#include <cstdint>

namespace bl::graphics {

enum class GpuChannel { Effect, Frame };

class GpuTimer {
public:
    static void Enable();
    static bool Begin(GpuChannel channel);
    static void End(GpuChannel channel);
    static bool Available();
    static void Totals(GpuChannel channel, int64_t& totalNs, int64_t& samples);
};

}
