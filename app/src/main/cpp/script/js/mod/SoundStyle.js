// new SoundStyle('Sounds/Tiro', { Volume, Pitch, PitchVariance, MaxInstances,
// SoundLimitBehavior }). O mesmo arquivo com as mesmas opções devolve o mesmo
// objeto: criar no SetDefaults não custa nada.
class SoundStyle {
    constructor(path, options) {
        const o = options || {};
        const option = (key, fallback) => {
            const v = o[key] !== undefined ? o[key] : o[key[0].toLowerCase() + key.slice(1)];
            return v === undefined ? fallback : v;
        };

        const sound = {
            file: ModFiles.Audio('Sounds', null, path),
            volume: Number(option('Volume', 1)),
            pitch: Number(option('Pitch', 0)),
            pitchVariance: Number(option('PitchVariance', 0)),
            maxInstances: option('MaxInstances', 1) | 0,
            limit: option('SoundLimitBehavior', SoundLimitBehavior.ReplaceOldest),
        };
        const key = [sound.file || path, sound.volume, sound.pitch, sound.pitchVariance, sound.maxInstances, sound.limit].join('|');
        const cached = SoundLoader.Styles.get(key);
        if (cached) return cached;

        // Sem lançar: fica mudo, e o log diz por quê.
        if (!sound.file) {
            Safe.Once('som:' + path, "SoundStyle: nao achei '" + path + "' (" + ModFiles.AUDIO.join(', ') + ') em Assets/Sounds');
        }
        sound.id = sound.file ? SoundLoader.Load(sound.file, path) : 0;
        const index = SoundLoader.Sounds.length;
        sound.group = index;
        SoundLoader.Sounds.push(sound);

        const marker = Terraria.Audio.LegacySoundStyle.new();
        marker['void .ctor(int soundId, int style, SoundType type, int maxTrackedInstances)'](SoundLoader.ID, index, 0, 0);
        SoundLoader.Styles.set(key, marker);
        SoundLoader.Install();
        return marker;
    }
}
