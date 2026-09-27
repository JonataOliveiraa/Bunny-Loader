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

    // A caixa de música de mod: o tile `tileType` (2x2, como a do jogo) toca a
    // faixa `musicSlot` quando ligado (quadro X a partir de 36) e na tela.
    static AddMusicBox(mod, musicSlot, itemType, tileType, tileFrameY = 0) {
        if (!(musicSlot > 0) || !(tileType >= FIRST_TILE)) {
            throw new TypeError('MusicLoader.AddMusicBox(mod, slot, item, tile): slot de musica e tile de mod');
        }

        let boxes = TileLoader.MusicBoxes.get(tileType);
        if (!boxes) TileLoader.MusicBoxes.set(tileType, boxes = new Map());
        boxes.set(tileFrameY, { slot: musicSlot, item: itemType });
        if (itemType > 0) TileLoader.MusicBoxItems.set(itemType, musicSlot);

        bl.hookMarks.set('tile.musicbox', tileType);
        bl.hookMarks.set('tile.wire', tileType);
        ModMusic.Install();
    }

    static IsMusicPlaying(slot) {
        const track = ModMusic.Track(slot);
        return !!track && bl.music.state(track.id) === 2;
    }
}

Object.freeze(MusicLoader);
