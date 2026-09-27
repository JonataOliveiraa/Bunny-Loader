// A Unity deste build não cria AudioClip novo: o som toca pelo Android
// (bl.sounds, SoundPool), e o jogo continua decidindo quando e quanto.
//
// O SoundStyle devolve um LegacySoundStyle MARCADOR do jogo (SoundId 1000,
// Style = o índice do som): é o tipo de Item.UseSound e NPC.HitSound. Todo som
// do jogo passa por LegacySoundPlayer.PlaySound, e o hook ali toca o nosso.
class SoundLoader {
    static ID = 1000;
    static Sounds = [];               // índice (o Style do marcador) -> som
    static Styles = new Map();        // arquivo + opções -> marcador

    static #ids = new Map();          // arquivo -> id do bl.sounds
    static #attenuation = 0;

    static Load(file, label) {
        let id = SoundLoader.#ids.get(file) || 0;
        if (!id) {
            id = Safe.Run('SoundStyle ' + label, () => bl.sounds.load(file)) || 0;
            SoundLoader.#ids.set(file, id);
        }
        return id;
    }

    static Of(style) {
        if (!style || typeof style !== 'object' || style.SoundId !== SoundLoader.ID) return undefined;

        return SoundLoader.Sounds[style.Style];
    }

    // Volume e pan como o LegacySoundPlayer: sem posição (x = -1), cheio; com
    // posição, cai com a distância ao centro da tela. Devolve o stream, ou 0.
    static Play(sound, x, y, volumeScale, pitchOffset) {
        if (!sound || !sound.id) return 0;

        const Main = Terraria.Main;
        let volume = 1, pan = 0;
        if (x !== -1 && y !== -1) {
            const attenuation = SoundLoader.#Attenuation();
            const screen = Main.screenPosition;
            const halfWidth = Main.screenWidth / 2;
            const cx = screen.X + halfWidth;
            const cy = screen.Y + Main.screenHeight / 2;
            const distance = Math.hypot(x - cx, y - cy);
            if (distance >= attenuation) return 0;

            pan = Math.max(-1, Math.min(1, (x - cx) / halfWidth));
            volume = 1 - distance / attenuation;
        }

        volume *= sound.volume * volumeScale * Main.soundVolume;
        if (!(volume > 0)) return 0;

        const now = Date.now();
        sound.playing = sound.playing.filter((p) => p.end > now);
        if (sound.maxInstances > 0 && sound.playing.length >= sound.maxInstances) {
            if (sound.limit === SoundLimitBehavior.IgnoreNew) return 0;
            bl.sounds.stop(sound.playing.shift().stream);
        }

        // Tom em oitavas, como o SoundEffectInstance.Pitch: velocidade = 2^tom.
        const rate = Math.pow(2, sound.pitch + (Math.random() - 0.5) * sound.pitchVariance + pitchOffset);
        const stream = bl.sounds.play(sound.id, volume * Math.min(1, 1 - pan), volume * Math.min(1, 1 + pan), rate);
        if (stream > 0) {
            if (!sound.duration) sound.duration = bl.sounds.duration(sound.id) || 1000;
            sound.playing.push({ stream, end: now + sound.duration / Math.max(0.5, Math.min(2, rate)) });
        }
        return stream;
    }

    static Install() {
        Hooks.Once('sound.Play', () => {
            Terraria.Audio.LegacySoundPlayer['SoundEffectInstance PlaySound(int type, int x, int y, int Style, float volumeScale, float pitchOffset)'].hook(
                (original, self, type, x, y, style, volumeScale, pitchOffset) => {
                    if (type !== SoundLoader.ID) return original(self, type, x, y, style, volumeScale, pitchOffset);

                    SoundLoader.Play(SoundLoader.Sounds[style], x, y, volumeScale, pitchOffset);
                    return null;
                });
        });
    }

    static #Attenuation() {
        if (!SoundLoader.#attenuation) {
            SoundLoader.#attenuation = Terraria.Audio.LegacySoundPlayer.SoundAttenuationDistance || 2500;
        }
        return SoundLoader.#attenuation;
    }
}
