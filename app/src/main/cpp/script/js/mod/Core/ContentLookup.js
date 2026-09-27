// O tipo e o modelo de um conteúdo de mod (pela classe, pelo nome, ou por
// 'mod/Nome'), e as texturas do mod. Não achou: 0, como no tModLoader.
class ContentLookup {
    static #textures = new Map();   // arquivo -> Asset<Texture2D>

    static Registry(base) {
        if (base === ModItem) return ItemLoader.ByType;
        if (base === ModProjectile) return ProjectileLoader.ByType;
        if (base === ModNPC) return NPCLoader.ByType;
        if (base === ModBuff) return BuffLoader.ByType;
        if (base === ModTile) return TileLoader.ByType;

        throw new TypeError('ModContent: espera ModItem, ModProjectile, ModNPC, ModBuff ou ModTile');
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

    // 'Items/Espada', 'Textures/brilho', 'Assets/Textures/brilho.png' ou um
    // caminho qualquer do mod; 'mod/...' com o id de outro mod na frente.
    static FindTexture(path) {
        const rel = String(path).replace(/^\/+/, '');
        const inRoot = (root, r) => {
            const raw = /\.[a-z0-9]{2,4}$/i.test(r) ? r : r + '.png';
            for (const candidate of [ModFiles.Texture(r), raw]) {
                const full = bl.path.join(root, candidate);
                if (bl.file.exists(full)) return full;
            }
            return null;
        };

        const own = bl.mod && bl.mod.path;
        const mine = own ? inRoot(own, rel) : null;
        if (mine) return mine;

        const slash = rel.indexOf('/');
        if (slash > 0) {
            const mod = ModRegistry.Find(rel.slice(0, slash), 'ModContent');
            if (mod && mod.path) return inRoot(mod.path, rel.slice(slash + 1));
        }
        return null;
    }

    static Request(path) {
        const file = ContentLookup.FindTexture(path);
        if (!file) throw new Error("ModContent.Request: nao achei a textura '" + path + "' em Assets/Textures");

        let asset = ContentLookup.#textures.get(file);
        if (!asset) {
            asset = bl.loadTextureAsset(file);
            ContentLookup.#textures.set(file, asset);
        }
        return asset;
    }
}
