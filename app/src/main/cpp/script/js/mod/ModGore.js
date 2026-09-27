class ModGore {
    // O tipo do gore 'Assets/Textures/Gores/<nome>.png' deste mod, ou 0.
    static getTypeByName(name) {
        return GoreLoader.TypeOf(bl.mod && bl.mod.uuid, name);
    }
}
