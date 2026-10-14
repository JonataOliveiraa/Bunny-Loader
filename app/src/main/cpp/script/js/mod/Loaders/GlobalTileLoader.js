// Os GlobalTile: um gancho por método sobrescrito, sem o filtro nativo pelo
// tipo (vale para os tiles do jogo também). Os ganchos dos tiles de mod
// (TileLoader e companhia) ficam nos mesmos métodos do jogo, encadeados.
class GlobalTileLoader {
    static #dustLeft = -1;        // o NumDust da quebra em andamento; -1 = sem limite
    static #lastPress = new Map();
    static #adj = null;           // tipo -> os tipos que ele conta como (AdjTiles)

    static Hook(cls) {
        const has = (name) => globalTiles.Overrides(cls, name);
        const once = (key, install) => Hooks.Once('gtile.' + key, () => Safe.Run('GlobalTile (' + key + ')', install));

        if (has('PostSetupTileMerge')) {
            Ready.Add(() => globalTiles.Each('PostSetupTileMerge', (g) => g.PostSetupTileMerge()));
        }
        if (has('CanKillTile')) once('CanKillTile', GlobalTileLoader.#HookCanKill);
        if (['KillTile', 'NumDust'].some(has)) once('KillTile', GlobalTileLoader.#HookKill);
        if (['NumDust', 'CreateDust'].some(has)) once('Dust', GlobalTileLoader.#HookDust);
        if (has('KillSound')) once('KillSound', GlobalTileLoader.#HookSound);
        if (['CanDrop', 'Drop'].some(has)) once('Drop', GlobalTileLoader.#HookDrop);
        if (has('CanPlace')) once('CanPlace', GlobalTileLoader.#HookCanPlace);
        if (has('PlaceInWorld')) once('PlaceInWorld', GlobalTileLoader.#HookPlace);
        if (has('CanExplode')) once('CanExplode', GlobalTileLoader.#HookExplode);
        if (has('Slope')) once('Slope', GlobalTileLoader.#HookSlope);
        if (has('RandomUpdate')) once('RandomUpdate', GlobalTileLoader.#HookRandomUpdate);
        if (['PreHitWire', 'HitWire'].some(has)) once('Wire', GlobalTileLoader.#HookWire);
        if (has('RightClick')) once('RightClick', GlobalTileLoader.#HookRightClick);
        if (has('MouseOver')) once('MouseOver', GlobalTileLoader.#HookMouseOver);
        if (has('MouseOverFar')) once('MouseOverFar', GlobalTileLoader.#HookMouseOverFar);
        if (has('AnimateTile')) once('AnimateTile', GlobalTileLoader.#HookAnimate);
        if (has('AdjTiles')) once('AdjTiles', GlobalTileLoader.#HookAdjTiles);
        if (['PreShakeTree', 'ShakeTree'].some(has)) once('ShakeTree', GlobalTileLoader.#HookShake);
    }

    // O jogo tranca o TileObjectData no fim do Initialize ("Tile data is
    // locked"); o tModLoader tira a trava, aqui ela sai só durante o setup.
    static Unlocked(run) {
        const TileObjectData = Terraria.ObjectData.TileObjectData;
        const was = TileObjectData.readOnlyData;
        TileObjectData.readOnlyData = false;
        try {
            return run();
        } finally {
            TileObjectData.readOnlyData = was;
        }
    }

    static #Type(i, j) {
        return bl.tiles.typeAt(i, j);
    }

    static #HookCanKill() {
        Terraria.WorldGen['bool CanKillTile(int i, int j, out bool blockDamaged)'].hook((original, i, j, blockDamaged) => {
            const type = GlobalTileLoader.#Type(i, j);
            if (type >= 0 && !globalTiles.All('CanKillTile', (g) => g.CanKillTile(i, j, type, blockDamaged))) {
                blockDamaged.value = false;
                return false;
            }
            return original(i, j, blockDamaged);
        });
    }

    static #HookKill() {
        Terraria.WorldGen['void KillTile(int i, int j, bool fail, bool effectOnly, bool noItem)'].hook(
            (original, i, j, fail, effectOnly, noItem) => {
                const type = GlobalTileLoader.#Type(i, j);
                if (type < 0) return original(i, j, fail, effectOnly, noItem);

                const f = new Ref(fail), e = new Ref(effectOnly), n = new Ref(noItem);
                globalTiles.Each('KillTile', (g) => g.KillTile(i, j, type, f, e, n));

                const limits = globalTiles.With('NumDust').length > 0;
                if (limits) {
                    const num = new Ref(f.value ? 3 : 10);
                    globalTiles.Each('NumDust', (g) => g.NumDust(i, j, type, f.value, num));
                    GlobalTileLoader.#dustLeft = Math.max(0, num.value | 0);
                }
                try {
                    return original(i, j, f.value, e.value, n.value);
                } finally {
                    if (limits) GlobalTileLoader.#dustLeft = -1;
                }
            });
    }

    // O jogo escolhe a partícula dentro do MakeTileDust: o CreateDust vê a
    // que nasceu e pode trocar o tipo ou apagá-la.
    static #HookDust() {
        const Main = Terraria.Main;

        Terraria.WorldGen['int KillTile_MakeTileDust(int i, int j, Tile tileCache)'].hook((original, i, j, tile) => {
            if (GlobalTileLoader.#dustLeft === 0) return 6000;
            if (GlobalTileLoader.#dustLeft > 0) GlobalTileLoader.#dustLeft--;

            const index = original(i, j, tile);
            if (!(index >= 0 && index < 6000) || !globalTiles.With('CreateDust').length) return index;

            const type = tile.type;
            const dust = Main.dust[index];
            const dustType = new Ref(dust.type);
            if (!globalTiles.All('CreateDust', (g) => g.CreateDust(i, j, type, dustType)) || dustType.value < 0) {
                dust.active = false;
                return 6000;
            }
            if (dustType.value !== dust.type) dust.type = dustType.value;
            return index;
        });
    }

    static #HookSound() {
        Terraria.WorldGen['void KillTile_PlaySounds(int i, int j, bool fail, Tile tileCache)'].hook((original, i, j, fail, tile) => {
            const type = tile.type;
            if (!globalTiles.All('KillSound', (g) => g.KillSound(i, j, type, fail))) return undefined;

            return original(i, j, fail, tile);
        });
    }

    static #HookDrop() {
        Terraria.WorldGen['void KillTile_DropItems(int x, int y, Tile tileCache, bool includeLargeObjectDrops)'].hook(
            (original, x, y, tile, large) => {
                const type = tile.type;
                if (!globalTiles.All('CanDrop', (g) => g.CanDrop(x, y, type))) return undefined;

                original(x, y, tile, large);
                globalTiles.Each('Drop', (g) => g.Drop(x, y, type));
                return undefined;
            });
    }

    // O mesmo lugar do jogo que recusa a colocação (perto de jogadores, nas
    // armadilhas...): o TryPlacing pergunta a ele antes de colocar.
    static #HookCanPlace() {
        const P = Terraria.Player;

        P['bool PlaceThing_Tiles_BlockPlacementForAssortedThings(bool canPlace)'].hook((original, self, canPlace) => {
            const result = original(self, canPlace);
            if (!result) return result;

            const type = self.inventory[self.selectedItemState.selected].createTile;
            if (type < 0) return result;

            const i = P.tileTargetX, j = P.tileTargetY;
            return globalTiles.All('CanPlace', (g) => g.CanPlace(i, j, type));
        });
    }

    // Objeto (TileObjectData) pelo gancho de depois de colocar; bloco simples
    // pelo PlaceTile, só quando é o jogador colocando.
    static #HookPlace() {
        const placed = (i, j, type) => {
            const player = Terraria.Main.player[Terraria.Main.myPlayer];
            const item = player.HeldItem;
            globalTiles.Each('PlaceInWorld', (g) => g.PlaceInWorld(i, j, type, item));
        };

        Terraria.ObjectData.TileObjectData['bool CallPostPlacementPlayerHook(int tileX, int tileY, int type, int style, int dir, int alternate, TileObject data)'].hook(
            (original, x, y, type, style, dir, alternate, data) => {
                const r = original(x, y, type, style, dir, alternate, data);
                placed(x, y, type);
                return r;
            });

        Terraria.WorldGen['bool PlaceTile(int i, int j, int Type, bool mute, bool forced, int plr, int style)'].hook(
            (original, i, j, type, mute, forced, plr, style) => {
                const ok = original(i, j, type, mute, forced, plr, style);
                if (ok && !TileLoader.HasObjectData(type)) placed(i, j, type);
                return ok;
            }, { whileIn: TileWorldLoader.PlacingGate() });
    }

    static #HookExplode() {
        Terraria.Projectile['bool CanExplodeTile(int x, int y, bool explodeHardmodeOres)'].hook((original, self, x, y, ores) => {
            if (!original(self, x, y, ores)) return false;

            const type = GlobalTileLoader.#Type(x, y);
            return type < 0 || globalTiles.All('CanExplode', (g) => g.CanExplode(x, y, type));
        });
    }

    // Só o martelo do jogador: o SlopeTile e o PoundTile também rodam na geração do mundo.
    static #HookSlope() {
        const W = Terraria.WorldGen;
        const pounding = Terraria.Player['void ItemCheck_UseMiningTools_TryPoundingTile(Item sItem, int tileHitId, ref bool hitWall, int x, int y)'];
        pounding.scope();

        const allowed = (i, j) => {
            const type = GlobalTileLoader.#Type(i, j);
            return type < 0 || globalTiles.All('Slope', (g) => g.Slope(i, j, type));
        };

        W['bool SlopeTile(int i, int j, int slope, bool noEffects, bool quiet)'].hook(
            (original, i, j, slope, noEffects, quiet) => (allowed(i, j) ? original(i, j, slope, noEffects, quiet) : false),
            { whileIn: pounding });
        W['bool PoundTile(int i, int j)'].hook(
            (original, i, j) => (allowed(i, j) ? original(i, j) : false), { whileIn: pounding });
    }

    static #HookRandomUpdate() {
        for (const [name, underground] of [['UpdateWorld_OvergroundTile', false], ['UpdateWorld_UndergroundTile', true]]) {
            Terraria.WorldGen[`void ${name}(int i, int j, bool checkNPCSpawns, int wallDist)`].hook((original, i, j, npcs, wallDist) => {
                original(i, j, npcs, wallDist);

                const type = GlobalTileLoader.#Type(i, j);
                if (type >= 0) globalTiles.Each('RandomUpdate', (g) => g.RandomUpdate(i, j, type, underground));
            });
        }
    }

    static #HookWire() {
        Terraria.Wiring['void HitWireSingle(int i, int j)'].hook((original, i, j) => {
            const type = GlobalTileLoader.#Type(i, j);
            if (type < 0) return original(i, j);
            if (!globalTiles.All('PreHitWire', (g) => g.PreHitWire(i, j, type))) return undefined;

            original(i, j);
            globalTiles.Each('HitWire', (g) => g.HitWire(i, j, type));
            return undefined;
        });
    }

    // O toque novo (ver TileUseLoader): o celular chama o TileInteractionsUse
    // três vezes por quadro.
    static #FreshPress(player) {
        if (!player.tileInteractAttempted) return false;

        const frame = Terraria.Main.GameUpdateCount;
        const last = GlobalTileLoader.#lastPress.get(player.whoAmI);
        GlobalTileLoader.#lastPress.set(player.whoAmI, frame);
        if (last === frame) return false;
        return player.releaseUseTile || last === undefined || frame - last > 1;
    }

    static #HookRightClick() {
        Terraria.Player['void TileInteractionsUse(int myX, int myY)'].hook((original, self, x, y) => {
            const fresh = GlobalTileLoader.#FreshPress(self);
            original(self, x, y);
            if (!fresh) return undefined;

            const type = GlobalTileLoader.#Type(x, y);
            if (type >= 0) globalTiles.Each('RightClick', (g) => g.RightClick(x, y, type));
            return undefined;
        });
    }

    static #HookMouseOver() {
        const P = Terraria.Player;
        P['void TileInteractionsMouseOver(int myX, int myY)'].hook((original, self, x, y) => {
            original(self, x, y);
            if (x !== P.tileTargetX || y !== P.tileTargetY) return;

            const type = GlobalTileLoader.#Type(x, y);
            if (type >= 0) globalTiles.Each('MouseOver', (g) => g.MouseOver(x, y, type));
        });
    }

    static #HookMouseOverFar() {
        Terraria.Player['void TileInteractionsCheckLongDistance(int myX, int myY)'].hook((original, self, x, y) => {
            original(self, x, y);

            const type = GlobalTileLoader.#Type(x, y);
            if (type >= 0) globalTiles.Each('MouseOverFar', (g) => g.MouseOverFar(x, y, type));
        });
    }

    static #HookAnimate() {
        Terraria.Main['void AnimateTiles()'].hook((original) => {
            original();
            globalTiles.Each('AnimateTile', (g) => g.AnimateTile());
        });
    }

    // O AdjTiles de cada tipo é perguntado uma vez, com tudo carregado.
    static #HookAdjTiles() {
        Ready.Add(() => {
            const map = new Map();
            const count = Terraria.Main.tileSolid.length;
            for (let type = 0; type < count; type++) {
                const extra = new Set();
                globalTiles.Each('AdjTiles', (g) => {
                    const list = g.AdjTiles(type);
                    if (list) for (const t of list) extra.add(t);
                });
                if (extra.size) map.set(type, [...extra]);
            }
            GlobalTileLoader.#adj = map;
        });

        Terraria.Player['void AdjTiles()'].hook((original, self) => {
            original(self);

            const map = GlobalTileLoader.#adj;
            if (!map || !map.size) return;

            const adj = self.adjTile;
            for (const [type, extra] of map) {
                if (!adj[type]) continue;
                for (const t of extra) adj[t] = true;
            }
        });
    }

    // O começo do ShakeTree do jogo (contagem, copa, lugar livre); com um
    // ShakeTree devolvendo true, a sacudida fica registrada e cai só a folha.
    static #HookShake() {
        const W = Terraria.WorldGen;
        W['void ShakeTree(int i, int j)'].hook((original, i, j) => {
            if (Safe.Run('GlobalTile.ShakeTree', () => GlobalTileLoader.#Shake(i, j)) !== true) original(i, j);
        });
    }

    static #Shake(i, j) {
        const W = Terraria.WorldGen;
        const count = W.numTreeShakes;
        if (count === W.maxTreeShakes) return false;

        const bx = new Ref(0), by = new Ref(0);
        const treeType = W['TreeTypes GetTreeTypeAndTreeBottom(int x, int y, out int bottomX, out int bottomY)'](i, j, bx, by);
        if (treeType === TreeTypes.None) return false;

        const x = bx.value, bottom = by.value;
        const xs = W.treeShakeX, ys = W.treeShakeY;
        for (let k = 0; k < count; k++) {
            if (xs[k] === x && ys[k] === bottom) return false;
        }

        const shakeable = Terraria.ID.TileID.Sets.IsShakeable;
        let top = bottom - 1;
        while (top > 10 && bl.tiles.typeAt(x, top) >= 0 && shakeable[bl.tiles.typeAt(x, top)]) top--;
        top++;
        if (!W['bool IsTileALeafyTreeTop(int i, int j)'](x, top) ||
            Terraria.Collision['bool SolidTiles(int startX, int endX, int startY, int endY)'](x - 2, x + 2, top - 2, top + 2)) return false;

        globalTiles.Each('PreShakeTree', (g) => g.PreShakeTree(x, top, treeType));
        if (!globalTiles.Any('ShakeTree', (g) => g.ShakeTree(x, top, treeType))) return false;

        xs[count] = x;
        ys[count] = bottom;
        W.numTreeShakes = count + 1;
        GlobalTileLoader.#Leaves(x, top, bottom);
        return true;
    }

    // A folha que cai da copa, como no fim do ShakeTree do jogo.
    static #Leaves(x, top, bottom) {
        const W = Terraria.WorldGen;
        const Main = Terraria.Main;
        const height = new Ref(0), frame = new Ref(0), style = new Ref(0);
        Safe.Run('folhas da arvore', () => {
            W['void GetTreeLeaf(int x, Tile topTile, Tile t, ref int treeHeight, out int treeFrame, out int passStyle)'](
                x, TileLoader.Tile(x, top), TileLoader.Tile(x, bottom), height, frame, style);
            const leaf = style.value;
            if (leaf === -1) return;

            if (Main.netMode & 2) {
                Terraria.NetMessage['void SendData(int msgType, int remoteClient, int ignoreClient, NetworkText text, int number, float number2, float number3, float number4, int number5, int number6, int number7)'](
                    112, -1, -1, null, 1, x, top, 1, leaf, 1, 0);
            }
            if (Main.netMode === 0) W['void TreeGrowFX(int x, int y, int height, int treeGore, bool hitTree)'](x, top, 1, leaf, true);
        });
    }
}
