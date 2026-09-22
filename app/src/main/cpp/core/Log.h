#pragma once
#include <android/log.h>
#include <string>

namespace bl::log {

void open(const char* path);
void write(int level, const char* fmt, ...);

// Erros acumulados desde o boot, para o painel dentro do jogo.
//
// Existem porque o logcat nao esta ao alcance de quem so tem o celular: sem
// isto, um mod que quebra vira "nao funcionou" sem texto nenhum.
std::string errorsSoFar();
int errorCount();

// Chamado a cada BL_ERROR. Quem instala decide o que fazer (mostrar o painel).
void onError(void (*fn)(const char* line));

} // namespace bl::log

#define BL_INFO(...)  bl::log::write(ANDROID_LOG_INFO,  __VA_ARGS__)
#define BL_WARN(...)  bl::log::write(ANDROID_LOG_WARN,  __VA_ARGS__)
#define BL_ERROR(...) bl::log::write(ANDROID_LOG_ERROR, __VA_ARGS__)
