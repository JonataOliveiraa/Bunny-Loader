class SoundEngine {
    // Um SoundStyle de mod ou um SoundID do jogo, na posição (Vector2) ou sem
    // distância. Som de mod devolve o stream (0 = não tocou).
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

        const now = Date.now();
        sound.playing = sound.playing.filter((p) => p.end > now);
        return sound.playing.length ? sound.playing[sound.playing.length - 1].stream : 0;
    }

    static StopSound(stream) {
        if (stream > 0) bl.sounds.stop(stream);
    }
}

Object.freeze(SoundEngine);
