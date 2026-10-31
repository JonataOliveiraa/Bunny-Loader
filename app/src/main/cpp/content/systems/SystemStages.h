#pragma once
#include <cstdint>

namespace bl::runtime {

enum SystemStage : int32_t {
    kStagePlayersBegin = 0,
    kStagePlayersEnd = 1,
    kStageNpcsBegin = 2,
    kStageNpcsEnd = 3,
    kStageGoresEnd = 4,
    kStageItemsBegin = 5,
    kStageItemsEnd = 6,
    kStageTransform = 7,
    kStageClockAdvance = 8,
    kSystemStageCount = 9,
};

enum SystemStageClient : int32_t { kStageClientJs = 0, kStageClientManaged = 1, kStageClientCount = 2 };

using SystemStageFn = void (*)(int32_t stage, double* value);

int32_t systemStageIndex(const char* name);
bool installSystemStage(int32_t stage, int32_t client, SystemStageFn fn);
const char* systemStageName(int32_t stage);

}
