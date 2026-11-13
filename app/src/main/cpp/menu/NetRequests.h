#pragma once
#include <string>

namespace bl::runtime {

void installNetRequests();

int netMode();
inline bool isNetClientOnly() { return netMode() == 1; }
inline bool isNetHost() { return (netMode() & 2) != 0; }

void sendData(int msgType, int number);
void sendData(int msgType, int number, float number2, float number3);

bool sendToServer(const std::string& command);

void tickNetRequests();

}
