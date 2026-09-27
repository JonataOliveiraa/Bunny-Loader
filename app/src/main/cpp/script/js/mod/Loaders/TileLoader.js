// Os métodos recebem a posição (i, j) em tiles. O filtro nativo pelo tipo do
// tile deixa os blocos do jogo fora do JS.
class TileLoader {
    static ByType = new Map();

    static #itemsByTile = null;
    static #shapes = new Map();   // tipo -> 0 sem TileObjectData, 1 de uma célula, 2 de várias

    static At(i, j) {
        return TileLoader.ByType.get(bl.tiles.typeAt(i, j));
    }

    static ObjectData(type) {
        return Terraria.ObjectData.TileObjectData['TileObjectData GetTileData(int type, int style, int alternate)'](type, 0, 0);
    }

    static HasObjectData(type) { return TileLoader.#Shape(type) !== 0; }
    static IsMultiTile(type) { return TileLoader.#Shape(type) === 2; }

    // O ItemDrop, ou o item de mod que coloca este tile.
    static ItemDrop(m) {
        if (m.ItemDrop !== undefined) return m.ItemDrop;

        if (!TileLoader.#itemsByTile) {
            const map = new Map();
            const item = Terraria.Item.new();
            item['void .ctor()']();

            for (const t of ItemLoader.ByType.keys()) {
                item['void SetDefaults(int Type, ItemVariant variant)'](t, null);
                if (item.createTile >= FIRST_TILE && !map.has(item.createTile)) map.set(item.createTile, t);
            }
            TileLoader.#itemsByTile = map;
        }
        return TileLoader.#itemsByTile.get(m.Type) || 0;
    }

    // O CheckModTile do tModLoader: chamado quando o jogo enquadra uma célula de
    // um objeto de mod. Faltou uma célula ou o apoio: sai o objeto inteiro, com um drop só.
    static CheckObject(i, j, type) {
        const W = Terraria.WorldGen;
        if (W.destroyObject) return;

        let data = TileLoader.ObjectData(type);
        if (!data) return;

        const tile = Terraria.Main.tile['Tile get_Item(int x, int y)'](i, j);
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
        const W = Terraria.WorldGen;
        const at = TileLoader.At;

        Terraria.Player['int GetPickaxeDamage(int x, int y, int pickPower, int hitBufferIndex, Tile tileTarget)'].hook(
            (original, self, x, y, pickPower, hit, tile) => {
                const damage = original(self, x, y, pickPower, hit, tile);

                const m = at(x, y);
                if (!m) return damage;
                if (pickPower < m.MinPick) return 0;

                return m.MineResist > 0 ? Math.floor(damage / m.MineResist) : damage;
            }, { minType: FIRST_TILE, tile: 4 });

        // O jogo enquadra os objetos dele um a um pelo tipo (Check2x2...); o de
        // mod com TileObjectData passa pelo CheckObject.
        W['void TileFrameImportant(int i, int j, int type, Tile tileCache, bool resetFrame)'].hook(
            (original, i, j, type, tile, resetFrame) => {
                if (!TileLoader.ByType.has(type) || !TileLoader.HasObjectData(type)) return original(i, j, type, tile, resetFrame);

                Safe.Run('TileFrame de objeto de mod', () => TileLoader.CheckObject(i, j, type));
                return undefined;
            }, { minType: FIRST_TILE, tile: 3 });

        W['bool CanKillTile(int i, int j, out bool blockDamaged)'].hook((original, i, j, blockDamaged) => {
            const m = at(i, j);
            if (m && Safe.Run(m.constructor.name + '.CanKillTile', () => m.CanKillTile(i, j)) === false) {
                blockDamaged.value = false;
                return false;
            }
            return original(i, j, blockDamaged);
        }, { minType: FIRST_TILE, tileAt: [0, 1] });

        W['void KillTile(int i, int j, bool fail, bool effectOnly, bool noItem)'].hook(
            (original, i, j, fail, effectOnly, noItem) => {
                const m = at(i, j);
                if (m) Safe.Run(m.constructor.name + '.KillTile', () => m.KillTile(i, j, fail, effectOnly, noItem));

                return original(i, j, fail, effectOnly, noItem);
            }, { minType: FIRST_TILE, tileAt: [0, 1] });

        W['void KillTile_GetItemDrops(int x, int y, Tile tileCache, out int dropItem, out int dropItemStack, out int secondaryItem, out int secondaryItemStack, out bool noPrefix, bool includeLargeObjectDrops)'].hook(
            (original, x, y, tile, drop, stack, second, secondStack, noPrefix, large) => {
                original(x, y, tile, drop, stack, second, secondStack, noPrefix, large);

                const m = at(x, y);
                if (!m) return;

                // Objeto de várias células: o drop sai uma vez, no CheckObject (large).
                if (!large && TileLoader.IsMultiTile(m.Type)) {
                    drop.value = 0;
                    second.value = 0;
                    return;
                }

                const item = TileLoader.ItemDrop(m);
                if (item > 0) {
                    drop.value = item;
                    stack.value = 1;
                }
            }, { minType: FIRST_TILE, tile: 2 });

        const newDust = Terraria.Dust['int NewDust(Vector2 Position, int Width, int Height, int Type, float SpeedX, float SpeedY, int Alpha, Color newColor, float Scale)'];
        W['int KillTile_MakeTileDust(int i, int j, Tile tileCache)'].hook((original, i, j, tile) => {
            const m = at(i, j);
            if (!m) return original(i, j, tile);
            if (Safe.Run(m.constructor.name + '.CreateDust', () => m.CreateDust(i, j)) === false) return 6000;

            return newDust(Vector2.new(i * 16, j * 16), 16, 16, m.DustType, 0, 0, 0, Color.White, 1);
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
