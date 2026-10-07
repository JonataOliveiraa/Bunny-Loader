// Cada NPC de mod ganha a própria cópia do molde em npc.ModNPC.
class ModNPC {
    Type = undefined;
    HideFromModMenu = false;
    // Usa este tipo só durante a IA do jogo; configure o npc.aiStyle à parte.
    AIType = 0;
    // Anima como este NPC do jogo (0 = não anima). Pode vir do SetDefaults.
    AnimationType = 0;
    DisplayName = '';
    Texture = this.constructor.name;
    HideFromBestiary = false;
    // Padrão: a Texture + '_Head' / '_Shimmer_Head'. Morador sem cabeça nunca se muda.
    HeadTexture = '';
    ShimmerHeadTexture = '';
    // Padrão: a Texture + '_Head_Boss'. O ícone da barra de chefe e do mapa;
    // sem o arquivo, o chefe fica sem os dois.
    BossHeadTexture = '';
    // MusicLoader.GetMusicSlot(...) ou um MusicID do jogo; -1 = a do jogo.
    Music = -1;
    SceneEffectPriority = SceneEffectPriority.BossLow;

    get NPC() { return Entities.Of(this); }
    get Happiness() { return new NPCHappiness(this.Type); }

    Clone(newNPC) { return Entities.Clone(this); }

    SetStaticDefaults() {}
    SetDefaults(npc) {}
    PostStaticDefaults() {}
    PostSetDefaults(npc) {}
    PostSetupContent() {}
    ApplyBuffImmunity(npc) {}
    ModifyNPCLoot(npcLoot) {}
    SetBestiary(database, bestiaryEntry) {}
    HitEffect(npc, hitDirection, damage) {}
    OnSpawn(npc, source) {}
    ResetEffects(npc) {}
    OnHitByItem(npc, player, item, hit, damageDone) {}
    OnHitByProjectile(npc, projectile, hit, damageDone) {}
    CanHitPlayer(npc, player, cooldownSlot) { return true; }
    ModifyHitPlayer(npc, player, modifiers) {}
    OnHitPlayer(npc, player, hurtInfo) {}
    CanBeHitByItem(npc, player, item) { return null; }
    ModifyHitByItem(npc, player, item, modifiers) {}
    CanBeHitByProjectile(npc, projectile) { return null; }
    ModifyHitByProjectile(npc, projectile, modifiers) {}
    // NPC contra NPC (o inimigo que encosta no morador): false impede.
    CanHitNPC(npc, target) { return true; }
    CanBeHitByNPC(npc, attacker) { return true; }
    ModifyHitNPC(npc, target, modifiers) {}
    OnHitNPC(npc, target, hit) {}
    ModifyIncomingHit(npc, modifiers) {}
    CheckDead(npc) { return true; }
    ApplyDifficultyAndPlayerScaling(npc, numPlayers, balance, bossAdjustment) {}
    PreDraw(npc, spriteBatch, screenPos, drawColor) { return true; }
    PostDraw(npc, spriteBatch, screenPos, drawColor) {}
    DrawEffects(npc, drawColor) {}
    DrawBehind(npc, index) {}
    GetAlpha(npc, drawColor) { return null; }
    BossHeadSlot(npc, index) {}
    BossHeadRotation(npc, rotation) {}
    BossHeadSpriteEffects(npc, spriteEffects) {}

    PreAI(npc) { return true; }
    AI(npc) {}
    PostAI(npc) {}
    SendExtraAI(writer) {}
    ReceiveExtraAI(reader) {}
    FindFrame(npc, frameHeight) {}
    CheckActive(npc) { return true; }
    PreKill(npc) { return true; }
    OnKill(npc) {}

    // O peso no sorteio do spawn natural (o do jogo pesa 1). 0 = não nasce.
    // SpawnModBiomes: os ModBiome onde ele nasce, no Bestiário (a classe, a
    // instância ou o Type), escritos no SetDefaults como no tModLoader.
    SpawnChance(spawnInfo) { return 0; }
    // Sorteado: como nasce, no bloco do spawn (tileX, tileY), como no
    // tModLoader. Devolve o índice do NPC. Padrão: em cima do bloco.
    SpawnNPC(tileX, tileY) {
        const N = Terraria.NPC;
        return N.NewNPC(N.GetSpawnSourceForNaturalSpawn(), tileX * 16 + 8, tileY * 16, this.Type, 0, 0, 0, 0, 0, 255);
    }

    // Morador (npc.townNPC = true no SetDefaults, aiStyle 7).
    CanTownNPCSpawn(numTownNPCs) { return false; }
    CheckConditions(left, right, top, bottom) { return true; }
    SetNPCNameList() { return []; }
    GetChat(npc) { return undefined; }
    CanChat(npc) { return npc.townNPC; }
    TownNPCProfile() { return null; }
    ModifyActiveShop(npc, shopName, items) {}
    ModifyNPCHappiness(npc, player, primaryPlayerBiome, shopHelper, nearbyNPCsByType) {}
    NPCHeadSlot() { return bl.npcs.headSlot(this.Type); }
    // buttons.button e buttons.button2 recebem o texto; vazio = sem botão.
    SetChatButtons(npc, buttons) {}
    // Devolva o nome de uma loja registrada deste NPC para abri-la.
    OnChatButtonClicked(npc, firstButton) { return undefined; }
    AddShops() {}
    // attack: damage, knockback, projType, attackDelay, speed, gravityCorrection, randomOffset.
    TownNPCAttackStrength(npc, attack) {}
    TownNPCAttackProj(npc, attack) {}
    TownNPCAttackProjSpeed(npc, attack) {}

    static NPCValue(p = 0, g = 0, s = 0, c = 0) {
        return p * 1000000 + g * 10000 + s * 100 + c;
    }

    static register(cls) {
        if (typeof cls !== 'function' || !(cls.prototype instanceof ModNPC)) {
            throw new TypeError('ModNPC.register(Classe): passe a classe, que estende ModNPC');
        }

        GoreLoader.Autoload();
        Entities.Define(Terraria.NPC, 'ModNPC');

        const inst = new cls();
        const name = cls.name;
        Templates.Adopt(cls, inst);
        inst.Texture = ModFiles.ContentTexture(inst, cls);

        let animation = inst.AnimationType | 0;
        let type = -1;
        let townNpc = false;

        const def = {
            name,
            texture: ModFiles.Texture(inst.Texture),
            head: ModFiles.Texture(inst.HeadTexture || inst.Texture + '_Head'),
            shimmerHead: ModFiles.Texture(inst.ShimmerHeadTexture || inst.Texture + '_Shimmer_Head'),
            bossHead: ModFiles.Texture(inst.BossHeadTexture || inst.Texture + '_Head_Boss'),
            animationType: animation,
            displayName: inst.DisplayName || Lang.Localized('NPCName', name) || name,
            setDefaults(npc) {
                const m = Entities.Bind(inst.Clone(npc), npc, 'ModNPC');
                m.SetDefaults(npc);
                m.ApplyBuffImmunity(npc);
                m.PostSetDefaults(npc);
                ModMusic.TrackNpc(npc, m);
                townNpc = !!npc.townNPC;

                const now = m.AnimationType | 0;
                if (now !== animation && type >= 0) {
                    animation = now;
                    inst.AnimationType = now;
                    bl.npcs.setAnimationType(type, now);
                }
            },
            setStaticDefaults(t) {
                inst.SetStaticDefaults();
                inst.PostStaticDefaults();
                bl.npcs.setFrames(t, Terraria.Main.npcFrameCount[t]);
                Templates.HideFromMenu('npc', inst, t, () => {
                    const drawn = Terraria.ID.NPCID.Sets.NPCBestiaryDrawOffset;
                    return drawn.ContainsKey(t) && drawn.get_Item(t).Hide;
                });
                inst.ModifyNPCLoot(new NPCLoot(t));
            },
        };
        if (Hooks.Overrides(cls, ModNPC, 'HitEffect')) {
            def.hitEffect = (npc, hitDirection, damage) => {
                const m = NPCLoader.Of(npc);
                if (m) m.HitEffect(npc, hitDirection, damage);
            };
        }

        type = bl.npcs.register(def);
        inst.Type = type;
        Lang.Follow('NPCName.' + name, def.displayName);
        NPCLoader.ByType.set(type, inst);
        if (Hooks.Overrides(cls, ModNPC, 'SpawnChance')) NPCLoader.Spawnable.push(inst);

        // O nome de busca do jogo (NPCID.Search): 'ExampleMod/ExamplePerson'.
        // Sem ele a felicidade do morador lança exceção.
        const searchName = String(bl.mod.name).replace(/\s+/g, '') + '/' + name;
        const moods = TownNPCLoader.MoodTexts(name);
        const looks = TownNPCLoader.LookFiles(inst.Texture);

        Ready.Add(() => {
            // A amostra do jogo já passou pelo SetDefaults do mod: é dela que sai se é morador.
            const sample = Safe.Run(name + ' amostra', () => !!Terraria.ID.ContentSamples.NpcsByNetId.get_Item(type).townNPC);
            townNpc = townNpc || sample === true;

            Safe.Run(name + ' NPCID.Search', () => {
                const search = Terraria.ID.NPCID.Search;
                if (!search.ContainsName(searchName)) search['void Add(string name, int id)'](searchName, type);
            });

            for (const [key, texts] of moods) {
                ModLocalization.Register('TownNPCMood_' + searchName + '.' + key, texts);
                ModLocalization.Register('TownNPCMood_' + searchName + 'Transformed.' + key, texts);
            }

            if (!inst.HideFromBestiary) Safe.Run(name + '.SetBestiary', () => BestiaryLoader.Register(inst, townNpc));
            if (Hooks.Overrides(cls, ModNPC, 'AddShops')) Safe.Run(name + '.AddShops', () => inst.AddShops());
            if (townNpc) Safe.Run(name + ' (perfil de morador)', () => TownNPCLoader.SetupLooks(inst, looks));
            if (Hooks.Overrides(cls, ModNPC, 'TownNPCProfile')) {
                const profile = Safe.Run(name + '.TownNPCProfile', () => inst.TownNPCProfile());
                if (profile) Safe.Run(name + ' TownNPCProfile', () =>
                    Terraria.GameContent.TownNPCProfiles.Instance._townNPCProfiles.set_Item(type, profile));
            }

            inst.PostSetupContent();
        });

        NPCLoader.Hook(cls, type);
        return type;
    }

    static isModType(type) { return bl.npcs.isModNpc(type); }
    static isModNPC(npc) { return !!npc && bl.npcs.isModNpc(npc.type); }
    static getTypeByName(name) { return bl.npcs.typeOf(name); }
    static getModNPC(type) { return NPCLoader.ByType.get(type); }
    static getByName(name) { return NPCLoader.ByType.get(bl.npcs.typeOf(name)); }
}
