// Uma raridade de mod, como o ModRarity do tModLoader: o tipo sai no registro
// (depois das 12 do jogo) e vai em item.rare. RarityColor é lido a cada
// desenho (pode piscar, com Main.DiscoR...); GetPrefixedRarity decide a
// raridade quando o prefixo sobe ou desce o item (offset de -2 a +2).
class ModRarity {
    Type = undefined;

    SetStaticDefaults() {}

    get RarityColor() { return Color.White; }

    GetPrefixedRarity(offset, valueMult) { return this.Type; }

    static register(cls) {
        if (typeof cls !== 'function' || !(cls.prototype instanceof ModRarity)) {
            throw new TypeError('ModRarity.register(Classe): passe a classe, que estende ModRarity');
        }

        const inst = new cls();
        Templates.Adopt(cls, inst);
        RarityLoader.Add(inst);
        return inst.Type;
    }
}
