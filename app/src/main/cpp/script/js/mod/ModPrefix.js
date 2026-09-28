// Um prefixo (modificador) de mod, como o ModPrefix do tModLoader. Uma
// instância por prefixo; o Type é o número que o item guarda em item.prefix.
class ModPrefix {
    Type = undefined;
    // Texto, ou { 'pt-BR': ..., 'en-US': ... }. Vazio: PrefixName.<Classe> do Localization.
    DisplayName = '';

    get Name() { return this.constructor.name; }
    get FullName() { return (this.Mod ? this.Mod.id || this.Mod.uuid : '?') + '/' + this.Name; }

    // Que itens podem ganhar este prefixo (Custom: só onde CanRoll ou o item deixar).
    get Category() { return PrefixCategory.Custom; }

    // Com as tabelas do jogo já crescidas (PrefixID.Sets.ReducedNaturalChance[this.Type]...).
    SetStaticDefaults() {}

    // O peso na rolagem; o de cada prefixo do jogo é 1.
    RollChance(item) { return 1; }
    CanRoll(item) { return this.RollChance(item) > 0; }

    // Os `ref` do tModLoader chegam como Ref (.value): damageMult.value *= 1.2.
    // tagDamage e armorPenetration são do 1.4.5 (os de lacaio do jogo).
    // Com um parâmetro só, recebe { damage, knockBack, speed, size, shootSpeed,
    // mana, crit, tagDamage, armorPenetration }, como no ExMod do TL.
    SetStats(damageMult, knockbackMult, useTimeMult, scaleMult, shootSpeedMult, manaMult, critBonus,
             tagDamage, armorPenetration) {}

    // false: o prefixo não pega neste item (o jogo já confere os status do SetStats).
    AllStatChangesHaveEffectOn(item) { return true; }

    // Depois dos status do SetStats: o que mais o prefixo muda no item.
    Apply(item) {}

    // O preço (e com ele a raridade): valueMult.value *= 1.1.
    ModifyValue(valueMult) {}

    // A cada quadro, com o acessório equipado.
    ApplyAccessoryEffects(player) {}

    // Linhas a mais no tooltip (as de dano, velocidade... o jogo já põe).
    GetTooltipLines(item) { return null; }

    static register(cls) {
        if (typeof cls !== 'function' || !(cls.prototype instanceof ModPrefix)) {
            throw new TypeError('ModPrefix.register(Classe): passe a classe, que estende ModPrefix');
        }

        const inst = new cls();
        Templates.Adopt(cls, inst);
        PrefixLoader.Register(inst);
        return inst.Type;
    }

    static isModType(type) { return PrefixLoader.GetPrefix(type) !== undefined; }
    static getModPrefix(type) { return PrefixLoader.GetPrefix(type); }
    static getTypeByName(name) { return ContentLookup.TypeOf(PrefixLoader.ByType, name); }
}
