// O tipo e o modelo de um conteúdo de mod (pela classe, pelo nome, ou por
// 'mod/Nome'), e as texturas do mod. Não achou: 0, como no tModLoader.
class ContentLookup {
    static #textures = new Map();   // arquivo -> Asset<Texture2D>
    static #fonts = new Map();      // arquivo -> { Value: SpriteFont }
    static #as = null;              // o mod do As(), no lugar do bl.mod

    static Registry(base) {
        if (base === ModItem) return ItemLoader.ByType;
        if (base === ModProjectile) return ProjectileLoader.ByType;
        if (base === ModNPC) return NPCLoader.ByType;
        if (base === ModBuff) return BuffLoader.ByType;
        if (base === ModDust) return DustLoader.ByType;
        if (base === ModTile) return TileLoader.ByType;
        if (base === ModWall) return WallLoader.ByType;
        if (base === ModPrefix) return PrefixLoader.ByType;
        if (base === ModMount) return MountLoader.ByType;
        if (base === ModHair) return HairLoader.ByType;
        if (base === ModCloud) return CloudLoader.ByType;
        if (base === ModEmoteBubble) return EmoteBubbleLoader.ByType;
        if (base === ModAchievement) return AchievementLoader.ByName;
        if (base === ModRarity) return RarityLoader.ByType;

        throw new TypeError('ModContent: espera ModItem, ModProjectile, ModNPC, ModBuff, ModDust, ModTile, ModWall, ModPrefix, ModMount, ModHair, ModCloud, ModEmoteBubble, ModAchievement ou ModRarity');
    }

    // Pelo nome: no mod de quem chama, ou no único mod que o tem.
    static Find(byType, which) {
        if (typeof which === 'function') {
            const inst = Templates.Get(which);
            return inst && byType.get(inst.Type) === inst ? inst : undefined;
        }

        const name = String(which);
        const slash = name.lastIndexOf('/');
        if (slash > 0) {
            const mod = ModRegistry.Find(name.slice(0, slash), 'ModContent');
            const wanted = name.slice(slash + 1);
            for (const inst of byType.values()) {
                if (mod && inst.Mod === mod && inst.constructor.name === wanted) return inst;
            }
            return undefined;
        }

        const caller = bl.mod;
        let found;
        let count = 0;
        for (const inst of byType.values()) {
            if (inst.constructor.name !== name) continue;
            if (caller && inst.Mod === caller) return inst;

            found = found || inst;
            count++;
        }

        if (count > 1) Safe.Once('ModContent:' + name, "ModContent: '" + name + "' existe em " + count + " mods; peca por 'mod/" + name + "'");
        return count === 1 ? found : undefined;
    }

    static TypeOf(byType, which) {
        const inst = ContentLookup.Find(byType, which);
        return inst ? inst.Type : 0;
    }

    // O caminho no mod ('Content/Items/Espada', 'Assets/Textures/brilho.png'),
    // o de antes dentro de Assets/Textures ('Items/Espada'), ou 'mod/...' com o
    // id de outro mod na frente.
    static FindTexture(path) {
        const rel = String(path).replace(/^\/+/, '');
        const inRoot = (root, r) => {
            const raw = /\.[a-z0-9]{2,4}$/i.test(r) ? r : r + '.png';
            for (const candidate of [ModFiles.Texture(r, root), raw]) {
                const full = bl.path.join(root, candidate);
                if (bl.file.exists(full)) return full;
            }
            return null;
        };

        const current = ContentLookup.#as || bl.mod;
        const own = current && current.path;
        const mine = own ? inRoot(own, rel) : null;
        if (mine) return mine;

        const slash = rel.indexOf('/');
        if (slash > 0) {
            const mod = ModRegistry.Find(rel.slice(0, slash), 'ModContent');
            if (mod && mod.path) return inRoot(mod.path, rel.slice(slash + 1));
        }
        return null;
    }

    // Uma fonte do mod: 'Assets/Fonts/ExampleFont' -> Assets/Fonts/ExampleFont.json.
    static FindFont(path) {
        const rel = String(path).replace(/^\/+/, '').replace(/\.json$/i, '') + '.json';
        const inRoot = (root, r) => {
            const full = bl.path.join(root, r);
            return bl.file.exists(full) ? full : null;
        };

        const current = ContentLookup.#as || bl.mod;
        const mine = current && current.path ? inRoot(current.path, rel) : null;
        if (mine) return mine;

        const slash = rel.indexOf('/');
        if (slash > 0) {
            const mod = ModRegistry.Find(rel.slice(0, slash), 'ModContent');
            if (mod && mod.path) return inRoot(mod.path, rel.slice(slash + 1));
        }
        return null;
    }

    // Roda `fn` como se `mod` estivesse carregando: o caminho sem id de mod do
    // ModContent.Request é dele (o SetStaticDefaults adiado para a thread do jogo).
    static As(mod, fn) {
        const previous = ContentLookup.#as;
        ContentLookup.#as = mod;
        try {
            return fn();
        } finally {
            ContentLookup.#as = previous;
        }
    }

    static Request(path) {
        // As imagens do jogo, como no tModLoader: "Terraria/Images/UI/ButtonPlay".
        if (/^Terraria\//.test(path)) return bl.vanillaTexture(path);
        const file = ContentLookup.FindTexture(path);
        if (file) return ContentLookup.RequestFile(file);

        const font = ContentLookup.FindFont(path);
        if (font) return ContentLookup.RequestFont(path, font);

        throw new Error("ModContent.Request: nao achei a textura '" + path + "' no mod");
    }

    // O Asset<DynamicSpriteFont> do tModLoader: Value e o SpriteFont do celular.
    static RequestFont(name, file) {
        let asset = ContentLookup.#fonts.get(file);
        if (!asset) {
            asset = Object.freeze({ Name: String(name), Value: bl.loadFont(file), IsLoaded: true });
            ContentLookup.#fonts.set(file, asset);
        }
        return asset;
    }

    // Pelo caminho completo do arquivo (sem depender de quem chama).
    static RequestFile(file) {
        let asset = ContentLookup.#textures.get(file);
        if (!asset) {
            asset = bl.loadTextureAsset(file);
            ContentLookup.#textures.set(file, asset);
        }
        return asset;
    }
}
