#include "mods/ModLoader.h"
#include "script/bridge/ScriptEngine.h"
#include <filesystem>
#include <fstream>
#include <iostream>
#include <stdexcept>

namespace fs = std::filesystem;
namespace bl::script {
ScriptEngine& engine() {
    static ScriptEngine instance;
    return instance;
}
}

int main() {
    const auto root = fs::temp_directory_path() / "bunny-server-sync-metadata-test";
    fs::remove_all(root);
    fs::create_directories(root / "probe/content");
    std::ofstream(root / "probe/content/main.js") << "export default class extends Mod {}";
    int checks = 0;
    auto check = [&](const char* version) {
        bl::mods::loadAll(root.string(), {{"probe", "main.js"}});
        const auto* mod = bl::mods::get(0);
        if (!mod || mod->version != version) {
            throw std::runtime_error(std::string("expected version ") + version + ", got " + (mod ? mod->version : "no mod"));
        }
        ++checks;
    };
    try {
        std::ofstream(root / "probe/manifest.json") << R"({"version":"1.1.0","name":"Probe","id":"probe"})";
        check("1.1.0");
        fs::remove(root / "probe/manifest.json");
        std::ofstream(root / "probe/mod.json") << R"({"version":"1.0.0","name":"Probe","id":"probe"})";
        check("1.0.0");
        std::ofstream(root / "probe/mod.json") << R"({"authors":[{"name":"Author","version":"99"}],"version":"1.1.0"})";
        check("1.1.0");
        std::ofstream(root / "probe/manifest.json") << R"({"version":"2.0.0"})";
        check("2.0.0");
        std::ofstream(root / "probe/manifest.json") << R"({"name":"Missing version"})";
        check("");
        fs::remove(root / "probe/manifest.json");
        fs::create_directory(root / "probe/manifest.json");
        check("1.1.0");
        fs::remove(root / "probe/mod.json");
        check("");
    } catch (...) {
        fs::remove_all(root);
        throw;
    }
    fs::remove_all(root);
    std::cout << "PASS native manifest metadata: " << checks << " checks\n";
}
