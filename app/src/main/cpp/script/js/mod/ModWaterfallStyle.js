// Um estilo de cachoeira de mod, como o ModWaterfallStyle do tModLoader: a
// textura da classe (Content/Biomes/X.js -> Assets/Textures/Biomes/X.png).
// Slot: o número, depois dos 28 do jogo. Um ModWaterStyle o escolhe em
// ChooseWaterfallStyle.
class ModWaterfallStyle {
    // Luz em cada ponto de cachoeira deste estilo na tela (Lighting.AddLight).
    // O celular embute a luz das cachoeiras do jogo no desenho; esta roda
    // depois dele, uma vez por quadro.
    AddLight(i, j) {}

    // A cor da cachoeira: r, g, b são Ref (.value, 0 a 255, a luz do lugar
    // vezes a opacidade) e a, a opacidade. Para cores que mudam com o tempo.
    ColorMultiplier(r, g, b, a) {}

    SetStaticDefaults() {}

    static register(cls) {
        if (typeof cls !== 'function' || !(cls.prototype instanceof ModWaterfallStyle)) {
            throw new TypeError('ModWaterfallStyle.register(Classe): passe a classe, que estende ModWaterfallStyle');
        }

        const inst = new cls();
        Templates.Adopt(cls, inst);
        inst.Texture = ModFiles.ContentTexture(inst, cls);
        WaterfallStyleLoader.Add(inst);
        Safe.Run(cls.name + '.SetStaticDefaults', () => inst.SetStaticDefaults());
        return inst;
    }
}
