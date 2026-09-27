class GlobalNPC extends GlobalType {
    SetDefaults(npc) {}
    OnSpawn(npc, source) {}
    ResetEffects(npc) {}
    PreAI(npc) { return true; }
    AI(npc) {}
    PostAI(npc) {}
    HitEffect(npc, hitDirection, damage) {}
    OnHitByItem(npc, player, item, damageDone, knockBack, crit) {}
    OnHitByProjectile(npc, projectile) {}
    PreKill(npc) { return true; }
    OnKill(npc) {}
    // chat é um Ref com a fala do jogo: troque o .value.
    GetChat(npc, chat) {}
    // Uma vez por tipo de NPC, com a amostra do jogo.
    ModifyNPCLoot(npc, npcLoot) {}
    ModifyGlobalLoot(globalLoot) {}
    NetSend(npc, writer) {}
    NetReceive(npc, reader) {}

    static register(cls) {
        const inst = globalNPCs.Register(cls, 'GlobalNPC');
        GlobalNPCLoader.Hook(cls);
        return inst;
    }
}
