// Uma instância por tipo. Móvel (várias células): TileObjectData no SetStaticDefaults.
// Os parâmetros `ref` do tModLoader chegam como Ref (.value).
class ModTile {
    Type = undefined;
    Texture = this.constructor.name;
    DustType = 0;
    HitSound = undefined;   // um SoundID (número ou estilo); undefined = o do jogo
    MinPick = 0;
    MineResist = 1;
    ItemDrop = undefined;   // undefined = o item de mod que coloca este tile
    AdjTiles = [];          // conta como estas estações de criação (TileID.WorkBenches...)
    AnimationFrameHeight = 0;
    // false: SetDrawPositions/AnimateIndividualTile/SetSpriteEffects rodam em todo
    // desenho (para quem anima por outro contador que não o Main.tileFrame do tipo).
    CacheDrawData = true;
    VanillaFallbackOnModDeletion = 0;

    get HighlightTexture() { return this.Texture + '_Highlight'; }

    SetStaticDefaults() {}
    PostSetDefaults() {}
    PostSetupContent() {}

    // A cor e o nome no mapa; cada chamada é uma opção a mais do tipo. Sem
    // AddMapEntry, o tile fica fora do mapa. O nome: um LocalizedText, uma
    // chave de texto (do mod ou do jogo) ou o próprio texto; um nome de classe
    // procura MapObject.<Nome> na localização do mod, senão vira "Example Tile".
    AddMapEntry(color, name) {
        const entries = this.mapEntries || (this.mapEntries = []);
        const key = ModTile.#MapEntryKey(this, name ?? this.CreateMapEntryName(), entries.length);
        entries.push({ color, name: key });
        bl.tiles.addMapEntry(this.Type, color.R, color.G, color.B, key);
    }

    // A chave do texto no dicionário do jogo: o mapa guarda o LocalizedText
    // dela, e a troca de idioma troca o texto.
    static #MapEntryKey(tile, name, index) {
        if (name && typeof name === 'object' && typeof name.Key === 'string') return name.Key;

        const text = String(name);
        const own = Lang.Localized('MapObject', text) ?? LocalizationLoader.Texts(text);
        if (own === undefined && text.includes('.') &&
            Terraria.Localization.Language['bool Exists(string key)'](text)) return text;

        const mod = tile.Mod ? (tile.Mod.id || tile.Mod.uuid) : 'BunnyLoader';
        const key = 'Mods.' + mod + '.MapObject.' + tile.constructor.name + (index ? '_' + index : '');
        const fallback = /^[A-Za-z_]\w*$/.test(text) ? text.replace(/([a-z0-9])([A-Z])/g, '$1 $2') : text;
        ModLocalization.Register(key, own ?? fallback);
        return key;
    }

    CreateMapEntryName() { return this.constructor.name; }

    // O item que sai ao quebrar: de todos os estilos, ou só dos `styles`.
    RegisterItemDrop(itemType, ...styles) {
        const drops = this.itemDrops || (this.itemDrops = new Map());
        if (!styles.length) drops.set(-1, itemType);
        for (const style of styles) drops.set(style, itemType);
    }

    GetMapOption(i, j) { return 0; }

    CanKillTile(i, j, blockDamaged) { return true; }
    KillTile(i, j, fail, effectOnly, noItem) {}
    // Um objeto saiu inteiro: (i, j) é o canto de cima à esquerda; frameX/frameY, o quadro dele.
    KillMultiTile(i, j, frameX, frameY) {}
    NumDust(i, j, fail, num) {}
    CreateDust(i, j, type) { return true; }
    KillSound(i, j, fail) { return true; }
    CanDrop(i, j) { return true; }
    // Os itens que saem: tipos, ou { type, stack }. undefined = o de sempre.
    GetItemDrops(i, j) { return undefined; }
    PlaceInWorld(i, j, item) {}

    ModifyLight(i, j, r, g, b) {}
    AnimateTile(frame, frameCounter) {}
    AnimateIndividualTile(type, i, j, frameXOffset, frameYOffset) {}
    SetDrawPositions(i, j, width, offsetY, height, tileFrameX, tileFrameY) {}
    SetSpriteEffects(i, j, spriteEffects) {}
    // Com PreDraw sobrescrito, o jogo não desenha o tile: false = nem o desenho padrão.
    PreDraw(i, j, spriteBatch) { return true; }
    PostDraw(i, j, spriteBatch) {}
    DrawEffects(i, j, spriteBatch, drawData) {}
    SpecialDraw(i, j, spriteBatch) {}
    EmitParticles(i, j, tile, tileFrameX, tileFrameY, tileLight, visible) {}
    NearbyEffects(i, j, closer) {}

    RightClick(i, j) { return false; }
    MouseOver(i, j) {}
    MouseOverFar(i, j) {}
    // Se o tile é alvo do smart-interact. No celular o toque num móvel passa
    // por ele (é o clique direito do PC): sem sobrescrever, vale para quem tem
    // RightClick.
    HasSmartInteract(i, j, settings) { return Hooks.Overrides(this.constructor, ModTile, 'RightClick'); }
    HitWire(i, j) {}
    Slope(i, j) { return true; }
    RandomUpdate(i, j) {}
    TileFrame(i, j, resetFrame, noBreak) { return true; }

    ModifySittingTargetInfo(i, j, info) {}
    ModifySleepingTargetInfo(i, j, info) {}

    IsLockedChest(i, j) { return false; }
    UnlockChest(i, j, frameXAdjustment, dustType, manual) { return false; }
    LockChest(i, j, frameXAdjustment, manual) { return false; }
    DefaultContainerName(frameX, frameY) { return this.CreateMapEntryName(); }

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
                TileLoader.AfterStaticDefaults(inst);
            },
        });
        inst.Type = type;
        TileLoader.ByType.set(type, inst);
        TileLoader.Mark(inst, cls);

        Ready.Add(() => Safe.Run(name + '.PostSetupContent', () => inst.PostSetupContent()));
        Hooks.Once('tile.hooks', TileLoader.Install);
        return type;
    }

    static isModType(type) { return bl.tiles.isModTile(type); }
    static getTypeByName(name) { return bl.tiles.typeOf(name); }
    static getModTile(type) { return TileLoader.ByType.get(type); }
}
