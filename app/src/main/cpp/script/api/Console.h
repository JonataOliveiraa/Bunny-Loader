#pragma once
#include <string>
#include <vector>

namespace bl::script {

void consoleSubmit(std::string code);
std::vector<std::string> consoleTake();

void tickConsole();

void consolePrint(char kind, const std::string& text);

void installConsoleLog();

}
