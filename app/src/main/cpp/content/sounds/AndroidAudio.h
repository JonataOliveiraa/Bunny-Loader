#pragma once
#include <string>

namespace bl::runtime::content {

/**
 * Sons de mod pelo SoundPool do app (dev.bunnyloader.game.ModAudio), por JNI.
 *
 * A Unity deste build nao cria AudioClip novo (ver ModAudio.kt), entao o som
 * de mod toca fora dela. Tudo aqui vale em qualquer thread: a que nao for do
 * Java e anexada na primeira chamada e desanexada quando termina.
 */

/** Comeca a carregar o arquivo (assincrono). O id do som, ou 0 se recusou. */
int loadSound(const std::string& path);

/** 1 pronto, 0 carregando, -1 falhou (o Android nao decodificou o arquivo). */
int soundState(int id);

/** Duracao do som em ms (0 se o arquivo nao disse). */
int soundDuration(int id);

/** Volume por lado 0..1, velocidade 0,5..2. O stream, ou 0 se nao tocou. */
int playSound(int id, float left, float right, float rate);

void stopSound(int stream);

/** Musica (MediaPlayer, em streaming, em laco). Registra sem ler. O id, ou 0. */
int registerMusic(const std::string& path);

/** Volume agora (0..1, com o fade). Acima de 0 toca; em 0 para e volta ao comeco. */
void setMusicVolume(int id, float volume);

/** -1 falhou, 0 parada, 1 preparando, 2 tocando. */
int musicState(int id);

} // namespace bl::runtime::content
