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
    // Arquivo de comando do canal de DEV (adb). Fica na pasta externa do
    // proprio Bunny Loader; o caminho vem de cima porque o nativo nao conhece
    // o nome do pacote. Vazio = canal desligado.
    std::string cmdPath;
    // Painel de erro DENTRO do jogo (Configuracoes). Desligado, um mod que
    // quebra so deixa rastro no logcat — util para quem grava video.
    bool showErrors = true;
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

// Pasta base acordada entre o provisionamento e a lib injetada.
//
// Fica na pasta externa DO PROPRIO app do jogo: o processo do jogo (mesmo uid)
// sempre pode ler a sua getExternalFilesDir sob SELinux Enforcing, sem root, em
// qualquer Android. O adb push tambem escreve la sem root. (/data/local/tmp so
// funciona em emulador Permissive ou com root.)
constexpr const char* kBunnyBaseDir =
    "/sdcard/Android/data/com.and.games505.TerrariaPaid/files/bunny";
constexpr const char* kDefaultConfigPath =
    "/sdcard/Android/data/com.and.games505.TerrariaPaid/files/bunny/config";

} // namespace bl
