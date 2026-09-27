class ModSystem {
    OnModLoad() {}
    AddRecipeGroups() {}
    AddRecipes() {}
    PostAddRecipes() {}
    PostSetupContent() {}

    // OnWorldLoad antes do LoadWorldData, PostWorldLoad depois; no cliente de
    // multijogador só o OnWorldLoad.
    OnWorldLoad() {}
    LoadWorldData(tag) {}
    PostWorldLoad() {}
    SaveWorldData(tag) {}
    ClearWorld() {}
    PreSaveAndQuit() {}
    OnWorldUnload() {}

    // UpdateWorld e UpdateTime só no servidor (ou sozinho); PostUpdateEverything em todos.
    PreUpdateWorld() {}
    PostUpdateWorld() {}
    PreUpdateTime() {}
    PostUpdateTime() {}
    PostUpdateEverything() {}

    NetSend(writer) {}
    NetReceive(reader) {}

    static register(cls) {
        if (typeof cls !== 'function' || !(cls.prototype instanceof ModSystem)) {
            throw new TypeError('ModSystem.register(Classe): passe a classe, que estende ModSystem');
        }

        const inst = new cls();
        const name = cls.name;
        Templates.Adopt(cls, inst);
        SystemLoader.Add(inst);
        Safe.Run(name + '.OnModLoad', () => inst.OnModLoad());

        Ready.Add(() => inst.AddRecipeGroups(), 'groups');
        Ready.Add(() => {
            inst.AddRecipes();
            Safe.Run(name + '.PostAddRecipes', () => inst.PostAddRecipes());
            inst.PostSetupContent();
        });

        SystemLoader.Hook(cls);
        return inst;
    }
}
