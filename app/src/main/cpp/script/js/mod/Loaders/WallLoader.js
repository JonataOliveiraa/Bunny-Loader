class WallLoader {
    static ByType = new Map();
    static Animated = [];
    static LIGHT_PADDING = 28;

    static #itemsByWall = null;
    static #dustLeft = -1;
    static #fail = false;
    static #drop = undefined;

    static At(i, j) {
        return WallLoader.ByType.get(bl.walls.typeAt(i, j));
    }

    static Overrides(m, name) {
        return Hooks.Overrides(m.constructor, ModWall, name);
    }

    static Mark(m, cls) {
        const has = (name) => Hooks.Overrides(cls, ModWall, name);
        if (has('ModifyLight')) {
            bl.hookMarks.set('wall.light', m.Type);
            Hooks.Once('wall.light', WallLoader.#HookLight);
        }
        if (has('RandomUpdate')) {
            bl.hookMarks.set('wall.random', m.Type);
            Hooks.Once('wall.random', WallLoader.#HookRandom);
        }
        if (has('WallFrame')) {
            bl.hookMarks.set('wall.frame', m.Type);
            Hooks.Once('wall.frame', WallLoader.#HookFrame);
        }
        if (has('AnimateWall')) {
            WallLoader.Animated.push(m);
            Hooks.Once('wall.animate', WallLoader.#HookAnimation);
        }
    }

    static #HookLight() {
        const r = new Ref(0), g = new Ref(0), b = new Ref(0);
        const slices = [[], [], []];
        let next = 0;

        const compute = (x0, x1, y0, y1) => {
            const found = bl.walls.find('wall.light', x0, y0, x1, y1);
            const out = [];
            const byType = WallLoader.ByType;
            for (let k = 0; k < found.length; k += 3) {
                const i = found[k], j = found[k + 1];
                const m = byType.get(found[k + 2]);
                if (!m) continue;
                r.value = 0;
                g.value = 0;
                b.value = 0;
                try {
                    m.ModifyLight(i, j, r, g, b);
                } catch (e) {
                    Safe.Report(m.__lightLabel || (m.__lightLabel = m.constructor.name + '.ModifyLight'), e);
                    continue;
                }
                if (r.value > 0 || g.value > 0 || b.value > 0) out.push(i, j, r.value, g.value, b.value);
            }
            return out;
        };

        Terraria.Lighting['void LightTiles(int firstX, int lastX, int firstY, int lastY)'].hook((original, x0, x1, y0, y1) => {
            if (!Terraria.Main.gameMenu) {
                const pad = WallLoader.LIGHT_PADDING;
                const top = y0 - pad, bottom = y1 + pad;
                const n = slices.length;
                const step = Math.ceil((bottom - top + 1) / n);
                const k = next;
                next = (next + 1) % n;
                const from = top + k * step, to = Math.min(bottom, from + step - 1);
                slices[k] = compute(x0 - pad, x1 + pad, from, to);
            }
            return original(x0, x1, y0, y1);
        }, { ifBusy: 'original' });

        Terraria.Main['void DoUpdateInWorld()'].hook((original, self) => {
            original(self);
            if (Terraria.Main.gameMenu) return;
            for (const lights of slices) {
                if (lights.length) bl.tiles.addLights(lights);
            }
        }, { ifBusy: 'original' });
    }

    static #HookRandom() {
        for (const name of ['UpdateWorld_OvergroundTile', 'UpdateWorld_UndergroundTile']) {
            Terraria.WorldGen[`void ${name}(int i, int j, bool checkNPCSpawns, int wallDist)`].hook((original, i, j, npcs, wallDist) => {
                original(i, j, npcs, wallDist);
                const m = WallLoader.At(i, j);
                if (m) Safe.Run(m.constructor.name + '.RandomUpdate', () => m.RandomUpdate(i, j));
            }, { minType: FIRST_WALL, tileAt: [0, 1], wall: true, marks: 'wall.random' });
        }
    }

    static #HookFrame() {
        const Framing = Terraria.Framing;
        let lookup = null;
        const styleOf = new Map();
        const table = () => {
            if (lookup) return lookup;
            const found = Framing.wallFrameLookup;
            if (!found) return null;
            for (let style = 0; style < found.length; style++) {
                const row = found[style];
                if (!row) continue;
                for (let n = 0; n < row.length; n++) {
                    const key = row[n].X + ',' + row[n].Y + ',' + n;
                    if (!styleOf.has(key)) styleOf.set(key, style);
                }
            }
            return (lookup = found);
        };
        const tileOf = (i, j) => Terraria.Main.tile['Tile get_Item(int x, int y)'](i, j);
        const style = new Ref(0), number = new Ref(0);

        Framing['void WallFrame(int i, int j, bool resetFrame)'].hook((original, i, j, reset) => {
            const tile = tileOf(i, j);
            const x0 = tile['int wallFrameX()'](), y0 = tile['int wallFrameY()'](), n0 = tile['byte wallFrameNumber()']();
            original(i, j, reset);

            const m = WallLoader.At(i, j);
            if (!m || !table()) return;
            const x1 = tile['int wallFrameX()'](), y1 = tile['int wallFrameY()'](), n1 = tile['byte wallFrameNumber()']();
            const found = styleOf.get(x1 + ',' + y1 + ',' + n1);
            style.value = found === undefined ? 0 : found;
            number.value = n1;
            const ok = Safe.Run(m.constructor.name + '.WallFrame', () => m.WallFrame(i, j, !!reset, style, number));
            if (ok === false) {
                tile['void wallFrameX(int wallFrameX)'](x0);
                tile['void wallFrameY(int wallFrameY)'](y0);
                tile['void wallFrameNumber(byte wallFrameNumber)'](n0);
                return;
            }
            const s = style.value | 0, n = number.value | 0;
            if ((found !== undefined && s === found) && n === n1) return;
            const point = lookup[s] && lookup[s][n];
            if (!point) return;
            tile['void wallFrameNumber(byte wallFrameNumber)'](n);
            tile['void wallFrameX(int wallFrameX)'](point.X);
            tile['void wallFrameY(int wallFrameY)'](point.Y);
        }, { minType: FIRST_WALL, tileAt: [0, 1], wall: true, marks: 'wall.frame' });
    }

    static #HookAnimation() {
        const frame = new Ref(0), counter = new Ref(0);
        Terraria.Main['void DoUpdate_AnimateWalls()'].hook((original) => {
            original();
            const Main = Terraria.Main;
            const frames = Main.wallFrame, counters = Main.wallFrameCounter;
            for (const m of WallLoader.Animated) {
                const t = m.Type;
                const f0 = frames[t], c0 = counters[t];
                frame.value = f0;
                counter.value = c0;
                try {
                    m.AnimateWall(frame, counter);
                } catch (e) {
                    Safe.Report(m.__animateLabel || (m.__animateLabel = m.constructor.name + '.AnimateWall'), e);
                }
                const f1 = frame.value & 0xFF, c1 = counter.value & 0xFF;
                if (f1 !== f0) frames[t] = f1;
                if (c1 !== c0) counters[t] = c1;
            }
        }, { ifBusy: 'original' });
    }

    static GetItemDrop(m) {
        if (m.ItemDrop !== undefined && m.ItemDrop !== null) return m.ItemDrop | 0;
        return WallLoader.#ItemsByWall().get(m.Type) || 0;
    }

    static #ItemsByWall() {
        if (WallLoader.#itemsByWall) return WallLoader.#itemsByWall;

        const map = new Map();
        const item = Terraria.Item.new();
        item['void .ctor()']();
        for (const t of ItemLoader.ByType.keys()) {
            item['void SetDefaults(int Type, ItemVariant variant)'](t, null);
            const wall = item.createWall;
            if (wall >= FIRST_WALL && !map.has(wall)) map.set(wall, t);
        }
        return (WallLoader.#itemsByWall = map);
    }

    static Install() {
        const W = Terraria.WorldGen;
        const at = WallLoader.At;
        const of = (tile) => WallLoader.ByType.get(tile.wall);

        W['void KillWall(int i, int j, bool fail)'].hook((original, i, j, fail) => {
            const m = at(i, j);
            if (!m) return original(i, j, fail);

            const name = m.constructor.name;
            const f = new Ref(!!fail);
            Safe.Run(name + '.KillWall', () => m.KillWall(i, j, f));
            const num = new Ref(f.value ? 1 : 3);
            Safe.Run(name + '.NumDust', () => m.NumDust(i, j, f.value, num));

            const outer = [WallLoader.#dustLeft, WallLoader.#fail];
            WallLoader.#dustLeft = Math.max(0, num.value | 0);
            WallLoader.#fail = f.value;
            try {
                return original(i, j, f.value);
            } finally {
                [WallLoader.#dustLeft, WallLoader.#fail] = outer;
            }
        }, { minType: FIRST_WALL, tileAt: [0, 1], wall: true });

        const newDust = Terraria.Dust['int NewDust(Vector2 Position, int Width, int Height, int Type, float SpeedX, float SpeedY, int Alpha, Color newColor, float Scale)'];
        W['void KillWall_MakeWallDust(int i, int j, Tile tileCache)'].hook((original, i, j, tile) => {
            const m = of(tile);
            if (!m) return original(i, j, tile);

            if (WallLoader.#dustLeft === 0) return undefined;
            if (WallLoader.#dustLeft > 0) WallLoader.#dustLeft--;

            const type = new Ref(m.DustType);
            if (Safe.Run(m.constructor.name + '.CreateDust', () => m.CreateDust(i, j, type)) === false) return undefined;
            if (type.value === undefined || type.value === null) return original(i, j, tile);
            if (type.value < 0) return undefined;

            newDust(Vector2.new(i * 16, j * 16), 16, 16, type.value, 0, 0, 0, Color.White, 1);
            return undefined;
        }, { minType: FIRST_WALL, tile: 2, wall: true });

        const Engine = Terraria.Audio.SoundEngine;
        const playInt = Engine['SoundEffectInstance PlaySound(int type, int x, int y, int Style, float volumeScale, float pitchOffset)'];
        const playStyle = Engine['SoundEffectInstance PlaySound(LegacySoundStyle type, Vector2 position, float pitchOffset, float volumeScale)'];
        W['void KillWall_PlaySounds(int i, int j, Tile tileCache)'].hook((original, i, j, tile) => {
            const m = of(tile);
            if (!m) return original(i, j, tile);
            if (Safe.Run(m.constructor.name + '.KillSound', () => m.KillSound(i, j, WallLoader.#fail)) === false) return undefined;

            const sound = m.HitSound;
            if (sound === undefined || sound === null) return original(i, j, tile);
            if (typeof sound === 'number') playInt(sound, i * 16, j * 16, 1, 1, 0);
            else playStyle(sound, Vector2.new(i * 16, j * 16), 0, 1);
            return undefined;
        }, { minType: FIRST_WALL, tile: 2, wall: true });

        W['void KillWall_DropItems(int i, int j, Tile tileCache)'].hook((original, i, j, tile) => {
            const m = of(tile);
            if (!m) return original(i, j, tile);

            const type = new Ref(WallLoader.GetItemDrop(m));
            if (Safe.Run(m.constructor.name + '.Drop', () => m.Drop(i, j, type)) === false) return undefined;

            const outer = WallLoader.#drop;
            WallLoader.#drop = type.value | 0;
            try {
                return original(i, j, tile);
            } finally {
                WallLoader.#drop = outer;
            }
        }, { minType: FIRST_WALL, tile: 2, wall: true });

        W['int KillWall_GetItemDrops(Tile tileCache)'].hook((original, tile) => {
            const m = of(tile);
            if (!m) return original(tile);
            return WallLoader.#drop !== undefined ? WallLoader.#drop : WallLoader.GetItemDrop(m);
        }, { minType: FIRST_WALL, tile: 0, wall: true });
    }
}
