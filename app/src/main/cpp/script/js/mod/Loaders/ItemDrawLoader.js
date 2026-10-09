// Mesmas fronteiras e ordem do ItemLoader do tModLoader 1.4.5.
// Os stages nativos preservam o cálculo e os efeitos especiais do jogo.
class ItemDrawLoader {
    static #all = new Set();
    static #world = null;
    static #inventory = null;

    static Want(cls, type = -1) {
        const Base = type < 0 ? GlobalItem : ModItem;
        for (const kind of ['World', 'Inventory']) {
            const key = 'item.Draw' + kind;
            if (type >= 0 && ItemDrawLoader.#all.has(kind)) bl.hookMarks.set(key, type);
            if (!['PreDrawIn', 'PostDrawIn'].some(prefix => Hooks.Overrides(cls, Base, prefix + kind))) continue;
            if (type < 0) {
                ItemDrawLoader.#all.add(kind);
                for (let t = 0; t < FIRST_ITEM; t++) bl.hookMarks.set(key, t);
                for (const t of ItemLoader.ByType.keys()) bl.hookMarks.set(key, t);
            } else bl.hookMarks.set(key, type);
            Hooks.Once(key, () => kind === 'World' ? ItemDrawLoader.#World() : ItemDrawLoader.#Inventory());
        }
    }

    static #Own(item, name, args) {
        const m = ItemLoader.Of(item);
        if (m && Hooks.Overrides(m.constructor, ModItem, name))
            return Safe.Run(m.constructor.name + '.' + name, () => m[name](...args));
    }

    static #Globals(item, name, args) {
        let go = true;
        globalItems.Each(item, name, g => { if (g[name](...args) === false) go = false; });
        return go;
    }

    static #World() {
        const key = 'item.DrawWorld';
        bl.installItemDrawHooks('World', (light, alpha, rotation, scale) => {
            const state = ItemDrawLoader.#world;
            if (!state) return [true, rotation, scale];
            const r = new Ref(rotation), s = new Ref(scale);
            const args = [state.world, Terraria.Main.spriteBatch, light, alpha, r, s, state.index];
            let go = ItemDrawLoader.#Own(state.world.inner, 'PreDrawInWorld', args) !== false;
            go = ItemDrawLoader.#Globals(state.world.inner, 'PreDrawInWorld', args) && go;
            state.args = [args[0], args[1], light, alpha, r.value, s.value, state.index];
            return [go, r.value, s.value];
        }, () => {
            const state = ItemDrawLoader.#world;
            if (!state || !state.args) return;
            ItemDrawLoader.#Own(state.world.inner, 'PostDrawInWorld', state.args);
            ItemDrawLoader.#Globals(state.world.inner, 'PostDrawInWorld', state.args);
        });
        Terraria.Main['void DrawItem(WorldItem item, int whoami)'].hook((original, self, world, index) => {
            const outer = ItemDrawLoader.#world;
            ItemDrawLoader.#world = { world, index };
            bl.hookFlags.set(key, true);
            try { return original(self, world, index); }
            finally {
                ItemDrawLoader.#world = outer;
                bl.hookFlags.set(key, !!outer);
            }
        }, { minType: 0, on: 0, field: 'inner.type', marks: key });
    }

    static #Inventory() {
        const key = 'item.DrawInventory';
        bl.installItemDrawHooks('Inventory', (item, batch, position, frame, light, environment, origin, scale) => {
            const state = ItemDrawLoader.#inventory;
            if (!state) return true;
            const args = [item, batch, position, frame, item.GetAlpha(light), item.GetColor(environment), origin, scale];
            state.args = args;
            state.light = light;
            state.environment = environment;
            let go = ItemDrawLoader.#Globals(item, 'PreDrawInInventory', args);
            go = ItemDrawLoader.#Own(item, 'PreDrawInInventory', args) !== false && go;
            return go;
        }, () => {
            const state = ItemDrawLoader.#inventory;
            if (!state || !state.args) return;
            const args = state.args, item = args[0];
            args[4] = item.GetAlpha(state.light);
            args[5] = item.GetColor(state.environment);
            ItemDrawLoader.#Own(item, 'PostDrawInInventory', args);
            ItemDrawLoader.#Globals(item, 'PostDrawInInventory', args);
        });
        Terraria.UI.ItemSlot['float DrawItemIcon(Item item, int context, SpriteBatch spriteBatch, Vector2 screenPositionForItemCenter, float scale, float sizeLimit, Color environmentColor, float itemFade, bool flip)'].hook(
            (original, item, context, batch, position, scale, limit, environment, fade, flip) => {
                const outer = ItemDrawLoader.#inventory;
                ItemDrawLoader.#inventory = {};
                bl.hookFlags.set(key, true);
                try { return original(item, context, batch, position, scale, limit, environment, fade, flip); }
                finally {
                    ItemDrawLoader.#inventory = outer;
                    bl.hookFlags.set(key, !!outer);
                }
            }, { minType: 0, on: 0, marks: key });
    }
}
