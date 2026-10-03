class ModWall {
    Type = undefined;
    Texture = this.constructor.name;
    DustType = undefined;
    HitSound = undefined;
    ItemDrop = undefined;
    VanillaFallbackOnModDeletion = 0;

    SetStaticDefaults() {}
    PostSetupContent() {}

    AddMapEntry(color, name) {
        const entries = this.mapEntries || (this.mapEntries = []);
        const key = ModWall.#MapEntryKey(this, name ?? this.CreateMapEntryName(), entries.length);
        entries.push({ color, name: key });
        bl.walls.addMapEntry(this.Type, color.R, color.G, color.B, key);
    }

    static #MapEntryKey(wall, name, index) {
        if (name && typeof name === 'object' && typeof name.Key === 'string') return name.Key;

        const text = String(name);
        const own = Lang.Localized('MapObject', text) ?? LocalizationLoader.Texts(text);
        if (own === undefined && text.includes('.') &&
            Terraria.Localization.Language['bool Exists(string key)'](text)) return text;

        const mod = wall.Mod ? (wall.Mod.id || wall.Mod.uuid) : 'BunnyLoader';
        const key = 'Mods.' + mod + '.MapObject.' + wall.constructor.name + (index ? '_' + index : '');
        const fallback = /^[A-Za-z_]\w*$/.test(text) ? text.replace(/([a-z0-9])([A-Z])/g, '$1 $2') : text;
        ModLocalization.Register(key, own ?? fallback);
        return key;
    }

    CreateMapEntryName() { return this.constructor.name; }

    KillWall(i, j, fail) {}
    NumDust(i, j, fail, num) {}
    CreateDust(i, j, type) { return true; }
    KillSound(i, j, fail) { return true; }
    Drop(i, j, type) { return true; }
    ModifyLight(i, j, r, g, b) {}
    AnimateWall(frame, frameCounter) {}
    RandomUpdate(i, j) {}
    WallFrame(i, j, randomizeFrame, style, frameNumber) { return true; }

    static register(cls) {
        if (typeof cls !== 'function' || !(cls.prototype instanceof ModWall)) {
            throw new TypeError('ModWall.register(Classe): passe a classe, que estende ModWall');
        }

        const inst = new cls();
        const name = cls.name;
        Templates.Adopt(cls, inst);
        inst.Texture = ModFiles.ContentTexture(inst, cls);

        const type = bl.walls.register({
            name,
            texture: ModFiles.Texture(inst.Texture),
            setStaticDefaults() {
                Safe.Run(name + '.SetStaticDefaults', () => inst.SetStaticDefaults());
            },
        });
        inst.Type = type;
        WallLoader.ByType.set(type, inst);
        WallLoader.Mark(inst, cls);

        Ready.Add(() => Safe.Run(name + '.PostSetupContent', () => inst.PostSetupContent()));
        Hooks.Once('wall.hooks', WallLoader.Install);
        return type;
    }

    static isModType(type) { return bl.walls.isModWall(type); }
    static getTypeByName(name) { return bl.walls.typeOf(name); }
    static getModWall(type) { return WallLoader.ByType.get(type); }
}
