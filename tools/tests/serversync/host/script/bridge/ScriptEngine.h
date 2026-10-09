#pragma once
#include <string>
#include <vector>
namespace bl::script {
class ScriptEngine {
public:
    bool ready() const { return false; }
    bool loadMod(const std::string&, const std::vector<std::string>&) { return false; }
    bool loadBuiltinMod(const std::string&, const std::string&) { return false; }
};
ScriptEngine& engine();
}
