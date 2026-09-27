// Os métodos recebem a posição (i, j) em tiles. O filtro nativo pelo tipo do
// tile deixa os blocos do jogo fora do JS; as marcas (bl.hookMarks), os tipos
// que não sobrescrevem aquele método.
class TileLoader {
    static ByType = new Map();
    static Animated = [];         // os que sobrescrevem o AnimateTile
    static MusicBoxes = new Map();   // tipo -> Map(frameY do estilo -> { slot, item })
    static MusicBoxItems = new Map();   // item da caixa de música -> slot (equipado toca)

    static #itemsByTile = null;
    static #shapes = new Map();   // tipo -> 0 sem TileObjectData, 1 de uma célula, 2 de várias
    static #dustLeft = -1;        // o NumDust do KillTile em andamento; -1 = sem limite

    // Cada método que custa por quadro/célula, e a marca que o liga.
    static MARKS = [
        ['tile.light', ['ModifyLight']],
        ['tile.drawdata', ['SetDrawPositions', 'SetSpriteEffects', 'AnimateIndividualTile', 'PreDraw']],
        ['tile.draw', ['PreDraw', 'PostDraw', 'DrawEffects', 'SpecialDraw', 'EmitParticles']],
        ['tile.wire', ['HitWire']],
        ['tile.random', ['RandomUpdate']],
        ['tile.place', ['PlaceInWorld']],
        ['tile.nearby', ['NearbyEffects']],
        ['tile.frame', ['TileFrame']],
        ['tile.slope', ['Slope']],
        ['tile.chest', ['IsLockedChest', 'UnlockChest', 'LockChest']],
    ];

    static At(i, j) {
        return TileLoader.ByType.get(bl.tiles.typeAt(i, j));
    }

    static Tile(i, j) {
        return Terraria.Main.tile['Tile get_Item(int x, int y)'](i, j);
    }

    static Overrides(m, name) {
        return Hooks.Overrides(m.constructor, ModTile, name);
    }

    static ObjectData(type) {
        return Terraria.ObjectData.TileObjectData['TileObjectData GetTileData(int type, int style, int alternate)'](type, 0, 0);
    }

    static HasObjectData(type) { return TileLoader.#Shape(type) !== 0; }
    static IsMultiTile(type) { return TileLoader.#Shape(type) === 2; }

    static Mark(m, cls) {
        const over = (name) => Hooks.Overrides(cls, ModTile, name);
        for (const [marks, methods] of TileLoader.MARKS) {
            if (methods.some(over)) bl.hookMarks.set(marks, m.Type);
        }

        // Os de todo quadro só entram quando algum tile de mod os usa.
        const lazy = (key, install) => Hooks.Once(key, () => Safe.Run('ganchos de tile (' + key + ')', install));
        if (over('AnimateTile')) {
            TileLoader.Animated.push(m);
            lazy('tile.animate', TileDrawLoader.HookAnimation);
        }
        if (bl.hookMarks.has('tile.draw', m.Type)) lazy('tile.drawpass', TileDrawLoader.HookDrawPass);
        if (over('ModifyLight')) lazy('tile.light', TileDrawLoader.HookLight);
        if (over('NearbyEffects')) lazy('tile.nearby', TileWorldLoader.HookNearby);
        if (over('PlaceInWorld')) lazy('tile.place', TileWorldLoader.HookPlace);
    }

    // Depois do SetStaticDefaults do mod: o que o SetupContent do tModLoader faz.
    static AfterStaticDefaults(m) {
        const Main = Terraria.Main;
        const Sets = Terraria.ID.TileID.Sets;
        const type = m.Type;

        if (Main.tileLavaDeath[type]) Main.tileObsidianKill[type] = true;
        if (Main.tileSolid[type]) Main.tileNoSunLight[type] = true;
        Safe.Run(m.constructor.name + '.PostSetDefaults', () => m.PostSetDefaults());

        if (m.AnimationFrameHeight > 0) bl.tiles.setAnimationFrameHeight(type, m.AnimationFrameHeight);
        if (m.CacheDrawData === false) bl.hookMarks.set('tile.drawdata.uncached', type);
        if (m.AdjTiles && m.AdjTiles.length) {
            bl.hookMarks.set('tile.adj', type);
            Hooks.Once('tile.adj', () => Safe.Run('ganchos de tile (tile.adj)', TileWorldLoader.HookAdjTiles));
        }
        if (Sets.HasOutlines[type]) TileLoader.#Highlight(m);
        TileLoader.#RoomNeeds(type);
        TileLoader.#Door(type);
    }

    // Porta de mod: o par vai ao nativo (o jogador que encosta e os NPCs só
    // conhecem as portas 10 e 11) e o fio abre e fecha, como a do jogo.
    static #Door(type) {
        const Sets = Terraria.ID.TileID.Sets;
        const open = Sets.OpenDoorID[type], closed = Sets.CloseDoorID[type];
        if (open >= 0) bl.tiles.setDoor(type, open);
        else if (closed >= 0) bl.tiles.setDoor(closed, type);
        else return;
        bl.hookMarks.set('tile.wire', type);
    }

    // A casa do jogo percorre as listas CountsAs*Types (não os conjuntos
    // CountsAs*[tipo]): o tipo de mod marcado no conjunto entra na lista.
    static #RoomNeeds(type) {
        const RoomNeeds = Terraria.ID.TileID.Sets.RoomNeeds;
        for (const need of ['Chair', 'Table', 'Torch', 'Door']) {
            if (!RoomNeeds['CountsAs' + need][type]) continue;

            const key = 'CountsAs' + need + 'Types';
            const list = RoomNeeds[key];
            let listed = false;
            for (let k = 0; k < list.length; k++) listed = listed || list[k] === type;
            if (listed) continue;

            const grown = list.cloneResized(list.length + 1);
            grown[list.length] = type;
            RoomNeeds[key] = grown;
        }
    }

    // O item que sai do tile (tML: GetItemDropFromTypeAndStyle).
    static GetItemDropFromTypeAndStyle(type, style = 0) {
        const m = TileLoader.ByType.get(type);
        if (!m) return 0;

        const drops = m.itemDrops;
        if (drops && drops.has(style)) return drops.get(style);
        if (drops && drops.has(-1)) return drops.get(-1);
        if (m.ItemDrop !== undefined) return m.ItemDrop;

        const items = TileLoader.#ItemsByTile();
        return items.get(type + ':' + style) || items.get(type) || 0;
    }

    static StyleAt(i, j) {
        return Math.max(0, Terraria.ObjectData.TileObjectData.GetTileStyle(TileLoader.Tile(i, j)));
    }

    // O CheckModTile do tModLoader: chamado quando o jogo enquadra uma célula de
    // um objeto de mod. Faltou uma célula ou o apoio: sai o objeto inteiro, com um drop só.
    static CheckObject(i, j, type) {
        const W = Terraria.WorldGen;
        if (W.destroyObject) return;

        let data = TileLoader.ObjectData(type);
        if (!data) return;

        const tile = TileLoader.Tile(i, j);
        const frameX = tile.frameX, frameY = tile.frameY;
        const style = TileLoader.#StyleOf(data, frameX, frameY);

        data = Terraria.ObjectData.TileObjectData['TileObjectData GetTileData(Tile getTile)'](tile);
        if (!data) return;

        const pad = data.CoordinatePadding;
        const partFrameX = frameX % data.CoordinateFullWidth;
        const partFrameY = frameY % data.CoordinateFullHeight;
        const partX = Math.floor(partFrameX / (data.CoordinateWidth + pad));
        const heights = data.CoordinateHeights;

        let partY = 0;
        for (let rest = partFrameY; partY + 1 < data.Height && rest - heights[partY] - pad >= 0; partY++) {
            rest -= heights[partY] + pad;
        }

        const left = i - partX, top = j - partY;
        const width = data.Width, height = data.Height;
        const whole = TileLoader.#IsWhole(left, top, width, height, type);
        if (whole && TileLoader.#Stays(left + data.Origin.X, top + data.Origin.Y, type, style)) return;

        W.destroyObject = true;
        try {
            // Primeiro o drop (com a célula ainda no mundo), depois cada célula.
            W['void KillTile_DropItems(int x, int y, Tile tileCache, bool includeLargeObjectDrops)'](i, j, tile, true);

            const kill = W['void KillTile(int i, int j, bool fail, bool effectOnly, bool noItem)'];
            for (let x = left; x < left + width; x++) {
                for (let y = top; y < top + height; y++) {
                    if (bl.tiles.typeAt(x, y) === type) kill(x, y, false, false, false);
                }
            }

            const m = TileLoader.ByType.get(type);
            if (m) Safe.Run(m.constructor.name + '.KillMultiTile', () => m.KillMultiTile(left, top, frameX - partFrameX, frameY - partFrameY));
        } finally {
            W.destroyObject = false;
        }

        const frame = W['void TileFrame(int i, int j, bool resetFrame, bool noBreak)'];
        for (let x = left - 1; x < left + width + 2; x++) {
            for (let y = top - 1; y < top + height + 2; y++) frame(x, y, false, false);
        }
    }

    static Install() {
        const parts = [
            ['mineração', TileLoader.#HookMining],
            ['enquadramento', TileLoader.#HookFraming],
            ['quebra', TileLoader.#HookKilling],
            ['desenho', TileDrawLoader.Install],
            ['uso', TileUseLoader.Install],
            ['mundo', TileWorldLoader.Install],
        ];
        for (const [name, install] of parts) Safe.Run('ganchos de tile (' + name + ')', install);
    }

    static #HookMining() {
        Terraria.Player['int GetPickaxeDamage(int x, int y, int pickPower, int hitBufferIndex, Tile tileTarget)'].hook(
            (original, self, x, y, pickPower, hit, tile) => {
                const damage = original(self, x, y, pickPower, hit, tile);

                const m = TileLoader.At(x, y);
                if (!m) return damage;
                if (pickPower < m.MinPick) return 0;

                return m.MineResist > 0 ? Math.floor(damage / m.MineResist) : damage;
            }, { minType: FIRST_TILE, tile: 4 });
    }

    static #HookFraming() {
        const W = Terraria.WorldGen;

        // O jogo enquadra os objetos dele um a um pelo tipo (Check2x2...); o de
        // mod com TileObjectData passa pelo CheckObject. Tocha e plataforma o
        // jogo enquadra pelo conjunto, a de mod também (CheckTorch, as rampas).
        const Sets = Terraria.ID.TileID.Sets;
        W['void TileFrameImportant(int i, int j, int type, Tile tileCache, bool resetFrame)'].hook(
            (original, i, j, type, tile, resetFrame) => {
                const vanilla = !TileLoader.ByType.has(type) || !TileLoader.HasObjectData(type) || Sets.Torches[type] || Sets.Platforms[type];
                if (vanilla) return original(i, j, type, tile, resetFrame);

                Safe.Run('TileFrame de objeto de mod', () => TileLoader.CheckObject(i, j, type));
                return undefined;
            }, { minType: FIRST_TILE, tile: 3 });

        W['void TileFrame(int i, int j, bool resetFrame, bool noBreak)'].hook((original, i, j, resetFrame, noBreak) => {
            const m = TileLoader.At(i, j);
            if (!m) return original(i, j, resetFrame, noBreak);

            const reset = new Ref(resetFrame), keep = new Ref(noBreak);
            if (Safe.Run(m.constructor.name + '.TileFrame', () => m.TileFrame(i, j, reset, keep)) === false) return undefined;

            return original(i, j, reset.value, keep.value);
        }, { minType: FIRST_TILE, tileAt: [0, 1], marks: 'tile.frame' });
    }

    static #HookKilling() {
        const W = Terraria.WorldGen;
        const at = TileLoader.At;

        W['bool CanKillTile(int i, int j, out bool blockDamaged)'].hook((original, i, j, blockDamaged) => {
            const m = at(i, j);
            if (m && Safe.Run(m.constructor.name + '.CanKillTile', () => m.CanKillTile(i, j, blockDamaged)) === false) {
                blockDamaged.value = false;
                return false;
            }
            return original(i, j, blockDamaged);
        }, { minType: FIRST_TILE, tileAt: [0, 1] });

        W['void KillTile(int i, int j, bool fail, bool effectOnly, bool noItem)'].hook(
            (original, i, j, fail, effectOnly, noItem) => {
                const m = at(i, j);
                if (!m) return original(i, j, fail, effectOnly, noItem);

                const f = new Ref(fail), e = new Ref(effectOnly), n = new Ref(noItem);
                const name = m.constructor.name;
                Safe.Run(name + '.KillTile', () => m.KillTile(i, j, f, e, n));

                const num = new Ref(f.value ? 3 : 10);
                Safe.Run(name + '.NumDust', () => m.NumDust(i, j, f.value, num));
                TileLoader.#dustLeft = Math.max(0, num.value | 0);
                try {
                    return original(i, j, f.value, e.value, n.value);
                } finally {
                    TileLoader.#dustLeft = -1;
                }
            }, { minType: FIRST_TILE, tileAt: [0, 1] });

        W['void KillTile_GetItemDrops(int x, int y, Tile tileCache, out int dropItem, out int dropItemStack, out int secondaryItem, out int secondaryItemStack, out bool noPrefix, bool includeLargeObjectDrops)'].hook(
            (original, x, y, tile, drop, stack, second, secondStack, noPrefix, large) => {
                original(x, y, tile, drop, stack, second, secondStack, noPrefix, large);

                const m = at(x, y);
                if (!m) return;
                TileLoader.#Drops(m, x, y, large, drop, stack, second, secondStack);
            }, { minType: FIRST_TILE, tile: 2 });

        const newDust = Terraria.Dust['int NewDust(Vector2 Position, int Width, int Height, int Type, float SpeedX, float SpeedY, int Alpha, Color newColor, float Scale)'];
        W['int KillTile_MakeTileDust(int i, int j, Tile tileCache)'].hook((original, i, j, tile) => {
            const m = at(i, j);
            if (!m) return original(i, j, tile);

            if (TileLoader.#dustLeft === 0) return 6000;
            if (TileLoader.#dustLeft > 0) TileLoader.#dustLeft--;

            const type = new Ref(m.DustType);
            if (Safe.Run(m.constructor.name + '.CreateDust', () => m.CreateDust(i, j, type)) === false) return 6000;
            if (type.value < 0) return 6000;

            return newDust(Vector2.new(i * 16, j * 16), 16, 16, type.value, 0, 0, 0, Color.White, 1);
        }, { minType: FIRST_TILE, tile: 2 });

        const Engine = Terraria.Audio.SoundEngine;
        const playInt = Engine['SoundEffectInstance PlaySound(int type, int x, int y, int Style, float volumeScale, float pitchOffset)'];
        const playStyle = Engine['SoundEffectInstance PlaySound(LegacySoundStyle type, Vector2 position, float pitchOffset, float volumeScale)'];
        W['void KillTile_PlaySounds(int i, int j, bool fail, Tile tileCache)'].hook((original, i, j, fail, tile) => {
            const m = at(i, j);
            if (!m) return original(i, j, fail, tile);
            if (Safe.Run(m.constructor.name + '.KillSound', () => m.KillSound(i, j, fail)) === false) return undefined;

            const sound = m.HitSound;
            if (sound === undefined || sound === null) return original(i, j, fail, tile);

            if (typeof sound === 'number') playInt(sound, i * 16, j * 16, 1, 1, 0);
            else playStyle(sound, Vector2.new(i * 16, j * 16), 0, 1);
            return undefined;
        }, { minType: FIRST_TILE, tile: 3 });
    }

    // O drop de uma célula. Objeto de várias células: o drop sai uma vez, no
    // CheckObject (large). O GetItemDrops dá até dois itens (os dois do jogo).
    static #Drops(m, x, y, large, drop, stack, second, secondStack) {
        const name = m.constructor.name;
        const none = () => {
            drop.value = 0;
            second.value = 0;
        };

        if (!large && TileLoader.IsMultiTile(m.Type)) return none();
        if (Safe.Run(name + '.CanDrop', () => m.CanDrop(x, y)) === false) return none();

        if (TileLoader.Overrides(m, 'GetItemDrops')) {
            const list = Safe.Run(name + '.GetItemDrops', () => m.GetItemDrops(x, y));
            if (list !== undefined && list !== null) {
                const items = [...list].map((it) => typeof it === 'number' ? { type: it, stack: 1 } : { type: it.type, stack: it.stack || 1 });
                none();
                if (items[0]) {
                    drop.value = items[0].type;
                    stack.value = items[0].stack;
                }
                if (items[1]) {
                    second.value = items[1].type;
                    secondStack.value = items[1].stack;
                }
                return undefined;
            }
        }

        const style = TileLoader.HasObjectData(m.Type) ? TileLoader.StyleAt(x, y) : 0;
        const item = TileLoader.GetItemDropFromTypeAndStyle(m.Type, style);
        if (item > 0) {
            drop.value = item;
            stack.value = 1;
        }
        return undefined;
    }

    // Os itens de mod que colocam cada tile: "tipo:estilo" e "tipo" -> item.
    static #ItemsByTile() {
        if (TileLoader.#itemsByTile) return TileLoader.#itemsByTile;

        const map = new Map();
        const item = Terraria.Item.new();
        item['void .ctor()']();

        for (const t of ItemLoader.ByType.keys()) {
            item['void SetDefaults(int Type, ItemVariant variant)'](t, null);
            const tile = item.createTile;
            if (tile < FIRST_TILE) continue;

            const key = tile + ':' + item.placeStyle;
            if (!map.has(key)) map.set(key, t);
            if (!map.has(tile)) map.set(tile, t);
        }
        return (TileLoader.#itemsByTile = map);
    }

    // TileID.Sets.HasOutlines pede o contorno (Textura_Highlight). Sem o
    // arquivo, o contorno sai: o jogo desenharia uma textura que não existe.
    static #Highlight(m) {
        const Sets = Terraria.ID.TileID.Sets;
        const root = m.Mod && m.Mod.path;
        const file = root ? bl.path.join(root, ModFiles.Texture(m.HighlightTexture)) : null;

        if (!file || !bl.file.exists(file)) {
            Sets.HasOutlines[m.Type] = false;
            return;
        }
        const asset = Safe.Run(m.constructor.name + ' (contorno)', () => bl.loadTextureAsset(file));
        if (asset) Terraria.GameContent.TextureAssets.HighlightMask[m.Type] = asset;
        else Sets.HasOutlines[m.Type] = false;
    }

    static #Shape(type) {
        let shape = TileLoader.#shapes.get(type);
        if (shape === undefined) {
            const data = TileLoader.ObjectData(type);
            shape = !data ? 0 : data.Width !== 1 || data.Height !== 1 ? 2 : 1;
            TileLoader.#shapes.set(type, shape);
        }
        return shape;
    }

    // O estilo pelo quadro da célula, como o CheckModTile do tModLoader.
    static #StyleOf(data, frameX, frameY) {
        const subX = Math.floor(frameX / data.CoordinateFullWidth);
        const subY = Math.floor(frameY / data.CoordinateFullHeight);
        const wrap = data.StyleWrapLimit || 1;
        const skip = data.StyleLineSkip || 1;
        const subTile = data.StyleHorizontal
            ? Math.floor(subY / skip) * wrap + subX
            : Math.floor(subX / skip) * wrap + subY;

        return Math.floor(subTile / (data.StyleMultiplier || 1));
    }

    static #IsWhole(left, top, width, height, type) {
        for (let x = left; x < left + width; x++) {
            for (let y = top; y < top + height; y++) {
                if (bl.tiles.typeAt(x, y) !== type) return false;
            }
        }
        return true;
    }

    // O objeto ainda cabe onde está (âncoras, parede, líquido)? O CanPlace do
    // jogo recusa o lugar ocupado, e ali está o próprio objeto: durante a
    // pergunta, o tipo conta como "quebra ao colocar" (o checkStay do tModLoader).
    static #Stays(x, y, type, style) {
        const breakable = Terraria.ID.TileID.Sets.BreakableWhenPlacing;
        const was = breakable[type];
        breakable[type] = true;
        try {
            return Terraria.TileObject['bool CanPlace(int x, int y, int type, int style, int dir, out TileObject objectData, bool onlyCheck, Nullable<int> forcedRandom)'](
                x, y, type, style, 0, new Ref(), false, null);
        } finally {
            breakable[type] = was;
        }
    }
}
