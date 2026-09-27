// Uma instância por tipo (buff não é entidade); os métodos recebem o
// jogador/NPC e o índice na lista dele.
class ModBuff {
    Type = undefined;
    HideFromModMenu = false;
    DisplayName = '';
    Description = '';
    Texture = this.constructor.name;

    SetStaticDefaults() {}
    PostStaticDefaults() {}
    PostSetupContent() {}
    ModifyDisplayName() {}
    ModifyDescription() {}

    UpdatePlayer(player, buffIndex) {}
    UpdateNPC(npc, buffIndex) {}
    ApplyPlayer(player, buffTime) {}
    ApplyNPC(npc, buffTime) {}
    // false impede o jogo de renovar o tempo.
    ReApplyPlayer(player, buffTime, buffIndex) { return true; }
    ReApplyNPC(npc, buffTime, buffIndex) { return true; }
    // true/false decide; null = o do jogo (debuff não sai).
    CanRemove(player, buffTime, buffIndex, debuff) { return null; }
    OnRemove(player, buffTime, buffIndex) {}

    static register(cls) {
        if (typeof cls !== 'function' || !(cls.prototype instanceof ModBuff)) {
            throw new TypeError('ModBuff.register(Classe): passe a classe, que estende ModBuff');
        }

        const inst = new cls();
        const name = cls.name;
        Templates.Adopt(cls, inst);

        const displayName = Lang.ModifyPerCulture(inst, 'DisplayName',
            inst.DisplayName || Lang.Localized('BuffName', name) || name, 'ModifyDisplayName');
        const description = Lang.ModifyPerCulture(inst, 'Description',
            inst.Description || Lang.Localized('BuffDescription', name) || '', 'ModifyDescription');
        inst.Texture = ModFiles.ContentTexture(inst, cls);

        const type = bl.buffs.register({
            name,
            texture: ModFiles.Texture(inst.Texture),
            displayName,
            description,
            setStaticDefaults() {
                inst.SetStaticDefaults();
                inst.PostStaticDefaults();
                Templates.HideFromMenu('buff', inst, inst.Type, () => false);
            },
        });
        inst.Type = type;
        BuffLoader.ByType.set(type, inst);

        Ready.Add(() => inst.PostSetupContent());
        BuffLoader.Hook(cls);
        return type;
    }

    static isModType(type) { return bl.buffs.isModBuff(type); }
    static getTypeByName(name) { return bl.buffs.typeOf(name); }
    static getModBuff(type) { return BuffLoader.ByType.get(type); }
}
