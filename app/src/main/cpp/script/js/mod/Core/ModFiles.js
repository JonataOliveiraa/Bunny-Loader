// Os arquivos do mod, relativos à pasta do arquivo de entrada.
//
// Texturas como no tModLoader: o caminho no mod, sem extensão. A de um conteúdo
// (item, NPC, projétil...) é, por padrão, o arquivo da classe trocando o .js
// pelo .png (Content/Items/Espada.js -> Content/Items/Espada.png), e as
// derivadas (_Head, _Glow, _Highlight...) ficam do lado. `this.Texture` muda:
// 'Content/Items/Outra', ou com o id do mod na frente ('examplemod/Content/...').
// Assets/ fica para o resto (fundos, sons, música, o que o mod carrega).
//
// Os mods de antes guardavam as texturas em Assets/Textures, pelo nome da
// classe: se o arquivo não está no caminho do tModLoader, vale o de lá (e o log
// avisa uma vez por mod).
class ModFiles {
    static AUDIO = ['.ogg', '.wav', '.mp3'];
    static LEGACY = 'Assets/Textures';
    static #textureIndex = new Map();   // uuid -> Map(nome -> caminho em Assets/Textures)
    static #allTextures = new Map();    // uuid -> [caminho de todo PNG do mod]
    static #warned = new Set();

    // ('Textures', 'Items/Espada') -> 'Assets/Textures/Items/Espada'.
    static Asset(kind, path) {
        const p = ModFiles.Normalize(path);
        if (p.startsWith('Assets/')) return p;

        return 'Assets/' + (p.startsWith(kind + '/') ? p : kind + '/' + p);
    }

    // Sem a barra do começo e sem o id do mod na frente ('examplemod/Content/X').
    static Normalize(path, mod = bl.mod) {
        let p = String(path).replace(/^\/+/, '').replace(/^\.\//, '');
        const slash = p.indexOf('/');
        if (mod && slash > 0) {
            const first = p.slice(0, slash).toLowerCase();
            const names = [mod.id, Safe.Run('mod', () => ModRegistry.ClassName(mod))].filter(Boolean).map((n) => String(n).toLowerCase());
            if (names.includes(first)) {
                p = p.slice(slash + 1);
            }
        }
        return p;
    }

    // O arquivo de uma textura, relativo à pasta do mod: o caminho do
    // tModLoader; senão, nos mods de antes, o mesmo em Assets/Textures, ou o
    // espelho lá (Content/Biomes/X_Icon -> Assets/Textures/Biomes/X_Icon).
    // `where`: o mod (ou a pasta dele), para quem chama fora da carga dele.
    static Texture(path, where) {
        const mod = where && typeof where === 'object' ? where : bl.mod;
        const root = typeof where === 'string' ? where : where && where.path;
        const p = ModFiles.Normalize(path, mod);
        const file = /\.(png|jpe?g)$/i.test(p) ? p : p + '.png';
        if (file.startsWith('Assets/')) return file;

        const exists = (rel) => (root ? bl.file.exists(bl.path.join(root, rel)) : bl.file.exists(rel));
        if (exists(file)) return file;

        const legacy = [ModFiles.LEGACY + '/' + file, ModFiles.LEGACY + '/' + file.replace(/^(Content|Common)\//, '')];
        for (const candidate of legacy) {
            if (!exists(candidate)) continue;
            ModFiles.#WarnLegacy(file, candidate, mod);
            return candidate;
        }
        return file;
    }

    // O caminho da textura, sem extensão (como o `Texture` do tModLoader).
    static TextureName(path) {
        return ModFiles.Normalize(path).replace(/\.(png|jpe?g)$/i, '');
    }

    // this.Texture escrito pelo mod; senão o do tModLoader (o arquivo da classe
    // sem o .js); senão, nos mods de antes, o espelho em Assets/Textures
    // (Content/Items/X.js -> Assets/Textures/Items/X) ou o primeiro PNG com o
    // nome da classe lá.
    static ContentTexture(inst, cls) {
        const declared = ModFiles.#DeclaredTexture(inst, cls);
        if (declared) return ModFiles.TextureName(declared);
        if (inst.Texture && inst.Texture !== cls.name) return ModFiles.TextureName(inst.Texture);

        const file = ContentAutoload.FileOf(cls);
        const own = (file ? file.replace(/[^/]*$/, '') : '') + cls.name;
        if (bl.file.exists(own + '.png')) return own;

        const mirror = file ? file.replace(/^(Content|Common)\//, '').replace(/[^/]*$/, '') + cls.name : null;
        let legacy = mirror && bl.file.exists(ModFiles.LEGACY + '/' + mirror + '.png') ? mirror : ModFiles.FindTexture(cls.name);
        if (legacy) {
            legacy = ModFiles.LEGACY + '/' + legacy;
            ModFiles.#WarnLegacy(own + '.png', legacy + '.png', bl.mod);
            return legacy;
        }
        return own;
    }

    // `get Texture() { return '...'; }` na classe, como o `override string
    // Texture =>` do tModLoader: o campo Texture da base fica na instância e
    // esconderia o getter, então ele é lido aqui, pelo protótipo.
    static #DeclaredTexture(inst, cls) {
        for (let p = cls.prototype; p && p !== Object.prototype; p = Object.getPrototypeOf(p)) {
            const d = Object.getOwnPropertyDescriptor(p, 'Texture');
            if (d && d.get) return Safe.Run(cls.name + '.Texture', () => d.get.call(inst));
        }
        return undefined;
    }

    // Os mods de antes: o caminho em Assets/Textures do primeiro PNG com o nome.
    static FindTexture(name) {
        const mod = bl.mod;
        if (!mod) return null;

        let index = ModFiles.#textureIndex.get(mod.uuid);
        if (!index) {
            index = new Map();
            const root = ModFiles.LEGACY + '/';
            for (const f of ModFiles.#AllTextures()) {
                if (!f.startsWith(root)) continue;
                const m = /([^/]+)\.png$/i.exec(f);
                if (m && !index.has(m[1])) index.set(m[1], f.slice(root.length, -4));
            }
            ModFiles.#textureIndex.set(mod.uuid, index);
        }
        return index.get(name) || null;
    }

    // Todo PNG numa pasta `kind` em qualquer lugar do mod (Gores/, Clouds/,
    // Backgrounds/), como os autoloads do tModLoader; Assets/Textures/<kind> entra junto.
    static ListTextures(kind) {
        const re = new RegExp('(^|/)' + kind + '/');
        return ModFiles.#AllTextures().filter((f) => re.test(f));
    }

    // Os PNG do mod, uma varredura por mod.
    static #AllTextures() {
        const mod = bl.mod;
        if (!mod) return [];

        let list = ModFiles.#allTextures.get(mod.uuid);
        if (list) return list;

        list = [];
        const walk = (dir) => {
            for (const f of bl.directory.listFiles(dir)) {
                if (/\.png$/i.test(f)) list.push(f.replace(/^\.\//, ''));
            }
            for (const d of bl.directory.listDirectories(dir)) walk(d);
        };
        Safe.Run('texturas do mod', () => walk('.'));
        ModFiles.#allTextures.set(mod.uuid, list);
        return list;
    }

    static #WarnLegacy(wanted, found, mod = bl.mod) {
        const key = mod ? mod.uuid : '';
        if (ModFiles.#warned.has(key)) return;

        ModFiles.#warned.add(key);
        bl.log((mod ? mod.name || mod.id : 'mod') + ': textura no lugar antigo (' + found + '); o caminho do tModLoader é ' +
               wanted + ', ao lado do .js. Os dois funcionam.');
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
}
