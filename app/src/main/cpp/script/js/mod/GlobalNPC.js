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

    // O spawn natural (só no jogo sozinho ou no servidor), como no tModLoader.
    // spawnRate e maxSpawns são Ref (.value): menor spawnRate = spawn mais
    // frequente; maxSpawns = quantos inimigos perto do jogador.
    EditSpawnRate(player, spawnRate, maxSpawns) {}
    // Ref (.value), em blocos: até onde nasce (spawnRange) e o quanto longe
    // do jogador precisa ser (safeRange).
    EditSpawnRange(player, spawnRangeX, spawnRangeY, safeRangeX, safeRangeY) {}
    // Com o ponto do spawn escolhido, antes do sorteio: muda os campos do
    // spawnInfo (waterTile, nearGranite...) que o jogo e o SpawnChance leem.
    EditSpawnInfo(spawnInfo) {}
    // O sorteio: pool[tipo] = peso (0 = o spawn do jogo, peso 1). Ver SpawnPool.
    EditSpawnPool(pool, spawnInfo) {}
    // Depois de nascer um NPC sorteado que não é o do jogo (o índice em Main.npc).
    SpawnNPC(npc, tileX, tileY) {}

    static register(cls) {
        const inst = globalNPCs.Register(cls, 'GlobalNPC');
        GlobalNPCLoader.Hook(cls);
        return inst;
    }
}
