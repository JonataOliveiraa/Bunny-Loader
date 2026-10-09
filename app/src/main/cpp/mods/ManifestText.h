#pragma once
#include <string>
#include <vector>

namespace bl::mods {

/** O texto de uma chave do objeto de fora do manifesto ("id", "name"...); vazio se nao ha. */
std::string manifestString(const std::string& txt, const char* key);

/** Uma lista de textos (`"dependencies": ["a", "b"]`); o que nao e texto fica de fora. */
std::vector<std::string> manifestStrings(const std::string& txt, const char* key);

} // namespace bl::mods
