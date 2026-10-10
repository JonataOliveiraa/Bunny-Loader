// As árvores de mod (ModTree). O desenho do tronco e da copa fica no nativo
// (bl.tiles.setTree, ModTrees.cpp); aqui o resto, pelo chão debaixo do tronco:
// o chão de mod serve para crescer, a muda de mod cresce, a madeira, a bolota,
// as folhas, a poeira, sacudir e a bolota plantada no chão de mod virar a muda.
class TreeLoader {
    static ByGround = new Map();   // tipo do chão -> árvore
    static #trees = [];

    static Add(inst) {
        TreeLoader.#trees.push(inst);
        if (TreeLoader.#trees.length === 1) Ready.Add(() => TreeLoader.#Setup(), 'setup');
    }

    // A textura ao lado da classe: <Texture><sufixo>.png.
    static Texture(tree, suffix) {
        const mod = tree.Mod;
        return ContentLookup.RequestFile(bl.path.join(mod.path, ModFiles.Texture(tree.Texture + suffix, mod)));
    }

    // A árvore cujo tronco passa por (i, j): o "fundo" do jogo é o chão.
    static At(i, j) {
        const x = new Ref(0), y = new Ref(0);
        Terraria.WorldGen['void GetTreeBottom(int i, int j, out int x, out int y)'](i, j, x, y);
        return TreeLoader.ByGround.get(bl.tiles.typeAt(x.value, y.value));
    }

    // Na thread do jogo, com o conteúdo pronto: as copas e os galhos ganham um
    // estilo cada, depois dos do jogo, em TextureAssets.TreeTop/TreeBranch.
    static #Setup() {
        const T = Terraria.GameContent.TextureAssets;
        const trees = TreeLoader.#trees;
        const first = T.TreeTop.length;
        const tops = T.TreeTop.cloneResized(first + trees.length);
        const branches = T.TreeBranch.cloneResized(first + trees.length);
        const ready = [];

        trees.forEach((tree, k) => {
            const name = tree.constructor.name;
            ContentLookup.As(tree.Mod, () => Safe.Run(name + '.SetStaticDefaults', () => tree.SetStaticDefaults()));
            const assets = ContentLookup.As(tree.Mod, () => Safe.Run(name + ' (textures)',
                () => [tree.GetTexture(), tree.GetTopTextures(), tree.GetBranchTextures()]));
            if (!assets) return;
            tops[first + k] = assets[1];
            branches[first + k] = assets[2];
            ready.push({ tree, trunk: assets[0], style: first + k });
        });
        T.TreeTop = tops;
        T.TreeBranch = branches;

        for (const { tree, trunk, style } of ready) {
            for (const ground of tree.GrowsOnTileId || []) {
                const other = TreeLoader.ByGround.get(ground);
                if (other) {
                    bl.error('ModTree: ' + tree.constructor.name + ' and ' + other.constructor.name + ' both grow on tile ' + ground + '; ' + tree.constructor.name + ' was left out there');
                    continue;
                }
                TreeLoader.ByGround.set(ground, tree);
                bl.tiles.setTree(ground, trunk.Value, style);
            }
        }
        if (TreeLoader.ByGround.size) Safe.Run('ModTree: ganchos', () => TreeLoader.#Hook());
    }

    static #Hook() {
        const W = Terraria.WorldGen;
        const self = TreeLoader;
        const minGround = Math.min(...self.ByGround.keys());
        bl.hookMarks.set('tree.trunk', Terraria.ID.TileID.Trees);
        bl.hookMarks.set('tree.sapling', Terraria.ID.TileID.Saplings);
        const over = (tree, name) => Hooks.Overrides(tree.constructor, ModTree, name);

        W['bool IsTileTypeFitForTree(ushort type)'].hook(
            (original, type) => self.ByGround.has(type) || original(type), { minType: minGround, arg: 0 });

        W['TreeTypes GetTreeType(int tileType)'].hook((original, type) => {
            const tree = self.ByGround.get(type);
            return tree ? tree.CountsAsTreeType : original(type);
        }, { minType: minGround, arg: 0 });

        // O GrowTree só desce pela muda do jogo (20) e só ela pode ocupar o
        // espaço do tronco: a muda de mod finge ser a 20 enquanto ele roda.
        W['bool GrowTree(int i, int y, int treeHeightAddon, bool ignoreWalls)'].hook((original, i, y, addon, walls) => {
            const type = bl.tiles.typeAt(i, y);
            if (!Terraria.ID.TileID.Sets.TreeSapling[type]) return original(i, y, addon, walls);
            const cells = [];
            for (let k = y; bl.tiles.typeAt(i, k) === type; k++) cells.push(k);
            for (let k = y - 1; bl.tiles.typeAt(i, k) === type; k--) cells.push(k);
            for (const k of cells) TileLoader.Tile(i, k).type = Terraria.ID.TileID.Saplings;
            const grew = original(i, y, addon, walls);
            if (!grew) for (const k of cells) TileLoader.Tile(i, k).type = type;
            return grew;
        }, { minType: FIRST_TILE, tileAt: [0, 1] });

        W['bool AttemptToGrowTreeFromSapling(int x, int y, bool underground, int treeHeightAddon, bool ignoreWalls)'].hook(
            (original, x, y, underground, addon, walls) => {
                const m = TileLoader.At(x, y);
                if (!m || !Terraria.ID.TileID.Sets.TreeSapling[m.Type]) return original(x, y, underground, addon, walls);
                if (Terraria.Main.netMode === 1) return false;
                return Safe.Run(m.constructor.name + '.GrowSapling', () => m.GrowSapling(x, y, underground, addon, walls)) === true;
            }, { minType: FIRST_TILE, tileAt: [0, 1] });

        // A bolota no chão de uma árvore de mod vira a muda dela.
        Terraria.TileObject['bool CanPlace(int x, int y, int type, int style, int dir, out TileObject objectData, bool onlyCheck, Nullable<int> forcedRandom)'].hook(
            (original, x, y, type, style, dir, data, onlyCheck, random) => {
                const tree = self.ByGround.get(bl.tiles.typeAt(x, y + 1));
                if (tree) {
                    const sapling = new Ref(style);
                    const t = Safe.Run(tree.constructor.name + '.SaplingGrowthType', () => tree.SaplingGrowthType(sapling));
                    if (Number.isInteger(t)) {
                        type = t;
                        style = sapling.value;
                    }
                }
                return original(x, y, type, style, dir, data, onlyCheck, random);
            }, { minType: 0, arg: 2, marks: 'tree.sapling' });

        W['void KillTile_GetTreeDrops(int i, int j, Tile tileCache, ref bool dropIsWood, ref bool bonusWood, ref int dropItem, ref int secondaryItem)'].hook(
            (original, i, j, tile, isWood, bonus, drop, second) => {
                original(i, j, tile, isWood, bonus, drop, second);
                const tree = self.At(i, j);
                if (!tree) return;
                const name = tree.constructor.name;
                if (isWood.value) {
                    const wood = Safe.Run(name + '.DropWood', () => tree.DropWood());
                    if (Number.isInteger(wood)) drop.value = wood;
                }
                const acorn = Terraria.ID.ItemID.Acorn;
                if (Safe.Run(name + '.CanDropAcorn', () => tree.CanDropAcorn()) === false) {
                    if (second.value === acorn) second.value = 0;
                } else if (second.value === 0 && tile.frameX >= 22 && tile.frameY >= 198 &&
                           Terraria.Main.netMode !== 1 && Math.random() < 0.5) {
                    // A copa: o jogo só dá bolota às árvores dele.
                    second.value = acorn;
                }
            }, { minType: 0, tile: 2, marks: 'tree.trunk' });

        // `t` é o chão. Também no desenho (as folhas que caem): sem esperar o motor.
        W['void GetTreeLeaf(int x, Tile topTile, Tile t, ref int treeHeight, out int treeFrame, out int passStyle)'].hook(
            (original, x, top, ground, height, frame, pass) => {
                original(x, top, ground, height, frame, pass);
                const tree = self.ByGround.get(ground.type);
                if (!tree) return;
                const leaf = Safe.Run(tree.constructor.name + '.TreeLeaf', () => tree.TreeLeaf());
                pass.value = Number.isInteger(leaf) ? leaf : -1;
            }, { minType: minGround, tile: 2, ifBusy: 'original' });

        if ([...self.ByGround.values()].some((tree) => over(tree, 'CreateDust'))) {
            const newDust = Terraria.Dust['int NewDust(Vector2 Position, int Width, int Height, int Type, float SpeedX, float SpeedY, int Alpha, Color newColor, float Scale)'];
            W['int KillTile_MakeTileDust(int i, int j, Tile tileCache)'].hook((original, i, j, tile) => {
                const tree = self.At(i, j);
                if (!tree || !over(tree, 'CreateDust')) return original(i, j, tile);
                const dust = Safe.Run(tree.constructor.name + '.CreateDust', () => tree.CreateDust());
                return Number.isInteger(dust) && dust >= 0 ? newDust(Vector2.new(i * 16, j * 16), 16, 16, dust, 0, 0, 0, Color.White, 1) : 6000;
            }, { minType: 0, tile: 2, marks: 'tree.trunk' });
        }

        if ([...self.ByGround.values()].some((tree) => over(tree, 'Shake'))) {
            W['void ShakeTree(int i, int j)'].hook((original, i, j) => {
                if (Safe.Run('ModTree.Shake', () => self.#Shake(i, j)) !== true) original(i, j);
            }, { minType: 0, tileAt: [0, 1], marks: 'tree.trunk' });
        }
    }

    // O ShakeTree do jogo até a hora das quedas; o Shake do mod decide se elas
    // vêm. true = já tratado aqui (o do jogo não roda).
    static #Shake(i, j) {
        const W = Terraria.WorldGen;
        const bx = new Ref(0), by = new Ref(0);
        W['void GetTreeBottom(int i, int j, out int x, out int y)'](i, j, bx, by);
        const x = bx.value, bottom = by.value;
        const tree = TreeLoader.ByGround.get(bl.tiles.typeAt(x, bottom));
        if (!tree || !Hooks.Overrides(tree.constructor, ModTree, 'Shake')) return false;

        const count = W.numTreeShakes;
        if (count === W.maxTreeShakes) return true;
        const xs = W.treeShakeX, ys = W.treeShakeY;
        for (let k = 0; k < count; k++) {
            if (xs[k] === x && ys[k] === bottom) return true;
        }

        const shakeable = Terraria.ID.TileID.Sets.IsShakeable;
        let top = bottom - 1;
        while (top > 10 && bl.tiles.typeAt(x, top) >= 0 && shakeable[bl.tiles.typeAt(x, top)]) top--;
        top++;
        // Sem copa, ou presa: o jogo marca e não dá nada.
        if (!W['bool IsTileALeafyTreeTop(int i, int j)'](x, top) ||
            Terraria.Collision['bool SolidTiles(int startX, int endX, int startY, int endY)'](x - 2, x + 2, top - 2, top + 2)) return false;

        const leaves = new Ref(true);
        if (tree.Shake(x, top, leaves) !== false) return false;

        xs[count] = x;
        ys[count] = bottom;
        W.numTreeShakes = count + 1;
        if (!leaves.value) return true;
        const leaf = tree.TreeLeaf();
        if (!Number.isInteger(leaf) || leaf < 0) return true;
        const Main = Terraria.Main;
        if (Main.netMode & 2) {
            Terraria.NetMessage['void SendData(int msgType, int remoteClient, int ignoreClient, NetworkText text, int number, float number2, float number3, float number4, int number5, int number6, int number7)'](
                112, -1, -1, null, 1, x, top, 1, leaf, 1, 0);
        }
        if (Main.netMode === 0) W['void TreeGrowFX(int x, int y, int height, int treeGore, bool hitTree)'](x, top, 1, leaf, true);
        return true;
    }
}
