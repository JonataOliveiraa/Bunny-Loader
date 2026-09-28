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

    // A contagem de blocos em volta do jogador local (a cada 5 quadros, a do
    // jogo): Reset antes de contar, TileCountsAvailable no fim. O Reset vem
    // também ao sair do mundo e ao carregar outro. tileCounts[tipo]
    // vale durante a chamada; guarde o número, não o array.
    ResetNearbyTileEffects() {}
    TileCountsAvailable(tileCounts) {}

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
