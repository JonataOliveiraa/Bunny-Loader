// A carga de um mod, como no tModLoader: o arquivo de entrada exporta a classe
// Mod, e toda classe de conteúdo exportada por ele ou por um arquivo de
// Content/ e Common/ é registrada sozinha. `static Autoload = false` deixa uma
// classe de fora. O núcleo (ScriptEngine.cpp) importa os arquivos e chama o Load.
class ContentAutoload {
    // Fundos, água e biomas antes do NPC: o SetStaticDefaults dele roda no
    // registro, e o Happiness.SetBiomeAffection(ModBiome) do morador pede o
    // bioma já registrado. A montaria antes do buff e do item, que pedem o
    // MountType (o SetStaticDefaults dela roda depois, com tudo carregado).
    static #ORDER = [ModMount, ModBuff, ModPrefix, ModPlayer,
                     ModSurfaceBackgroundStyle, ModUndergroundBackgroundStyle, ModWaterfallStyle, ModWaterStyle,
                     ModBiome, ModSceneEffect, ModMenu,
                     ModNPC, ModProjectile, ModItem, ModTile, ModSystem,
                     GlobalItem, GlobalNPC, GlobalProjectile, GlobalLoot,
                     ModCommand, ModHair, ModCloud, ModEmoteBubble, ModAchievement];
    static #fileOf = new Map();

    static FileOf(cls) {
        return ContentAutoload.#fileOf.get(cls);
    }

    static Load(files) {
        const mod = bl.mod;
        if (!mod) throw new Error('carregador: nenhum mod carregando');

        const [entryPath, entry] = files[0];
        const main = entry.default;
        if (typeof main !== 'function') {
            throw new TypeError(entryPath + " precisa de 'export default class <Nome> extends Mod'");
        }
        if (!(main.prototype instanceof Mod)) {
            throw new TypeError('o export default de ' + entryPath + ' (' + (main.name || '?') + ') precisa estender Mod');
        }

        LocalizationLoader.Load(mod);
        BackgroundTextureLoader.Autoload();

        const buckets = ContentAutoload.#Collect(files, main);
        ContentAutoload.#ORDER.forEach((base, i) => {
            for (const cls of buckets[i]) ContentAutoload.#Register(base, cls);
        });

        ModRegistry.Adopt(mod, main);
        // Depois do Mod.Load, que pode ter chamado o AddCloudFromTexture.
        CloudLoader.Autoload();
    }

    // As classes exportadas, por tipo de conteúdo, na ordem de #ORDER.
    static #Collect(files, main) {
        const buckets = ContentAutoload.#ORDER.map(() => []);
        const seen = new Set([main]);

        for (const [path, exports] of files) {
            for (const key of Object.keys(exports)) {
                const cls = exports[key];
                if (typeof cls !== 'function' || seen.has(cls)) continue;

                seen.add(cls);
                if (Object.prototype.hasOwnProperty.call(cls, 'Autoload') && cls.Autoload === false) continue;

                const slot = ContentAutoload.#ORDER.findIndex((base) => cls.prototype instanceof base);
                if (slot < 0) continue;

                buckets[slot].push(cls);
                ContentAutoload.#fileOf.set(cls, path);
            }
        }
        return buckets;
    }

    static #Register(base, cls) {
        try {
            base.register(cls);
        } catch (e) {
            if (e instanceof Error) e.message = cls.name + ' (' + ContentAutoload.FileOf(cls) + '): ' + e.message;
            throw e;
        }
    }
}

bl.__setModLoader((files) => ContentAutoload.Load(files));
delete bl.__setModLoader;
