#pragma once

namespace bl::runtime {

using NaturalRegenCallback = float (*)(void* player, float regen);

bool setNaturalRegenCallback(NaturalRegenCallback callback);

}
