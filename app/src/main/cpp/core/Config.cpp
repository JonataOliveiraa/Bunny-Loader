#include "core/Config.h"
#include "core/Log.h"
#include <cstdio>
#include <cstring>
#include <string>

namespace bl {

namespace {

// Remove espacos/CR das pontas.
std::string trim(const std::string& s) {
    size_t a = s.find_first_not_of(" \t\r\n");
    if (a == std::string::npos) return {};
    size_t b = s.find_last_not_of(" \t\r\n");
    return s.substr(a, b - a + 1);
}

void splitCsv(const std::string& value, std::vector<std::string>& out) {
    out.clear();
    size_t start = 0;
    while (start <= value.size()) {
        size_t comma = value.find(',', start);
        std::string part = trim(value.substr(
            start, comma == std::string::npos ? std::string::npos : comma - start));
        if (!part.empty()) out.push_back(part);
        if (comma == std::string::npos) break;
        start = comma + 1;
    }
}

} // namespace

bool loadConfigFromFile(const char* path) {
    FILE* f = fopen(path, "r");
    if (!f) return false;

    auto& c = config();
    char line[4096];
    while (fgets(line, sizeof(line), f)) {
        std::string s = trim(line);
        if (s.empty() || s[0] == '#') continue;
        size_t eq = s.find('=');
        if (eq == std::string::npos) continue;

        std::string key = trim(s.substr(0, eq));
        std::string val = trim(s.substr(eq + 1));

        if (key == "gameLibDir")       c.gameLibDir = val;
        else if (key == "modsDir")     c.modsDir = val;
        else if (key == "logPath")     c.logPath = val;
        else if (key == "cmdPath")     c.cmdPath = val;
        else if (key == "showErrors")  c.showErrors = (val != "false" && val != "0");
        else if (key == "gameVersion") c.gameVersion = strtoll(val.c_str(), nullptr, 10);
        else if (key == "enabledMods") splitCsv(val, c.enabledMods);
    }
    fclose(f);
    return true;
}

} // namespace bl
