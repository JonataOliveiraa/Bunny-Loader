class GlobalNPC extends GlobalType {
    SetDefaults(npc) {}
    OnSpawn(npc, source) {}
    ResetEffects(npc) {}
    PreAI(npc) { return true; }
    AI(npc) {}
    PostAI(npc) {}
    HitEffect(npc, hitDirection, damage) {}
    // Golpes (como no ModNPC): null deixa o jogo decidir; modifiers é um
    // HitModifiers (no NPC) ou HurtModifiers (no jogador); hit, um HitInfo.
    CanBeHitByItem(npc, player, item) { return null; }
    ModifyHitByItem(npc, player, item, modifiers) {}
    OnHitByItem(npc, player, item, hit, damageDone) {}
    CanBeHitByProjectile(npc, projectile) { return null; }
    ModifyHitByProjectile(npc, projectile, modifiers) {}
    OnHitByProjectile(npc, projectile, hit, damageDone) {}
    ModifyIncomingHit(npc, modifiers) {}
    // cooldownSlot é Ref (.value).
    CanHitPlayer(npc, target, cooldownSlot) { return true; }
    ModifyHitPlayer(npc, target, modifiers) {}
    OnHitPlayer(npc, target, hurtInfo) {}
    CanHitNPC(npc, target) { return true; }
    CanBeHitByNPC(npc, attacker) { return true; }
    ModifyHitNPC(npc, target, modifiers) {}
    OnHitNPC(npc, target, hit) {}
    PreKill(npc) { return true; }
    OnKill(npc) {}
    // Depois do quadro do jogo (ou do ModNPC): npc.frame.Y = n * frameHeight.
    FindFrame(npc, frameHeight) {}
    // false: o NPC não some longe dos jogadores (CheckActive) / não morre com vida 0 (CheckDead).
    CheckActive(npc) { return true; }
    CheckDead(npc) { return true; }
    ApplyDifficultyAndPlayerScaling(npc, numPlayers, balance, bossAdjustment) {}

    // Conversa: null deixa o jogo decidir; false impede, true permite.
    CanChat(npc) { return null; }
    // chat é um Ref com a fala do jogo: troque o .value.
    GetChat(npc, chat) {}
    ModifyNPCHappiness(npc, player, primaryPlayerBiome, shopHelper, nearbyNPCsByType) {}

    // Lojas. ModifyShop: uma vez por loja (NPCShop; shop.NpcType, shop.Name),
    // na primeira vez que ela abre; o que o Add acrescenta entra depois dos
    // itens dela. SetupShop (o formato antigo do tModLoader): type é o NPC, shop
    // tem o array item e nextSlot é um Ref com a primeira casa livre:
    //   shop.item[nextSlot.value].SetDefaults(ItemID.Torch); nextSlot.value++;
    // ModifyActiveShop: os itens da loja aberta, a cada abertura.
    // SetupTravelShop: shop é o Main.travelShop (tipos) e nextSlot, um Ref.
    ModifyShop(shop) {}
    SetupShop(type, shop, nextSlot) {}
    ModifyActiveShop(npc, shopName, items) {}
    SetupTravelShop(shop, nextSlot) {}

    // Desenho: false no PreDraw não desenha; drawColor do DrawEffects é Ref.
    PreDraw(npc, spriteBatch, screenPos, drawColor) { return true; }
    PostDraw(npc, spriteBatch, screenPos, drawColor) {}
    DrawEffects(npc, drawColor) {}
    GetAlpha(npc, drawColor) { return null; }
    // index, rotation e spriteEffects são Ref (.value).
    BossHeadSlot(npc, index) {}
    BossHeadRotation(npc, rotation) {}
    BossHeadSpriteEffects(npc, spriteEffects) {}
    // Uma vez por tipo de NPC, com a amostra do jogo.
    ModifyNPCLoot(npc, npcLoot) {}
    ModifyGlobalLoot(globalLoot) {}
    NetSend(npc, writer) {}
    NetReceive(npc, reader) {}

    // O spawn natural (só no jogo sozinho ou no servidor), como no tModLoader.
    // Antes da taxa, da área e do ponto: os campos do spawnInfo do jogador
    // (noWorms, invaders, ZoneCorrupt...); o ponto ainda não existe (-1).
    EditSpawnFlags(spawnInfo) {}
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
