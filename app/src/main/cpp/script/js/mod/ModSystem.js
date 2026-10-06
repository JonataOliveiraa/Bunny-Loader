class ModSystem {
    OnModLoad() {}
    OnModUnload() {}
    OnLocalizationsLoaded() {}
    ResizeArrays() {}
    AddRecipeGroups() {}
    AddRecipes() {}
    PostAddRecipes() {}
    PostSetupRecipes() {}
    PostSetupContent() {}

    // OnWorldLoad antes do LoadWorldData, PostWorldLoad depois; no cliente de
    // multijogador só o OnWorldLoad.
    OnWorldLoad() {}
    LoadWorldData(tag) {}
    PostWorldLoad() {}
    SaveWorldData(tag) {}
    SaveWorldHeader(tag) {}
    CanWorldBePlayed(playerData, worldData) { return true; }
    WorldCanBePlayedRejectionMessage(playerData, worldData) {
        return 'O personagem ' + playerData.Name + ' não pode entrar no mundo ' + worldData.Name + '.';
    }
    PreWorldGen() {}
    ModifyWorldGenTasks(tasks) {}
    PostWorldGen() {}
    ModifyHardmodeTasks(tasks) {}
    ClearWorld() {}
    PreSaveAndQuit() {}
    OnWorldUnload() {}

    // UpdateWorld e UpdateTime só no servidor (ou sozinho); PostUpdateEverything em todos.
    PreUpdateWorld() {}
    PostUpdateWorld() {}
    PreUpdateTime() {}
    PostUpdateTime() {}
    PostUpdateEverything() {}
    UpdateUI(gameTime) {}
    PostUpdateInput() {}
    PreUpdateEntities() {}
    PreUpdatePlayers() {}
    PostUpdatePlayers() {}
    PreUpdateNPCs() {}
    PostUpdateNPCs() {}
    PreUpdateGores() {}
    PostUpdateGores() {}
    PreUpdateProjectiles() {}
    PostUpdateProjectiles() {}
    PreUpdateItems() {}
    PostUpdateItems() {}
    PreUpdateDusts() {}
    PostUpdateDusts() {}
    PreUpdateInvasions() {}
    PostUpdateInvasions() {}
    ModifyTimeRate(timeRate, tileUpdateRate, eventUpdateRate) {}
    ModifySunLightColor(tileColor, backgroundColor) {}
    ModifyLightingBrightness(scale) {}
    ModifyScreenPosition() {}
    ModifyTransformMatrix(transform) {}

    // A contagem de blocos em volta do jogador local (a cada 5 quadros, a do
    // jogo): Reset antes de contar, TileCountsAvailable no fim. O Reset vem
    // também ao sair do mundo e ao carregar outro. tileCounts[tipo]
    // vale durante a chamada; guarde o número, não o array.
    ResetNearbyTileEffects() {}
    TileCountsAvailable(tileCounts) {}

    NetSend(writer) {}
    NetReceive(reader) {}
    HijackGetData(messageType, reader, playerNumber) { return false; }
    HijackSendData(whoAmI, msgType, remoteClient, ignoreClient, text, number, number2, number3, number4, number5, number6, number7) { return false; }

    static register(cls) {
        if (typeof cls !== 'function' || !(cls.prototype instanceof ModSystem)) {
            throw new TypeError('ModSystem.register(Classe): passe a classe, que estende ModSystem');
        }

        const inst = new cls();
        Templates.Adopt(cls, inst);
        SystemLoader.Add(inst);
        SystemLoader.Invoke(inst, 'OnModLoad');
        Ready.Add(() => SystemLoader.Invoke(inst, 'ResizeArrays'), 'resize');
        Ready.Add(() => SystemLoader.Invoke(inst, 'AddRecipeGroups'), 'groups');
        Ready.Add(() => SystemLoader.Invoke(inst, 'AddRecipes'), 'recipes');
        Ready.Add(() => SystemLoader.Invoke(inst, 'PostAddRecipes'), 'postRecipes');
        Ready.Add(() => SystemLoader.Invoke(inst, 'PostSetupContent'));
        Ready.Add(() => SystemLoader.Invoke(inst, 'PostSetupRecipes'), 'finish');
        SystemLoader.Hook(cls);
        return inst;
    }
}
