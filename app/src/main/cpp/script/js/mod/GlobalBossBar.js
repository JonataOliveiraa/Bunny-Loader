// O desenho de TODA barra de vida de chefe (as do jogo e as ModBossBar), como
// o GlobalBossBar do tModLoader. drawParams é o BossBarDrawParams:
// { BarTexture, BarCenter, BarColor, IconTexture, IconFrame, IconColor, Life,
// LifeMax, Shield, ShieldMax, IconScale, ShowText, TextOffset }. O que mudar
// nele vale para o desenho. No celular toda barra (do jogo e de mod) segue o
// layout dele (embaixo do minimapa): o BarCenter diz onde ela está, mas mudar
// não a move; IconScale e TextOffset não valem e o BarTexture é null.
class GlobalBossBar {
    SetStaticDefaults() {}
    PostSetupContent() {}

    // false: sem a barra (o PostDraw roda mesmo assim).
    PreDraw(spriteBatch, npc, drawParams) { return true; }
    PostDraw(spriteBatch, npc, drawParams) {}

    static register(cls) {
        const inst = globalBossBars.Register(cls);
        BossBarLoader.WantGlobals();
        return inst;
    }
}
