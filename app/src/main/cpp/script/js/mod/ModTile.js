// Uma instância por tipo. Móvel (várias células): TileObjectData no SetStaticDefaults.
class ModTile {
    Type = undefined;
    Texture = this.constructor.name;
    DustType = 0;
    HitSound = undefined;   // um SoundID (número ou estilo); undefined = o do jogo
    MinPick = 0;
    MineResist = 1;
    ItemDrop = undefined;   // undefined = o item de mod que coloca este tile

    SetStaticDefaults() {}
    PostSetupContent() {}

    // A cor no mapa. Sem AddMapEntry, o tile fica fora do mapa.
    AddMapEntry(color, name) {
        (this.mapEntries || (this.mapEntries = [])).push({ color, name });
        if (this.mapEntries.length === 1) bl.tiles.setMapColor(this.Type, color.R, color.G, color.B);
    }

    CanKillTile(i, j) { return true; }
    KillTile(i, j, fail, effectOnly, noItem) {}
    // Um objeto saiu inteiro: (i, j) é o canto de cima à esquerda; frameX/frameY, o quadro dele.
    KillMultiTile(i, j, frameX, frameY) {}
    CreateDust(i, j) { return true; }
    KillSound(i, j, fail) { return true; }

    static register(cls) {
        if (typeof cls !== 'function' || !(cls.prototype instanceof ModTile)) {
            throw new TypeError('ModTile.register(Classe): passe a classe, que estende ModTile');
        }

        const inst = new cls();
        const name = cls.name;
        Templates.Adopt(cls, inst);
        inst.Texture = ModFiles.ContentTexture(inst, cls);

        const type = bl.tiles.register({
            name,
            texture: ModFiles.Texture(inst.Texture),
            setStaticDefaults() {
                inst.SetStaticDefaults();
            },
        });
        inst.Type = type;
        TileLoader.ByType.set(type, inst);

        Ready.Add(() => Safe.Run(name + '.PostSetupContent', () => inst.PostSetupContent()));
        Hooks.Once('tile.hooks', TileLoader.Install);
        return type;
    }

    static isModType(type) { return bl.tiles.isModTile(type); }
    static getTypeByName(name) { return bl.tiles.typeOf(name); }
    static getModTile(type) { return TileLoader.ByType.get(type); }
}
