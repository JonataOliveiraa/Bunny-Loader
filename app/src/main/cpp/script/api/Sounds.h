#pragma once
#include "script/bridge/ScriptEngine.h"

#if BL_HAVE_QUICKJS
#include "quickjs.h"

namespace bl::script {

/**
 * bl.sounds e bl.music: audio de mod pelo Android (a Unity deste build nao
 * cria AudioClip novo; ver content/sounds/AndroidAudio.h). E o piso: os mods
 * usam o SoundStyle, o SoundEngine e o MusicLoader do ModClasses.js.
 *
 *   bl.sounds.load(caminho)             -> id; comeca a carregar (OGG, MP3, WAV...)
 *   bl.sounds.state(id)                 -> 1 pronto, 0 carregando, -1 falhou
 *   bl.sounds.duration(id)              -> ms (0 se o arquivo nao disse)
 *   bl.sounds.play(id, esq, dir, veloc) -> stream (0 se ainda nao carregou)
 *   bl.sounds.stop(stream)
 *
 *   bl.music.register(caminho)          -> id (nada e lido ainda)
 *   bl.music.setVolume(id, volume)      -> a cada quadro; 0 para
 *   bl.music.state(id)                  -> -1 falhou, 0 parada, 1 preparando, 2 tocando
 *
 * Caminho relativo a pasta do mod de quem chama, como no bl.loadTexture.
 * Qualquer thread.
 */
void installSoundsApi(JSContext* ctx, JSValue bl);

} // namespace bl::script
#endif
