// Luz, animação e desenho dos tiles de mod. O desenho do jogo roda em várias
// threads e fica no nativo; o JS entra só para os tipos marcados, e o que o
// mod desenha (chamas, PreDraw, SpecialDraw) sai numa passada própria, depois
// dos tiles, na thread do jogo.
class TileDrawLoader {
    static #special = null;   // os AddSpecialPoint pedidos durante a passada

    static Install() {
        Safe.Run('dados de desenho dos tiles', TileDrawLoader.#HookDrawData);
    }

    // O jogo calcula a luz dos tiles numa cópia interna do ApplyTileLight (o
    // hook nele não pega), então a luz do tile de mod entra como a de um
    // projétil: Lighting.AddLight, a cada quadro, na atualização do mundo.
    // O ModifyLight roda a cada LIGHT_EVERY quadros (e só com
    // Main.tileLighted[tipo], como no jogo); nos outros vale a última conta.
    static LIGHT_EVERY = 3;
    static LIGHT_PADDING = 28;

    static HookLight() {
        const r = new Ref(0), g = new Ref(0), b = new Ref(0);
        let lights = [];
        let age = TileDrawLoader.LIGHT_EVERY;

        const compute = (x0, x1, y0, y1) => {
            const found = bl.tiles.find('tile.light', x0, y0, x1, y1);
            const out = [];
            for (let k = 0; k < found.length; k += 2) {
                const i = found[k], j = found[k + 1];
                const m = TileLoader.At(i, j);
                if (!m || !Terraria.Main.tileLighted[m.Type]) continue;

                r.value = 0;
                g.value = 0;
                b.value = 0;
                Safe.Run(m.constructor.name + '.ModifyLight', () => m.ModifyLight(i, j, r, g, b));
                if (r.value > 0 || g.value > 0 || b.value > 0) out.push(i, j, r.value, g.value, b.value);
            }
            return out;
        };

        // A área vem do LightTiles (o desenho); a luz entra na atualização do
        // mundo, como a de um projétil. No motor novo (modos Cor e Branco) o
        // AddLight feito dentro do LightTiles não aparecia: só o do Retro e
        // do Psicodélico, que usam o motor antigo.
        Terraria.Lighting['void LightTiles(int firstX, int lastX, int firstY, int lastY)'].hook((original, x0, x1, y0, y1) => {
            if (!Terraria.Main.gameMenu && ++age >= TileDrawLoader.LIGHT_EVERY) {
                age = 0;
                // O motor novo varre 28 tiles além da tela (LightingEngine.ProcessScan):
                // um tile de mod logo fora dela também ilumina a borda.
                const pad = TileDrawLoader.LIGHT_PADDING;
                lights = Safe.Run('luz dos tiles de mod', () => compute(x0 - pad, x1 + pad, y0 - pad, y1 + pad)) || [];
            }
            return original(x0, x1, y0, y1);
        }, { ifBusy: 'original' });

        Terraria.Main['void DoUpdateInWorld()'].hook((original, self) => {
            original(self);
            if (lights.length && !Terraria.Main.gameMenu) bl.tiles.addLights(lights);
        }, { ifBusy: 'original' });
    }

    // Um quadro por tipo (Main.tileFrame); o desenho soma frame * AnimationFrameHeight.
    static HookAnimation() {
        // Os mesmos dois Ref a cada bloco e a cada quadro, e sem closure: dois
        // Ref novos e um Safe.Run por tipo animado custavam ~3 vezes mais.
        const frame = new Ref(0), counter = new Ref(0);
        Terraria.Main['void AnimateTiles()'].hook((original) => {
            original();
            if (!TileLoader.Animated.length) return;

            const Main = Terraria.Main;
            const frames = Main.tileFrame, counters = Main.tileFrameCounter;
            const animated = TileLoader.Animated;
            // A cada quadro: o rótulo montado uma vez, e só o que mudou volta pela ponte.
            for (let k = 0; k < animated.length; k++) {
                const m = animated[k];
                const t = m.Type;
                const f0 = frames[t], c0 = counters[t];
                frame.value = f0;
                counter.value = c0;
                try {
                    m.AnimateTile(frame, counter);
                } catch (e) {
                    Safe.Report(m.__animateLabel || (m.__animateLabel = m.constructor.name + '.AnimateTile'), e);
                }
                const f1 = frame.value | 0, c1 = counter.value | 0;
                if (f1 !== f0) frames[t] = f1;
                if (c1 !== c0) counters[t] = c1;
            }
        }, { ifBusy: 'original' });
    }

    // Chamado pelo desenho nativo (bl.tiles.onDrawData), célula a célula.
    // Com PreDraw, o jogo não desenha a célula: a passada desenha.
    static #HookDrawData() {
        const refOf = (d, key) => ({
            get value() { return d[key]; },
            set value(v) { d[key] = v | 0; },
        });

        bl.tiles.onDrawData((x, y, type, d) => {
            const m = TileLoader.ByType.get(type);
            if (!m) return;

            if (TileLoader.Overrides(m, 'PreDraw')) {
                d.width = 0;
                d.height = 0;
                return;
            }
            TileDrawLoader.ApplyDrawData(m, x, y, refOf(d, 'width'), refOf(d, 'top'), refOf(d, 'height'),
                refOf(d, 'frameX'), refOf(d, 'frameY'), refOf(d, 'addFrX'), refOf(d, 'addFrY'), refOf(d, 'effects'));
        });
    }

    static ApplyDrawData(m, x, y, width, top, height, frameX, frameY, addFrX, addFrY, effects) {
        const name = m.constructor.name;
        if (TileLoader.Overrides(m, 'SetDrawPositions')) {
            Safe.Run(name + '.SetDrawPositions', () => m.SetDrawPositions(x, y, width, top, height, frameX, frameY));
        }
        if (TileLoader.Overrides(m, 'AnimateIndividualTile')) {
            Safe.Run(name + '.AnimateIndividualTile', () => m.AnimateIndividualTile(m.Type, x, y, addFrX, addFrY));
        }
        if (TileLoader.Overrides(m, 'SetSpriteEffects')) {
            Safe.Run(name + '.SetSpriteEffects', () => m.SetSpriteEffects(x, y, effects));
        }
    }

    // A passada: depois dos tiles, a cada quadro, para os tipos marcados na tela.
    static HookDrawPass() {
        const TilesRenderer = Terraria.GameContent.Drawing.TileDrawing;
        const postDraw = TilesRenderer['void PostDrawTiles(bool solidLayer, bool forRenderTargets, bool intoRenderTargets)'];

        postDraw.hook((original, self, solidLayer, forRenderTargets, intoRenderTargets) => {
            original(self, solidLayer, forRenderTargets, intoRenderTargets);
            if (solidLayer || intoRenderTargets || Terraria.Main.gameMenu) return;

            Safe.Run('desenho dos tiles de mod', () => TileDrawLoader.#DrawPass());
        });

        // O DrawEffects do mod pede o SpecialDraw por aqui, como no tModLoader.
        // Fora da passada (o próprio PostDrawTiles do jogo), segue o do jogo.
        Safe.Run('AddSpecialLegacyPoint', () => TilesRenderer['void AddSpecialLegacyPoint(int x, int y)'].hook((original, self, x, y) => {
            const list = TileDrawLoader.#special;
            if (!list) return original(self, x, y);

            list.push(x, y);
            return undefined;
        }, { whileIn: postDraw }));

        Safe.Run('AddSpecialPoint', () => TilesRenderer.AddSpecialPoint.hook((original, self, x, y, kind) => {
            const list = TileDrawLoader.#special;
            if (!list) return original(self, x, y, kind);

            list.push(x, y);
            return undefined;
        }, { whileIn: postDraw }));
    }

    static #DrawPass() {
        const Main = Terraria.Main;
        const sp = Main.screenPosition;
        const x0 = Math.floor(sp.X / 16) - 2, y0 = Math.floor(sp.Y / 16) - 2;
        const x1 = x0 + Math.ceil(Main.screenWidth / 16) + 4, y1 = y0 + Math.ceil(Main.screenHeight / 16) + 4;

        const found = bl.tiles.find('tile.draw', x0, y0, x1, y1, true);
        if (!found.length) return;

        // O lote só começa se algum tile desenha (as partículas não precisam).
        const sb = Main.spriteBatch;
        let began = false, opened = false;
        const open = () => {
            if (opened) return;
            opened = true;
            began = TileDrawLoader.#Begin(sb);
        };
        const toScreen = Main.drawToScreen;
        Main.drawToScreen = true;   // o mod soma o offScreenRange só quando desenha no alvo dos tiles
        const special = TileDrawLoader.#special = [];

        try {
            for (let k = 0; k < found.length; k += 4) {
                TileDrawLoader.#DrawTile(found[k], found[k + 1], found[k + 2], found[k + 3], sb, open);
            }
            for (let k = 0; k < special.length; k += 2) {
                const i = special[k], j = special[k + 1];
                const m = TileLoader.At(i, j);
                if (!m) continue;

                open();
                Safe.Run(m.constructor.name + '.SpecialDraw', () => m.SpecialDraw(i, j, sb));
            }
        } finally {
            TileDrawLoader.#special = null;
            Main.drawToScreen = toScreen;
            if (began) sb.End();
        }
    }

    // O Tile e a luz da célula custam uma ida à ponte cada: o EmitParticles
    // só os recebe se os declara (a tocha que só olha o frameX não paga).
    static #DrawTile(i, j, frameX, frameY, sb, open) {
        const m = TileLoader.At(i, j);
        if (!m) return;

        const name = m.constructor.name;
        let tile = null, light = null;
        const getTile = () => tile || (tile = TileLoader.Tile(i, j));
        const getLight = () => light || (light = Terraria.Lighting['Color GetColor(int x, int y)'](i, j));

        if (TileLoader.Overrides(m, 'PreDraw')) {
            open();
            const draw = Safe.Run(name + '.PreDraw', () => m.PreDraw(i, j, sb));
            if (draw !== false) TileDrawLoader.#DrawDefault(m, i, j, getTile(), getLight(), sb);
        }
        if (TileLoader.Overrides(m, 'DrawEffects')) {
            open();
            const drawData = { tileFrameX: frameX, tileFrameY: frameY, typeCache: m.Type, tileLight: getLight(), tileCache: getTile() };
            Safe.Run(name + '.DrawEffects', () => m.DrawEffects(i, j, sb, drawData));
        }
        if (TileLoader.Overrides(m, 'EmitParticles')) {
            const wants = m.EmitParticles.length;
            const t = wants >= 3 ? getTile() : null;
            const l = wants >= 6 ? getLight() : null;
            Safe.Run(name + '.EmitParticles', () => m.EmitParticles(i, j, t, frameX, frameY, l, true));
        }
        if (TileLoader.Overrides(m, 'PostDraw')) {
            open();
            Safe.Run(name + '.PostDraw', () => m.PostDraw(i, j, sb));
        }
    }

    // O que o jogo desenharia, para o PreDraw que devolve true.
    static #DrawDefault(m, i, j, tile, light, sb) {
        const Main = Terraria.Main;
        const box = (v) => new Ref(v);
        const width = box(16), top = box(0), height = box(16);
        const frameX = box(tile.frameX), frameY = box(tile.frameY);
        const addFrX = box(0), addFrY = box(m.AnimationFrameHeight > 0 ? Main.tileFrame[m.Type] * m.AnimationFrameHeight : 0);
        const effects = box(0);

        const data = TileLoader.HasObjectData(m.Type) ? TileLoader.ObjectData(m.Type) : null;
        if (data) {
            width.value = data.CoordinateWidth;
            top.value = data.DrawYOffset;
        }
        TileDrawLoader.ApplyDrawData(m, i, j, width, top, height, frameX, frameY, addFrX, addFrY, effects);

        const texture = Terraria.GameContent.TextureAssets.Tile[m.Type].Value;
        const sp = Main.screenPosition;
        const at = Vector2.new(i * 16 - Math.floor(sp.X) - (width.value - 16) / 2, j * 16 - Math.floor(sp.Y) + top.value);
        const source = Rectangle.new(frameX.value + addFrX.value, frameY.value + addFrY.value, width.value, height.value);
        sb['void Draw(Texture2D texture, Vector2 position, Nullable`1 sourceRectangle, Color color, float rotation, Vector2 origin, float scale, SpriteEffects effects, float layerDepth)'](
            texture, at, source, light, 0, Vector2.new(0, 0), 1, effects.value, 0);
    }

    // Começa o lote como o PostDrawTiles do jogo. false = já estava começado.
    static #Begin(sb) {
        if ('_beginCalled' in sb && sb._beginCalled) return false;

        const G = Microsoft.Xna.Framework.Graphics;
        const Main = Terraria.Main;
        sb['void Begin(SpriteSortMode sortMode, BlendState blendState, SamplerState samplerState, DepthStencilState depthStencilState, RasterizerState rasterizerState, Effect effect, Nullable<Matrix> transformMatrix, bool defferedBatch)'](
            0, G.BlendState.AlphaBlend, Main.DefaultSamplerState, G.DepthStencilState.None, Main.Rasterizer, null, Main.Transform, true);
        return true;
    }
}
