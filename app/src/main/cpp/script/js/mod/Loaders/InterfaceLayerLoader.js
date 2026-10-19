// O ModSystem.ModifyInterfaceLayers. A cada quadro o Main.DrawInterface
// percorre Main._gameInterfaceLayers e chama o Draw de cada camada (que acerta
// a escala e chama o DrawSelf). Aqui os mods mexem numa cópia JS da lista; a
// lista do jogo só é refeita quando a ordem final muda. Cada camada JS vira
// um GameInterfaceLayer do jogo (só a base, que nenhuma camada do jogo usa): o
// hook no DrawSelf da base chama a camada JS pelo nome.
//
// O mod cria a camada de novo a cada quadro (o padrão do tModLoader): o nativo
// é o do NOME (e da repetição dele no quadro), não o do objeto. Pelo objeto,
// eram dois GameInterfaceLayer novos por quadro presos para sempre, e a lista
// do jogo refeita a cada quadro.
class InterfaceLayerLoader {
    static #PREFIX = 'bl:';
    static #installed = false;
    static #nativeList = null;
    static #vanilla = [];          // { native, layer } na ordem do jogo
    static #nativeOfVanilla = new Map();   // camada JS do jogo -> nativo
    static #applied = [];          // os nativos na lista do jogo agora
    static #byName = new Map();    // nome do nativo -> camada JS deste quadro
    static #natives = new Map();   // nome da camada (e repetição) -> { native, scale }
    static #seen = new Map();      // nome da camada -> vezes neste quadro
    static #counter = 0;

    static Install() {
        if (InterfaceLayerLoader.#installed) return;
        InterfaceLayerLoader.#installed = true;
        const Main = Terraria.Main;
        Main['void DrawInterface(GameTime gameTime)'].hook((original, self, gameTime) => {
            Safe.Run('ModSystem.ModifyInterfaceLayers', () => InterfaceLayerLoader.#Apply(self));
            original(self, gameTime);
        });
        Terraria.UI.GameInterfaceLayer['bool DrawSelf()'].hook((original, self) => {
            const name = self.Name;
            const layer = name && name.startsWith(InterfaceLayerLoader.#PREFIX) ? InterfaceLayerLoader.#byName.get(name) : null;
            if (!layer) return original(self);
            return Safe.Run('interface layer ' + layer.Name, () => layer.DrawSelf()) !== false;
        });
    }

    static #Capture(list) {
        const count = list.Count;
        const vanilla = [];
        for (let i = 0; i < count; i++) {
            const native = list['GameInterfaceLayer get_Item(int index)'](i);
            const name = native.Name;
            if (name.startsWith(InterfaceLayerLoader.#PREFIX)) continue;
            const layer = new GameInterfaceLayer(name, native.ScaleType);
            vanilla.push({ native, layer });
        }
        InterfaceLayerLoader.#vanilla = vanilla;
        InterfaceLayerLoader.#nativeOfVanilla = new Map(vanilla.map((v) => [v.layer, v.native]));
        InterfaceLayerLoader.#applied = vanilla.map((v) => v.native);
        InterfaceLayerLoader.#nativeList = list;
    }

    static #NativeOf(layer) {
        const seen = InterfaceLayerLoader.#seen;
        const name = String(layer.Name);
        const repeat = seen.get(name) || 0;
        seen.set(name, repeat + 1);
        const key = repeat ? name + '#' + repeat : name;

        let entry = InterfaceLayerLoader.#natives.get(key);
        if (!entry) {
            const nativeName = InterfaceLayerLoader.#PREFIX + (InterfaceLayerLoader.#counter++) + ':' + name;
            const native = Terraria.UI.GameInterfaceLayer.new();
            native['void .ctor(string name, InterfaceScaleType scaleType)'](nativeName, layer.ScaleType);
            entry = { native, nativeName, scale: layer.ScaleType };
            InterfaceLayerLoader.#natives.set(key, entry);
        }
        if (entry.scale !== layer.ScaleType) {
            entry.native.ScaleType = layer.ScaleType;
            entry.scale = layer.ScaleType;
        }
        InterfaceLayerLoader.#byName.set(entry.nativeName, layer);
        return entry.native;
    }

    static #Apply(main) {
        // O jogo vai (re)montar a lista neste quadro: as camadas dos mods voltam no seguinte.
        if (main._needToSetupDrawInterfaceLayers) {
            InterfaceLayerLoader.#nativeList = null;
            return;
        }
        const list = main._gameInterfaceLayers;
        if (!list || list.Count === 0) return;
        if (!InterfaceLayerLoader.#nativeList || list.Count !== InterfaceLayerLoader.#applied.length) {
            InterfaceLayerLoader.#Capture(list);
        }

        const layers = new InterfaceLayerList();
        for (const v of InterfaceLayerLoader.#vanilla) {
            v.layer.Active = true;
            layers.push(v.layer);
        }
        SystemLoader.Call('ModifyInterfaceLayers', layers);

        const nativeOfVanilla = InterfaceLayerLoader.#nativeOfVanilla;
        InterfaceLayerLoader.#seen.clear();
        const final = [];
        for (const layer of layers) {
            if (!layer || layer.Active === false) continue;
            final.push(nativeOfVanilla.get(layer) || InterfaceLayerLoader.#NativeOf(layer));
        }
        const applied = InterfaceLayerLoader.#applied;
        let same = final.length === applied.length;
        for (let i = 0; same && i < final.length; i++) same = final[i] === applied[i];
        if (same) return;

        list['void Clear()']();
        for (const n of final) list['void Add(GameInterfaceLayer item)'](n);
        InterfaceLayerLoader.#applied = final;
    }
}
