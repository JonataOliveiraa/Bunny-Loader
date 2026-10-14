// Código de mod para TODO buff (os do jogo e os de mod), como o GlobalBuff do
// tModLoader. Os métodos recebem o tipo do buff; o Update do tModLoader vem
// em dois, como no ModBuff: UpdatePlayer e UpdateNPC.
class GlobalBuff {
    SetStaticDefaults() {}
    PostSetupContent() {}

    // buffIndex é Ref: quem tira o buff da lista diminui 1 dele.
    UpdatePlayer(type, player, buffIndex) {}
    UpdateNPC(type, npc, buffIndex) {}

    // O buff que já estava ganhou outro AddBuff. true: o jogo não renova o
    // tempo (como no tModLoader; o ReApply do ModBuff é ao contrário).
    ReApplyPlayer(type, player, time, buffIndex) { return false; }
    ReApplyNPC(type, npc, time, buffIndex) { return false; }

    // O nome e a dica do buff tocado (buffName, tip e rare são Ref; o rare
    // não muda a cor no celular).
    ModifyBuffText(type, buffName, tip, rare) {}

    // O ícone na barra de buffs. drawParams: { Texture, Position, TextPosition,
    // SourceRectangle, MouseRectangle, DrawColor }; o que mudar vale para o
    // desenho. false: sem o ícone (o PostDraw roda mesmo assim).
    PreDraw(spriteBatch, type, buffIndex, drawParams) { return true; }
    PostDraw(spriteBatch, type, buffIndex, drawParams) {}

    // O toque que cancela o buff. false: o buff fica.
    RightClick(type, buffIndex) { return true; }

    static register(cls) {
        const inst = globalBuffs.Register(cls);
        BuffLoader.HookGlobal(cls);
        return inst;
    }
}
