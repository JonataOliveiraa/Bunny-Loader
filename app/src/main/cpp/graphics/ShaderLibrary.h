#pragma once
#include <string>

namespace bl::graphics {

class ShaderLibrary {
public:
    static const char* Vertex();

    static std::string BuildFragment(const std::string& source, std::string& error);

    static std::string Describe(const std::string& log, const std::string& name, const std::string& source);
};

}
