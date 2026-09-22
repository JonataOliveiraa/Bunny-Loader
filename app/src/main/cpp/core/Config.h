#pragma once
#include <cstdint>
#include <string>
#include <vector>

namespace bl {

struct Config {
    std::string gameLibDir;
    std::string modsDir;
    std::vector<std::string> enabledMods;
    std::string logPath;
    int64_t gameVersion = 0;
};

inline Config& config() {
    static Config instance;
    return instance;
}

// Caminho B (lançar + injetar): a libbunny.so e pre-carregada no processo do
// jogo, sem codigo Java nosso la dentro. Entao a config nao vem por JNI — vem
// de um arquivo texto simples (key=value por linha) que o launcher grava antes
// de iniciar o jogo. enabledMods e uma lista separada por virgula.
//
// Retorna false se o arquivo nao existir/nao abrir; nesse caso o chamador
// deve abortar o boot silenciosamente (o processo pode nem ser o do jogo).
bool loadConfigFromFile(const char* path);

// Local acordado entre o launcher e a lib injetada.
constexpr const char* kDefaultConfigPath = "/data/local/tmp/bunny/config";

} // namespace bl
