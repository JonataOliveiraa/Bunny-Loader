class ModGore {
    // O tipo do gore '<pasta>/Gores/<nome>.png' deste mod, ou 0. Fora
    // da carga (num hook do jogo, sem mod na pilha), o primeiro mod que o tiver.
    static getTypeByName(name) {
        return bl.mod ? GoreLoader.TypeOf(bl.mod.uuid, name) : GoreLoader.TypeOfAny(name);
    }
}
