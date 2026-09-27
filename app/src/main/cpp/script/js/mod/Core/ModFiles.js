// Os arquivos do mod, relativos à pasta do arquivo de entrada:
// Assets/Textures, Assets/Sounds, Assets/Music e Localization.
class ModFiles {
    static AUDIO = ['.ogg', '.wav', '.mp3'];
    static #textureIndex = new Map();

    // ('Textures', 'Items/Espada') -> 'Assets/Textures/Items/Espada'.
    static Asset(kind, path) {
        const p = String(path).replace(/^\/+/, '');
        if (p.startsWith('Assets/')) return p;

        return 'Assets/' + (p.startsWith(kind + '/') ? p : kind + '/' + p);
    }

    static Texture(path) {
        const p = ModFiles.Asset('Textures', path);
        return /\.(png|jpe?g)$/i.test(p) ? p : p + '.png';
    }

    // O caminho dentro de Assets/Textures, sem extensão.
    static TextureName(path) {
        return String(path).replace(/^\/+/, '').replace(/^(Assets\/)?Textures\//, '').replace(/\.png$/i, '');
    }

    // this.Texture escrito pelo mod; senão o espelho do arquivo da classe
    // (Content/Items/X.js -> Items/X); senão o primeiro PNG com o nome dela.
    static ContentTexture(inst, cls) {
        if (inst.Texture && inst.Texture !== cls.name) return ModFiles.TextureName(inst.Texture);

        const file = ContentAutoload.FileOf(cls);
        const mirror = file ? file.replace(/^(Content|Common)\//, '').replace(/[^/]*$/, '') + cls.name : null;
        if (mirror && bl.file.exists(ModFiles.Texture(mirror))) return mirror;

        return ModFiles.FindTexture(cls.name) || mirror || cls.name;
    }

    static FindTexture(name) {
        const mod = bl.mod;
        if (!mod) return null;

        let index = ModFiles.#textureIndex.get(mod.uuid);
        if (!index) {
            index = ModFiles.#IndexTextures();
            ModFiles.#textureIndex.set(mod.uuid, index);
        }
        return index.get(name) || null;
    }

    static #IndexTextures() {
        const index = new Map();
        const root = 'Assets/Textures';

        const walk = (dir) => {
            for (const f of bl.directory.listFiles(dir)) {
                const m = /([^/]+)\.png$/i.exec(f);
                if (m && !index.has(m[1])) index.set(m[1], f.slice(root.length + 1, -4));
            }
            for (const d of bl.directory.listDirectories(dir)) walk(d);
        };

        if (bl.directory.exists(root)) Safe.Run(root, () => walk(root));
        return index;
    }

    // 'Sounds/Tiro' -> o arquivo, com ou sem extensão, na pasta `root` (ou na
    // do mod de quem chama). null se não há.
    static Audio(kind, root, path) {
        const given = String(path);
        const rel = given.startsWith('/') ? given : ModFiles.Asset(kind, given);
        const names = /\.[a-z0-9]{2,4}$/i.test(rel) ? [rel] : ModFiles.AUDIO.map((e) => rel + e);
        const dir = root || (bl.mod && bl.mod.path);

        for (const name of names) {
            const file = name.startsWith('/') || !dir ? name : bl.path.join(dir, name);
            if (bl.file.exists(file)) return file;
        }
        return null;
    }

    static ListTextures(dir) {
        const path = 'Assets/Textures/' + dir;
        try {
            return bl.directory.exists(path) ? bl.directory.listFiles(path) : [];
        } catch (e) {
            return [];
        }
    }
}
