#include "mods/ManifestText.h"

namespace bl::mods {

namespace {
size_t skipSpace(const std::string& txt, size_t i) {
    while (i < txt.size() && (txt[i] == ' ' || txt[i] == '\t' || txt[i] == '\r' || txt[i] == '\n')) ++i;
    return i;
}

// Le a string que comeca nas aspas de `i`; devolve o indice depois dela.
size_t readString(const std::string& txt, size_t i, std::string* out) {
    for (++i; i < txt.size() && txt[i] != '"'; ++i) {
        if (txt[i] == '\\' && i + 1 < txt.size()) ++i;
        if (out) *out += txt[i];
    }
    return i + 1;
}

/**
 * Onde comeca o valor de uma chave do manifesto ("id", "name"...), ou npos.
 * Lido a mao: sao poucos campos, e puxar um parser de JSON para o nucleo por
 * causa deles nao se paga.
 *
 * So valem as chaves do objeto de FORA. `authors` tem um "name" por autor, e
 * um manifesto que lista os autores antes do nome do mod fazia o jogo chamar o
 * mod pelo nome do primeiro autor. Texto entre aspas (chave ou valor) nunca
 * conta como chave ou chave/colchete.
 */
size_t manifestValue(const std::string& txt, const char* key) {
    int depth = 0;
    for (size_t i = 0; i < txt.size();) {
        const char c = txt[i];
        if (c == '{' || c == '[') { ++depth; ++i; continue; }
        if (c == '}' || c == ']') { --depth; ++i; continue; }
        if (c != '"') { ++i; continue; }
        std::string name;
        size_t after = readString(txt, i, &name);
        size_t colon = skipSpace(txt, after);
        if (depth == 1 && colon < txt.size() && txt[colon] == ':' && name == key) {
            size_t v = skipSpace(txt, colon + 1);
            return v < txt.size() ? v : std::string::npos;
        }
        i = after;
    }
    return std::string::npos;
}
}

std::string manifestString(const std::string& txt, const char* key) {
    const size_t v = manifestValue(txt, key);
    if (v == std::string::npos || txt[v] != '"') return {};
    std::string out;
    readString(txt, v, &out);
    return out;
}

std::vector<std::string> manifestStrings(const std::string& txt, const char* key) {
    std::vector<std::string> out;
    size_t i = manifestValue(txt, key);
    if (i == std::string::npos || txt[i] != '[') return out;
    int depth = 0;
    while (i < txt.size()) {
        const char c = txt[i];
        if (c == '"') {
            std::string value;
            i = readString(txt, i, &value);
            if (depth == 1 && !value.empty()) out.push_back(value);
            continue;
        }
        if (c == '[' || c == '{') ++depth;
        if ((c == ']' || c == '}') && --depth == 0) break;
        ++i;
    }
    return out;
}

} // namespace bl::mods
