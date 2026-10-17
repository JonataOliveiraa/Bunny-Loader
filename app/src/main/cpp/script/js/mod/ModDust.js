// Uma instância por tipo (como no tModLoader): a poeira em si é o Dust do
// jogo, que chega em cada método. `get Texture() { return null; }` (ou
// `Texture = null`) usa a folha de poeiras do jogo, com a moldura que o
// OnSpawn escolher (dust.HackFrame(DustID.X)).
class ModDust {
    Type = undefined;
    Texture = this.constructor.name;
    UpdateType = -1;

    // O Asset<Texture2D> da poeira (a folha do jogo sem textura própria).
    get Texture2D() { return bl.dusts.texture(this.Type); }

    SetStaticDefaults() {}
    // Recém-criada pelo Dust.NewDust, com a moldura já no topo da textura.
    OnSpawn(dust) {}
    // false: o jogo não atualiza a poeira neste quadro (o normal com Update próprio).
    Update(dust) { return true; }
    // Depois da atualização do jogo; true desfaz o freio horizontal (velocity.X * 0,99).
    MidUpdate(dust) { return false; }
    // A cor a desenhar no lugar da do jogo; null = a do jogo.
    GetAlpha(dust, lightColor) { return null; }
    // Antes do desenho de todas as poeiras de mod (com Main.spriteBatch aberto);
    // false: o jogo não desenha esta.
    PreDraw(dust) { return true; }

    static register(cls) {
        if (typeof cls !== 'function' || !(cls.prototype instanceof ModDust)) {
            throw new TypeError('ModDust.register(Class): pass the class, which extends ModDust');
        }

        const inst = new cls();
        const name = cls.name;
        Templates.Adopt(cls, inst);

        const texture = DustLoader.TexturePath(inst, cls);
        const type = bl.dusts.register({ name, texture, hooks: DustLoader.HookBits(cls) });
        inst.Type = type;
        DustLoader.Adopt(inst, type);

        Ready.Add(() => {
            Safe.Run(name + '.SetStaticDefaults', () => inst.SetStaticDefaults());
        });
        return type;
    }

    static isModType(type) { return bl.dusts.isModDust(type); }
    static getTypeByName(name) { return bl.dusts.typeOf(name); }
    static getModDust(type) { return DustLoader.GetDust(type); }
}
