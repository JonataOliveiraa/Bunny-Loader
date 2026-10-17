// As poeiras de mod (content/dusts/ModDusts.cpp): o nativo acha as poeiras de
// mod uma vez por quadro e chama o JS em lote, só para o que a classe
// sobrescreveu. O resto (mover, iluminar, desenhar) é o código do jogo.
class DustLoader {
    static ByType = new Map();
    static #installed = false;

    static #ON_SPAWN = 1;
    static #UPDATE = 2;
    static #MID_UPDATE = 4;
    static #GET_ALPHA = 8;
    static #PRE_DRAW = 16;

    static #BEGIN = 'void Begin(SpriteSortMode sortMode, BlendState blendState, SamplerState samplerState, DepthStencilState depthStencilState, RasterizerState rasterizerState, Effect effect, Nullable<Matrix> transformMatrix, bool defferedBatch)';

    static GetDust(type) {
        return DustLoader.ByType.get(type) ?? null;
    }

    static HookBits(cls) {
        const has = (name) => Hooks.Overrides(cls, ModDust, name);
        return (has('OnSpawn') ? DustLoader.#ON_SPAWN : 0)
            | (has('Update') ? DustLoader.#UPDATE : 0)
            | (has('MidUpdate') ? DustLoader.#MID_UPDATE : 0)
            | (has('GetAlpha') ? DustLoader.#GET_ALPHA : 0)
            | (has('PreDraw') ? DustLoader.#PRE_DRAW : 0);
    }

    // '' = a folha do jogo: `get Texture() { return null; }` ou `Texture = null`.
    static TexturePath(inst, cls) {
        for (let p = cls.prototype; p && p !== ModDust.prototype; p = Object.getPrototypeOf(p)) {
            const d = Object.getOwnPropertyDescriptor(p, 'Texture');
            if (d && d.get && !Safe.Run(cls.name + '.Texture', () => d.get.call(inst))) return '';
        }
        if (inst.Texture === null || inst.Texture === '') return '';
        return ModFiles.Texture(ModFiles.ContentTexture(inst, cls));
    }

    // UpdateType vira acessor: mudar no SetStaticDefaults (ou depois) chega ao nativo.
    static Adopt(inst, type) {
        let updateType = Number(inst.UpdateType) | 0;
        Object.defineProperty(inst, 'UpdateType', {
            get: () => updateType,
            set: (value) => {
                updateType = Number(value) | 0;
                bl.dusts.setUpdateType(type, updateType);
            },
            enumerable: true,
        });
        if (updateType >= 0) bl.dusts.setUpdateType(type, updateType);

        DustLoader.ByType.set(type, inst);
        DustLoader.#Install();
    }

    static #Install() {
        if (DustLoader.#installed) return;
        DustLoader.#installed = true;
        bl.dusts.setCallbacks({
            spawn: DustLoader.#Spawn,
            update: DustLoader.#Update,
            midUpdate: DustLoader.#MidUpdate,
            preDraw: DustLoader.#PreDraw,
            getAlpha: DustLoader.#GetAlpha,
        });
    }

    static #Spawn(type, dust) {
        const m = DustLoader.ByType.get(type);
        if (m) Safe.Run(m.constructor.name + '.OnSpawn', () => m.OnSpawn(dust));
    }

    // Um laço só, com try por poeira: um erro não leva as outras. O tipo vem
    // do nativo (ler dust.type pela ponte custava mais que o próprio Update).
    static #Each(dusts, types, method, fallback) {
        const out = new Array(dusts.length);
        for (let i = 0; i < dusts.length; i++) {
            const dust = dusts[i];
            const m = DustLoader.ByType.get(types[i]);
            if (!m) { out[i] = fallback; continue; }
            try {
                const r = m[method](dust);
                out[i] = typeof r === 'boolean' ? r : fallback;
            } catch (e) {
                Safe.Report(m.constructor.name + '.' + method, e);
                out[i] = fallback;
            }
        }
        return out;
    }

    static #Update(dusts, types) {
        return DustLoader.#Each(dusts, types, 'Update', true);
    }

    static #MidUpdate(dusts, types) {
        return DustLoader.#Each(dusts, types, 'MidUpdate', false);
    }

    // O mesmo lote do Main.DrawDust; o jogo desenha as poeiras logo depois.
    static #PreDraw(dusts, types) {
        const Main = Terraria.Main;
        const G = Microsoft.Xna.Framework.Graphics;
        const batch = Main.spriteBatch;
        batch[DustLoader.#BEGIN](0, G.BlendState.AlphaBlend, G.SamplerState.PointClamp, G.DepthStencilState.None,
            G.RasterizerState.CullNone, null, Main.Transform, true);
        try {
            return DustLoader.#Each(dusts, types, 'PreDraw', true);
        } finally {
            batch['void End()']();
        }
    }

    static #GetAlpha(dust, lightColor, type) {
        const m = DustLoader.ByType.get(type);
        if (!m) return null;
        try {
            return m.GetAlpha(dust, lightColor) ?? null;
        } catch (e) {
            Safe.Report(m.constructor.name + '.GetAlpha', e);
            return null;
        }
    }
}
