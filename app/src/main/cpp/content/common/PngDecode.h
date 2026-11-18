#pragma once
#include <cstddef>
#include <cstdint>
#include <string>
#include <vector>

namespace bl::runtime::content {

bool decodePng(const uint8_t* data, size_t size, bool bottomUp, int* width, int* height,
               std::vector<uint8_t>& rgba, std::string* error);

bool decodePngFile(const std::string& path, bool bottomUp, int* width, int* height,
                   std::vector<uint8_t>& rgba, std::string* error);

}
