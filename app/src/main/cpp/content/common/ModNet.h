#pragma once
#include <cstdint>
#include <string>

#include "content/common/ContentAssets.h"

namespace bl::runtime {

using ManagedPacketHandler = void (*)(const uint8_t* data, int32_t length, int32_t from);

constexpr uint16_t kManagedNetModule = 0xB17F;

bool installManagedNet(ManagedPacketHandler handler);
bool sendManagedPacket(const uint8_t* data, int32_t length, int32_t toClient, int32_t ignoreClient);
int32_t gameNetMode();
void setManagedHello(const std::string& hello, const content::CultureNames& refusal);

}
