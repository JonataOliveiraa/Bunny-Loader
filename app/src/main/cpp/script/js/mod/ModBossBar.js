// Uma barra de vida de chefe própria, como a ModBossBar do tModLoader. O NPC
// de mod a pede no SetDefaults: `this.BossBar = ModContent.GetInstance(MinhaBarra)`.
// Com ela, o NPC ganha a barra grande mesmo sem ser chefe e sem cabeça de
// chefe. O desenho é o das barras do jogo no celular (GUIBossBar.Draw:
// painéis embaixo do minimapa, vida, escudo e ícone), com o BossBarDrawParams
// do PreDraw (cores da faixa e do ícone, texto). Ver BossBarLoader.
class ModBossBar {
    #life = 0;
    #lifeMax = 0;
    #shield = 0;
    #shieldMax = 0;

    // Os valores do último quadro desenhado.
    get Life() { return this.#life; }
    get LifeMax() { return this.#lifeMax; }
    get Shield() { return this.#shield; }
    get ShieldMax() { return this.#shieldMax; }

    SetStaticDefaults() {}

    // O ícone (Asset<Texture2D> ou Texture2D); iconFrame: Ref de um Rectangle
    // (null = a textura inteira). null: o "?" do jogo.
    GetIconTexture(iconFrame) { return null; }

    // Quem decide se a barra aparece. info.npcIndexToAimAt é o NPC (mude para
    // mirar outra parte de um verme); life, lifeMax, shield e shieldMax são Ref.
    // null: a vida do NPC; true: desenha com os valores dados; false: não desenha.
    ModifyInfo(info, life, lifeMax, shield, shieldMax) { return null; }

    // drawParams: o BossBarDrawParams (ver GlobalBossBar), que vale para o
    // desenho. false: sem a barra do jogo.
    PreDraw(spriteBatch, npc, drawParams) { return true; }
    PostDraw(spriteBatch, npc, drawParams) {}

    // Do BossBarLoader, a cada quadro com a barra.
    Collected(life, lifeMax, shield, shieldMax) {
        this.#life = life;
        this.#lifeMax = lifeMax;
        this.#shield = shield;
        this.#shieldMax = shieldMax;
    }

    static register(cls) {
        if (typeof cls !== 'function' || !(cls.prototype instanceof ModBossBar)) {
            throw new TypeError('ModBossBar.register(Class): pass the class, which extends ModBossBar');
        }

        const inst = new cls();
        Templates.Adopt(cls, inst);
        BossBarLoader.Add(inst);
        Safe.Run(cls.name + '.SetStaticDefaults', () => inst.SetStaticDefaults());
        return inst;
    }
}
