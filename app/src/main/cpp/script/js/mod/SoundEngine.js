class SoundEngine {
    // Um SoundStyle de mod ou um SoundID do jogo, na posição (Vector2) ou sem
    // distância. Som de mod devolve um pedido assíncrono (0 = recusado).
    static PlaySound(style, position) {
        const sound = SoundLoader.Of(style);
        if (sound) return position ? SoundLoader.Play(sound, position.X, position.Y, 1, 0) : SoundLoader.Play(sound, -1, -1, 1, 0);

        const Engine = Terraria.Audio.SoundEngine;
        // SoundID.MenuTick, SoundID.Mech...: no celular são o número do tipo antigo.
        if (typeof style === 'number') {
            const x = position ? Math.floor(position.X) : -1, y = position ? Math.floor(position.Y) : -1;
            return Engine['SoundEffectInstance PlaySound(int type, int x, int y, int Style, float volumeScale, float pitchOffset)'](style, x, y, 1, 1, 0);
        }
        return position
            ? Engine['SoundEffectInstance PlaySound(LegacySoundStyle type, Vector2 position, float pitchOffset, float volumeScale)'](style, position, 0, 1)
            : Engine['SoundEffectInstance PlaySound(LegacySoundStyle type, int x, int y, float pitchOffset, float volumeScale)'](style, -1, -1, 0, 1);
    }

    static FindActiveSound(style) {
        const sound = SoundLoader.Of(style);
        if (!sound) return 0;

        return bl.sounds.active(sound.group);
    }

    // -1 falhou; 0 terminou/desconhecido; 1 pendente; 2 tocando; 3 pausado;
    // 4 cancelado; 5 descartado por atraso. Histórico limitado aos últimos pedidos.
    static GetSoundState(handle) {
        return handle > 0 ? bl.sounds.playbackState(handle) : 0;
    }

    static StopSound(handle) {
        if (handle > 0) bl.sounds.cancel(handle);
    }
}

Object.freeze(SoundEngine);
