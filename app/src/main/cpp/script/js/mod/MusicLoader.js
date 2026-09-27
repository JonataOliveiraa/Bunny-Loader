class MusicLoader {
    static get MusicCount() { return ModMusic.Base() + ModMusic.Tracks.length; }

    // GetMusicSlot('Music/Chefe') ou GetMusicSlot(mod, 'Music/Chefe'). 0 se não existe.
    static GetMusicSlot(modOrPath, path) {
        const base = path !== undefined && modOrPath && modOrPath.path ? modOrPath.path : null;
        const rel = path !== undefined ? path : modOrPath;
        const file = ModFiles.Audio('Music', base, rel);
        if (!file) {
            Safe.Once('musica:' + rel, "MusicLoader: nao achei '" + rel + "' (" + ModFiles.AUDIO.join(', ') + ') em Assets/Music');
            return 0;
        }

        let slot = ModMusic.Slots.get(file);
        if (slot) return slot;

        const id = Safe.Run('MusicLoader ' + rel, () => bl.music.register(file)) || 0;
        if (!id) return 0;

        slot = ModMusic.Base() + ModMusic.Tracks.length;
        ModMusic.Tracks.push({ id, file, fade: 0, sent: 0 });
        ModMusic.Slots.set(file, slot);
        ModMusic.Install();
        return slot;
    }

    static MusicExists(modOrPath, path) {
        const base = path !== undefined && modOrPath && modOrPath.path ? modOrPath.path : null;
        return !!ModFiles.Audio('Music', base, path !== undefined ? path : modOrPath);
    }

    static IsMusicPlaying(slot) {
        const track = ModMusic.Track(slot);
        return !!track && bl.music.state(track.id) === 2;
    }
}

Object.freeze(MusicLoader);
