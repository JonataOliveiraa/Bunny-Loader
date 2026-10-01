// Como o GoreLoader do tModLoader: todo PNG numa pasta Gores/ do mod vira um
// tipo novo, com o nome do arquivo.
class GoreLoader {
    static #byMod = new Map();     // uuid -> Map(nome -> tipo)
    static #pending = [];          // { mod, name, file }
    static #installed = false;

    // No registro de conteúdo, com o mod na pilha.
    static Autoload() {
        const mod = bl.mod && bl.mod.uuid;
        if (!mod || GoreLoader.#byMod.has(mod)) return;

        GoreLoader.#byMod.set(mod, new Map());
        for (const file of ModFiles.ListTextures('Gores')) {
            if (!file.endsWith('.png')) continue;

            const name = file.slice(file.lastIndexOf('/') + 1, -4);
            GoreLoader.#pending.push({ mod, name, file: bl.mod.path + '/' + file });
        }

        if (GoreLoader.#pending.length) Ready.Add(GoreLoader.#Install);
    }

    static TypeOf(mod, name) {
        const map = GoreLoader.#byMod.get(mod);
        return (map && map.get(name)) || 0;
    }

    static TypeOfAny(name) {
        for (const map of GoreLoader.#byMod.values()) {
            const type = map.get(name);
            if (type) return type;
        }
        return 0;
    }

    // TextureAssets.Gore, GoreID.Sets e ChildSafety.SafeGore crescem.
    static #Install() {
        const pending = GoreLoader.#pending;
        if (GoreLoader.#installed || pending.length === 0) return;

        GoreLoader.#installed = true;
        const Textures = Terraria.GameContent.TextureAssets;
        const Sets = Terraria.ID.GoreID.Sets;
        const Safety = Terraria.GameContent.ChildSafety;

        const first = Textures.Gore.length;
        const total = first + pending.length;
        Textures.Gore = Textures.Gore.cloneResized(total);
        Sets.SpecialAI = Sets.SpecialAI.cloneResized(total);
        Sets.DisappearSpeed = Sets.DisappearSpeed.cloneResized(total);
        Sets.DisappearSpeedAlpha = Sets.DisappearSpeedAlpha.cloneResized(total);
        Sets.IsDrip = Sets.IsDrip.cloneResized(total);
        Safety.SafeGore = Safety.SafeGore.cloneResized(total);

        pending.forEach((gore, i) => {
            const type = first + i;
            Safe.Run('gore ' + gore.name, () => { Textures.Gore[type] = bl.loadTextureAsset(gore.file); });
            Sets.DisappearSpeed[type] = 1;
            Sets.DisappearSpeedAlpha[type] = 1;
            GoreLoader.#byMod.get(gore.mod).set(gore.name, type);
        });

        bl.log('gores de mod: ' + pending.length + ' (tipos ' + first + '..' + (total - 1) + ')');
    }
}
