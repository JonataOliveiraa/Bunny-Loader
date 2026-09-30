// Um penteado de mod, como o ModHair do tModLoader: a textura da classe
// (Content/Hairs/X.js -> Assets/Textures/Hairs/X.png) e a _Alt (com chapéu).
// Aparece na criação de personagem (AvailableDuringCharacterCreation) e no
// Cabeleireiro quando as condições valem (GetUnlockConditions: objetos com
// IsMet(), ou funções). IsUnlocked(naCriacao, noCabeleireiro), do ExMod,
// decide tudo sozinho se o mod o escrever.
class ModHair {
    Type = undefined;
    Texture = this.constructor.name;
    get AltTexture() { return this.Texture + '_Alt'; }
    get AvailableDuringCharacterCreation() { return true; }

    SetStaticDefaults() {}
    GetUnlockConditions() { return []; }

    IsUnlocked(isAtCharacterCreation, isAtStylist) {
        if (isAtCharacterCreation) return this.AvailableDuringCharacterCreation;
        const conditions = this.GetUnlockConditions() || [];
        for (const c of conditions) {
            const met = typeof c === 'function' ? c() : c && typeof c.IsMet === 'function' ? c.IsMet() : c;
            if (!met) return false;
        }
        return true;
    }

    static register(cls) {
        if (typeof cls !== 'function' || !(cls.prototype instanceof ModHair)) {
            throw new TypeError('ModHair.register(Classe): passe a classe, que estende ModHair');
        }

        const inst = new cls();
        Templates.Adopt(cls, inst);
        inst.Texture = ModFiles.ContentTexture(inst, cls);
        HairLoader.Add(inst);
        return inst.Type;
    }
}
