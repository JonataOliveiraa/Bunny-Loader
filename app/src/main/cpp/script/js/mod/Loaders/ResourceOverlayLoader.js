// Os ModResourceOverlay. O GUIHearts.HeartDraw e o GUIMana.StarDraw desenham
// cada coração e estrela com o SpriteBatch.Draw; durante eles (só eles: a
// marca 'resource.overlay' liga os hooks do Draw) cada desenho vira um
// ResourceOverlayDrawContext para os mods.
class ResourceOverlayLoader {
    static List = [];
    static #FLAG = 'resource.overlay';
    static #DRAW_FULL = 'void Draw(Texture2D texture, Vector2 position, Nullable`1 sourceRectangle, Color color, float rotation, Vector2 origin, float scale, SpriteEffects effects, float layerDepth)';
    static #DRAW_REF = 'void Draw(Texture2D texture, ref Vector2 position, ref Color color, float rotation, Vector2 origin, float scale)';
    static #SETS = ['Classic', 'Fancy', 'HorizontalBars'];
    static #current = null;          // { life, index, set }
    static #assets = new Map();      // endereço da Texture2D -> Asset (ou null)
    static #snapshot = null;
    static #snapshotFrame = -1;
    static #drawText = true;
    static #lastSet = { Index: 0, Name: 'Classic' };   // o da vida vale para a mana (desenhada depois)

    static Add(inst, cls) {
        ResourceOverlayLoader.List.push(inst);
        const has = (name) => Hooks.Overrides(cls, ModResourceOverlay, name);
        if (has('PreDrawResource') || has('PostDrawResource')) Hooks.Once('resource.overlay', ResourceOverlayLoader.#InstallResources);
        if (has('PreDrawResourceDisplay') || has('PostDrawResourceDisplay')) Hooks.Once('resource.display', ResourceOverlayLoader.#InstallDisplay);
    }

    static Snapshot() {
        const Main = Terraria.Main;
        const frame = Main.GameUpdateCount;
        if (ResourceOverlayLoader.#snapshotFrame !== frame || !ResourceOverlayLoader.#snapshot) {
            ResourceOverlayLoader.#snapshot = new PlayerStatsSnapshot(Main.player[Main.myPlayer]);
            ResourceOverlayLoader.#snapshotFrame = frame;
        }
        return ResourceOverlayLoader.#snapshot;
    }

    static #DisplaySet(hearts) {
        const index = Number(hearts.ActiveSet) | 0;
        const set = ResourceOverlayLoader.#lastSet;
        if (set.Index !== index) ResourceOverlayLoader.#lastSet = { Index: index, Name: ResourceOverlayLoader.#SETS[index] || 'Classic' };
        return ResourceOverlayLoader.#lastSet;
    }

    static #AssetOf(texture) {
        const key = bl.addressOf(texture);
        if (!ResourceOverlayLoader.#assets.has(key)) ResourceOverlayLoader.#assets.set(key, bl.assetOfTexture(texture));
        return ResourceOverlayLoader.#assets.get(key) || texture;
    }

    static #Around(life, displaySetOf) {
        return (original, self, layout, index, position, scale) => {
            const flags = bl.hookFlags;
            ResourceOverlayLoader.#current = { life, index, set: displaySetOf(self) };
            flags.set(ResourceOverlayLoader.#FLAG, true);
            try {
                return original(self, layout, index, position, scale);
            } finally {
                flags.set(ResourceOverlayLoader.#FLAG, false);
                ResourceOverlayLoader.#current = null;
            }
        };
    }

    static #InstallResources() {
        const hearts = bl.classOf('', 'GUIHearts');
        const mana = bl.classOf('', 'GUIMana');
        const heartsSet = (self) => ResourceOverlayLoader.#DisplaySet(self);
        hearts['void HeartDraw(ItemGrid_Layout gridLayout, int index, Vector2 position, float scale)'].hook(ResourceOverlayLoader.#Around(true, heartsSet));
        mana['void StarDraw(ItemGrid_Layout gridLayout, int index, Vector2 position, float scale)'].hook(
            ResourceOverlayLoader.#Around(false, () => ResourceOverlayLoader.#lastSet));

        const SB = Microsoft.Xna.Framework.Graphics.SpriteBatch;
        SB[ResourceOverlayLoader.#DRAW_FULL].hook((original, sb, texture, position, source, color, rotation, origin, scale, effects, depth) => {
            const cur = ResourceOverlayLoader.#current;
            if (!cur || ResourceOverlayDrawContext.Drawing) return original();
            return ResourceOverlayLoader.#Resource(cur, sb, texture, position, source, color, rotation, origin, scale, effects, original);
        }, { flag: ResourceOverlayLoader.#FLAG });
        SB[ResourceOverlayLoader.#DRAW_REF].hook((original, sb, texture, position, color, rotation, origin, scale) => {
            const cur = ResourceOverlayLoader.#current;
            if (!cur || ResourceOverlayDrawContext.Drawing) return original();
            const pos = position instanceof Ref ? position.value : position;
            const col = color instanceof Ref ? color.value : color;
            return ResourceOverlayLoader.#Resource(cur, sb, texture, pos, null, col, rotation, origin, scale, 0, original);
        }, { flag: ResourceOverlayLoader.#FLAG });
    }

    // Pre de todos; o desenho do jogo (ou o do contexto, se um mod mexeu nele); Post de todos.
    static #Resource(cur, sb, texture, position, source, color, rotation, origin, scale, effects, original) {
        const ctx = new ResourceOverlayDrawContext();
        ctx.snapshot = ResourceOverlayLoader.Snapshot();
        ctx.DisplaySet = cur.set;
        ctx.resourceNumber = cur.index;
        ctx.drawingLife = cur.life;
        ctx.texture = ResourceOverlayLoader.#AssetOf(texture);
        ctx.position = { X: position.X, Y: position.Y };
        ctx.source = source;
        ctx.color = color;
        ctx.rotation = rotation;
        ctx.origin = origin;
        ctx.scale = scale;
        ctx.effects = effects;
        ctx.SpriteBatch = sb;
        const before = [ctx.texture, ctx.position.X, ctx.position.Y, ctx.source, ctx.color, ctx.rotation, ctx.scale];

        let draw = true;
        for (const m of ResourceOverlayLoader.List) {
            try {
                if (m.PreDrawResource(ctx) === false) draw = false;
            } catch (e) {
                Safe.Report(m.constructor.name + '.PreDrawResource', e);
            }
        }
        if (draw) {
            const after = [ctx.texture, ctx.position.X, ctx.position.Y, ctx.source, ctx.color, ctx.rotation, ctx.scale];
            if (after.every((v, i) => v === before[i])) original();
            else ctx.Draw();
        }
        for (const m of ResourceOverlayLoader.List) {
            try {
                m.PostDrawResource(ctx);
            } catch (e) {
                Safe.Report(m.constructor.name + '.PostDrawResource', e);
            }
        }
        return undefined;
    }

    static #InstallDisplay() {
        const hearts = bl.classOf('', 'GUIHearts');
        const mana = bl.classOf('', 'GUIMana');
        const display = (life) => (original, self) => {
            const snapshot = ResourceOverlayLoader.Snapshot();
            const set = life ? ResourceOverlayLoader.#DisplaySet(self) : ResourceOverlayLoader.#lastSet;
            const textColor = new Ref(Color.White);
            const drawText = new Ref(true);
            let go = true;
            for (const m of ResourceOverlayLoader.List) {
                try {
                    if (m.PreDrawResourceDisplay(snapshot, set, life, textColor, drawText) === false) go = false;
                } catch (e) {
                    Safe.Report(m.constructor.name + '.PreDrawResourceDisplay', e);
                }
            }
            ResourceOverlayLoader.#drawText = drawText.value !== false;
            if (go) original(self);
            ResourceOverlayLoader.#drawText = true;
            for (const m of ResourceOverlayLoader.List) {
                try {
                    m.PostDrawResourceDisplay(snapshot, set, life, textColor.value, drawText.value);
                } catch (e) {
                    Safe.Report(m.constructor.name + '.PostDrawResourceDisplay', e);
                }
            }
        };
        hearts['void Draw()'].hook(display(true));
        mana['void Draw()'].hook(display(false));
        hearts['void DrawLifeString()'].hook((original, self) => {
            if (ResourceOverlayLoader.#drawText) original(self);
        });
    }
}
