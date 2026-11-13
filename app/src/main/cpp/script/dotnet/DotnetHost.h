#pragma once
#include <string>

namespace bl::dotnet {

class DotnetHost {
public:
    bool loadMod(const std::string& id, const std::string& path);

    void finishLoading();

    bool started() const { return started_; }

    bool consoleSubmit(const std::string& code);

    void consoleTick();

private:
    bool start();

    bool started_ = false;
    bool failed_ = false;
    int (*loadModFn_)(const char* id, const char* path) = nullptr;
    void (*consoleSubmitFn_)(const char* code) = nullptr;
    void (*consoleTickFn_)() = nullptr;
};

DotnetHost& host();

bool isAssemblyEntry(const std::string& entry);

bool isCSharpEntry(const std::string& entry);

}
