// O ModSystem.ModifyInterfaceLayers. A cada quadro o Main.DrawInterface
// percorre Main._gameInterfaceLayers e chama o Draw de cada camada (que acerta
// a escala e chama o DrawSelf). Aqui os mods mexem numa cópia JS da lista; a
// lista do jogo só é refeita quando a ordem final muda. Cada camada JS vira
// um GameInterfaceLayer do jogo (só a base, que nenhuma camada do jogo usa): o
// hook no DrawSelf da base chama a camada JS pelo nome.
class InterfaceLayerLoader {
    static #PREFIX = 'bl:';
    static #installed = false;
    static #nativeList = null;
    static #vanilla = [];          // { native, layer } na ordem do jogo
    static #applied = [];          // os nativos na lista do jogo agora
    static #byName = new Map();    // nome do nativo -> camada JS
    static #natives = new Map();   // camada JS -> nativo
    static #scales = new Map();    // camada JS -> ScaleType já posto no nativo
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
        InterfaceLayerLoader.#applied = vanilla.map((v) => v.native);
        InterfaceLayerLoader.#nativeList = list;
    }

    static #NativeOf(layer) {
        let native = InterfaceLayerLoader.#natives.get(layer);
        if (!native) {
            const name = InterfaceLayerLoader.#PREFIX + (InterfaceLayerLoader.#counter++) + ':' + layer.Name;
            native = Terraria.UI.GameInterfaceLayer.new();
            native['void .ctor(string name, InterfaceScaleType scaleType)'](name, layer.ScaleType);
            InterfaceLayerLoader.#natives.set(layer, native);
            InterfaceLayerLoader.#byName.set(name, layer);
            InterfaceLayerLoader.#scales.set(layer, layer.ScaleType);
        }
        if (InterfaceLayerLoader.#scales.get(layer) !== layer.ScaleType) {
            native.ScaleType = layer.ScaleType;
            InterfaceLayerLoader.#scales.set(layer, layer.ScaleType);
        }
        return native;
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

        const nativeOfVanilla = new Map(InterfaceLayerLoader.#vanilla.map((v) => [v.layer, v.native]));
        const final = [];
        for (const layer of layers) {
            if (!layer || layer.Active === false) continue;
            final.push(nativeOfVanilla.get(layer) || InterfaceLayerLoader.#NativeOf(layer));
        }
        const applied = InterfaceLayerLoader.#applied;
        if (final.length === applied.length && final.every((n, i) => n === applied[i])) return;

        list['void Clear()']();
        for (const n of final) list['void Add(GameInterfaceLayer item)'](n);
        InterfaceLayerLoader.#applied = final;
    }
}
