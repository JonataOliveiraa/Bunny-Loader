// Uma conquista de mod, como o ModAchievement do tModLoader: o Achievement do
// jogo em this.Achievement, com o nome '<mod>/<Classe>' (a chave no arquivo de
// conquistas). Nome e descrição do Localization em
// Achievements.<Classe>.FriendlyName (ou .Name, do ExMod) e .Description.
//
// A textura segue o menu do celular: uma folha de 592 x 64 com o ícone
// desbloqueado em x = 0 e o bloqueado em x = 528 (oito colunas de 66 depois,
// que é onde o menu procura). A do tModLoader (130 x 64, os dois lado a lado)
// não serve: o menu leria o bloqueado fora dela. Index é a coluna/linha na
// folha (colunas de 66, oito por linha).
class ModAchievement {
    Achievement = null;
    Name = '';
    Texture = this.constructor.name;
    get Index() { return 0; }
    // Slayer, Collector, Explorer ou Challenger (Terraria.Achievements.AchievementCategory).
    get Category() { return Terraria.Achievements.AchievementCategory.Slayer; }
    // Nome e descrição viram "???" no menu enquanto não completa.
    get Hidden() { return false; }
    get IsCompleted() { return !!(this.Achievement && this.Achievement.IsCompleted); }

    SetStaticDefaults() {}
    OnCompleted(achievement) {}
    // Os avisos do jogo (AchievementsHelper), só enquanto não completa.
    OnNPCKilled(player, npcId) {}
    OnItemPickup(player, itemType, stack) {}
    OnItemCraft(itemType, stack) {}
    OnTileDestroyed(player, tileType) {}

    // As condições, como no tModLoader. Devolvem a condição do jogo
    // (a de número tem .Value).
    AddCondition(key = 'Condition') {
        return this.#Add(Terraria.GameContent.Achievements.CustomFlagCondition['AchievementCondition Create(string name)'](key));
    }
    AddIntCondition(key, maxValue) {
        if (typeof key === 'number') [key, maxValue] = ['Condition', key];
        return this.#Add(Terraria.GameContent.Achievements.CustomIntCondition['AchievementCondition Create(string name, int maxValue)'](key, maxValue));
    }
    AddFloatCondition(key, maxValue) {
        if (typeof key === 'number') [key, maxValue] = ['Condition', key];
        return this.#Add(Terraria.GameContent.Achievements.CustomFloatCondition['AchievementCondition Create(string name, float maxValue)'](key, maxValue));
    }
    AddItemCraftCondition(itemIds) { return this.#AddByUser('ItemCraftCondition', 'short', 'items', 'item', itemIds); }
    AddItemPickupCondition(itemIds) { return this.#AddByUser('ItemPickupCondition', 'short', 'items', 'item', itemIds); }
    AddNPCKilledCondition(npcIds) { return this.#AddByUser('NPCKilledCondition', 'short', 'npcIds', 'npcId', npcIds); }
    AddTileDestroyedCondition(tileIds) {
        return this.#AddByUser('TileDestroyedCondition', 'ushort', 'tileIds', null, Array.isArray(tileIds) ? tileIds : [tileIds]);
    }

    #AddByUser(cls, type, many, one, ids) {
        const C = Terraria.GameContent.Achievements[cls];
        const user = LocalUser.Active;
        const condition = Array.isArray(ids)
            ? C['AchievementCondition Create(LocalUser user, ' + type + '[] ' + many + ')'](user, ids)
            : C['AchievementCondition Create(LocalUser user, ' + type + ' ' + one + ')'](user, ids);
        return this.#Add(condition);
    }

    #Add(condition) {
        this.Achievement['void AddCondition(AchievementCondition condition)'](condition);
        (this.__conditions ||= []).push(condition);
        return condition;
    }

    static register(cls) {
        if (typeof cls !== 'function' || !(cls.prototype instanceof ModAchievement)) {
            throw new TypeError('ModAchievement.register(Classe): passe a classe, que estende ModAchievement');
        }

        const inst = new cls();
        Templates.Adopt(cls, inst);
        inst.Texture = ModFiles.ContentTexture(inst, cls);
        AchievementLoader.Add(inst);
        return inst;
    }
}
