#pragma once
#include <android/log.h>
#include <string>

namespace bl::log {

void open(const char* path);

/**
 * Liga as linhas BL_DEBUG no arquivo de sessao ("Log detalhado" nas
 * Configuracoes). Desligado, elas vao so ao logcat.
 *
 * O arquivo e o que o modder abre, e o nucleo narrando o boot (cada tabela
 * aumentada, cada hook, cada tipo registrado) ocupava ~27 dos ~29 KB de uma
 * sessao com o ExampleMod — as linhas do mod sumiam no meio.
 */
void setVerbose(bool on);

void write(int level, const char* fmt, ...) __attribute__((format(printf, 2, 3)));

// Erros acumulados desde o boot, para o painel dentro do jogo.
//
// Existem porque o logcat nao esta ao alcance de quem so tem o celular: sem
// isto, um mod que quebra vira "nao funcionou" sem texto nenhum.
std::string errorsSoFar();
int errorCount();

// Chamado a cada BL_ERROR. Quem instala decide o que fazer (mostrar o painel).
void onError(void (*fn)(const char* line));

} // namespace bl::log

// DEBUG: detalhe do nucleo (tabela por tabela, hook por hook). INFO: o que o
// modder precisa ver — resumo de cada sistema e as linhas do mod.
#define BL_DEBUG(...) bl::log::write(ANDROID_LOG_DEBUG, __VA_ARGS__)
#define BL_INFO(...)  bl::log::write(ANDROID_LOG_INFO,  __VA_ARGS__)
#define BL_WARN(...)  bl::log::write(ANDROID_LOG_WARN,  __VA_ARGS__)
#define BL_ERROR(...) bl::log::write(ANDROID_LOG_ERROR, __VA_ARGS__)
