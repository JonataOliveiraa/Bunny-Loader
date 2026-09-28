// As texturas de fundo de mod, como no tModLoader: todo PNG em
// Assets/Textures/Backgrounds ganha um número depois dos do jogo
// (TextureAssets.Background e Main.backgroundWidth/Height crescem até ele).
// A chave é o caminho no mod, sem extensão: 'Assets/Textures/Backgrounds/Nome',
// ou '<mod>/Assets/Textures/Backgrounds/Nome' de fora dele.
//
// O fundo de mod não passa pelo DrawBackgroundItem do celular (que corta o que
// os blocos tapam por uma máscara por textura): SurfaceBackgroundLoader e
// UndergroundBackgroundLoader desenham ou trocam direto. Por isso as máscaras
// (TextureMaskManager.BackgroundMasks) não crescem.
class BackgroundTextureLoader {
    static #byMod = new Map();   // uuid -> Map(caminho -> número)
    static #pending = [];        // { slot, file, rel }
    static #count = 0;
    static #installed = false;

    static get VanillaCount() { return Terraria.Main.maxBackgrounds; }
    static get TotalCount() { return BackgroundTextureLoader.VanillaCount + BackgroundTextureLoader.#count; }

    // Na carga de cada mod, com o mod na pilha.
    static Autoload() {
        const mod = bl.mod;
        if (!mod || BackgroundTextureLoader.#byMod.has(mod.uuid)) return;

        BackgroundTextureLoader.#byMod.set(mod.uuid, new Map());
        for (const file of ModFiles.ListTextures('Backgrounds')) {
            if (file.endsWith('.png')) BackgroundTextureLoader.AddBackgroundTexture(mod, file.slice(0, -4));
        }
    }

    // Um fundo fora de Assets/Textures/Backgrounds; só durante a carga do mod.
    static AddBackgroundTexture(mod, texture) {
        if (!mod || !mod.uuid) throw new TypeError('BackgroundTextureLoader.AddBackgroundTexture(mod, caminho): passe o mod (this.Mod)');
        if (typeof texture !== 'string' || !texture) throw new TypeError('BackgroundTextureLoader.AddBackgroundTexture: o caminho é um texto');
        if (BackgroundTextureLoader.#installed) throw new Error('BackgroundTextureLoader.AddBackgroundTexture: só durante a carga do mod');

        let map = BackgroundTextureLoader.#byMod.get(mod.uuid);
        if (!map) BackgroundTextureLoader.#byMod.set(mod.uuid, map = new Map());
        const rel = BackgroundTextureLoader.#Relative(mod, texture);
        if (map.has(rel)) return map.get(rel);

        const slot = BackgroundTextureLoader.VanillaCount + BackgroundTextureLoader.#count++;
        map.set(rel, slot);
        BackgroundTextureLoader.#pending.push({ slot, rel, file: mod.path + '/' + rel + '.png' });
        if (BackgroundTextureLoader.#pending.length === 1) Ready.Add(() => BackgroundTextureLoader.#Install());
        return slot;
    }

    // GetBackgroundSlot(mod, 'Assets/Textures/Backgrounds/Nome') ou
    // GetBackgroundSlot('examplemod/Assets/Textures/Backgrounds/Nome'). Lança
    // se não existe, como no tModLoader.
    static GetBackgroundSlot(modOrKey, path) {
        const slot = BackgroundTextureLoader.#Find(modOrKey, path);
        if (slot < 0) {
            throw new Error("BackgroundTextureLoader: não achei o fundo '" + (path !== undefined ? path : modOrKey) + "'");
        }
        return slot;
    }

    // TryGetBackgroundSlot(chave, ref) ou (mod, caminho, ref): o número em ref.value.
    static TryGetBackgroundSlot(modOrKey, path, result) {
        if (result === undefined && path && typeof path === 'object') {
            result = path;
            path = undefined;
        }
        const slot = BackgroundTextureLoader.#Find(modOrKey, path);
        if (result && typeof result === 'object') result.value = slot < 0 ? 0 : slot;
        return slot >= 0;
    }

    static #Find(modOrKey, path) {
        let mod = null, rel = null;
        if (path !== undefined && modOrKey && typeof modOrKey === 'object') {
            mod = modOrKey;
            rel = BackgroundTextureLoader.#Relative(mod, path);
        } else if (typeof modOrKey === 'string') {
            const slash = modOrKey.indexOf('/');
            if (slash > 0) {
                mod = BackgroundTextureLoader.#ModByName(modOrKey.slice(0, slash));
                rel = modOrKey.slice(slash + 1);
            }
            if (!mod) {
                mod = bl.mod;
                rel = modOrKey;
            }
        }
        const map = mod && BackgroundTextureLoader.#byMod.get(mod.uuid);
        const slot = map && map.get(rel);
        return slot === undefined ? -1 : slot;
    }

    // O nome do mod como o tModLoader escreve ('ExampleMod'): o id, o uuid
    // ou o nome da classe, sem diferença de maiúsculas.
    static #ModByName(name) {
        const exact = ModRegistry.Find(name, 'BackgroundTextureLoader');
        if (exact) return exact;
        const lower = name.toLowerCase();
        for (const mod of ModRegistry.All()) {
            if (String(mod.id).toLowerCase() === lower || ModRegistry.ClassName(mod).toLowerCase() === lower) return mod;
        }
        return null;
    }

    // Sem a extensão e sem o nome do mod na frente.
    static #Relative(mod, texture) {
        let rel = texture.replace(/\\/g, '/').replace(/\.png$/i, '');
        const slash = rel.indexOf('/');
        if (slash > 0 && !rel.startsWith('Assets/')) {
            const head = rel.slice(0, slash).toLowerCase();
            if (head === String(mod.id).toLowerCase() || head === ModRegistry.ClassName(mod).toLowerCase()) rel = rel.slice(slash + 1);
        }
        return rel;
    }

    // Na thread do jogo, com o conteúdo pronto (a textura só nasce lá).
    static #Install() {
        if (BackgroundTextureLoader.#installed) return;
        BackgroundTextureLoader.#installed = true;

        const pending = BackgroundTextureLoader.#pending;
        const Textures = Terraria.GameContent.TextureAssets;
        const Main = Terraria.Main;
        const total = BackgroundTextureLoader.TotalCount;
        Textures.Background = Textures.Background.cloneResized(total);
        Main.backgroundWidth = Main.backgroundWidth.cloneResized(total);
        Main.backgroundHeight = Main.backgroundHeight.cloneResized(total);

        for (const bg of pending) {
            Safe.Run('fundo ' + bg.rel, () => {
                const asset = bl.loadTextureAsset(bg.file);
                const texture = asset.Value;
                Textures.Background[bg.slot] = asset;
                Main.backgroundWidth[bg.slot] = texture.Width;
                Main.backgroundHeight[bg.slot] = texture.Height;
            });
        }
        bl.log('fundos de mod: ' + pending.length + ' (números ' + BackgroundTextureLoader.VanillaCount + '..' + (total - 1) + ')');
    }
}
