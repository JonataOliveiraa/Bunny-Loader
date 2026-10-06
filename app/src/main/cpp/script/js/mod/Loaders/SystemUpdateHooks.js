class SystemUpdateHooks {
    static #clockRate = 1;
    static #tileRemainder = 0;
    static #eventRemainder = 0;

    static Install(cls) {
        const has = name => Hooks.Overrides(cls, ModSystem, name);
        const Main = Terraria.Main;
        const call = SystemLoader.Call;
        const pairs = [
            ['PlayersBegin', ['PreUpdatePlayers']],
            ['PlayersEnd', ['PostUpdatePlayers']],
            ['NPCsBegin', ['PreUpdateNPCs']],
            ['NPCsEnd', ['PostUpdateNPCs', 'PreUpdateGores']],
            ['GoresEnd', ['PostUpdateGores']],
            ['ItemsBegin', ['PostUpdateProjectiles', 'PreUpdateItems']],
            ['ItemsEnd', ['PostUpdateItems']],
        ];
        for (const [stage, methods] of pairs) {
            if (methods.some(has)) Hooks.Once('system.stage.' + stage, () => {
                bl.installSystemStage(stage, () => {
                    for (const method of methods) call(method);
                });
            });
        }
        if (has('PreUpdateEntities') || has('PostUpdateEverything')) Hooks.Once('system.UpdateEverything', () => {
            Main['void DoUpdateInWorld()'].hook((original, self) => {
                call('PreUpdateEntities');
                original(self);
                call('PostUpdateEverything');
            });
        });
        if (has('PreUpdateProjectiles')) Hooks.Once('system.ProjectilesBegin', () => {
            Main['void PreUpdateAllProjectiles()'].hook((original, self) => {
                call('PreUpdateProjectiles');
                original(self);
            });
        });
        if (has('PreUpdateDusts') || has('PostUpdateDusts')) Hooks.Once('system.Dusts', () => {
            Terraria.Dust['void UpdateDust()'].hook(original => {
                call('PreUpdateDusts');
                original();
                call('PostUpdateDusts');
            });
        });
        if (has('PreUpdateInvasions') || has('PostUpdateInvasions')) Hooks.Once('system.Invasions', () => {
            Main['void UpdateInvasion()'].hook(original => {
                call('PreUpdateInvasions');
                original();
                call('PostUpdateInvasions');
            });
        });
        if (has('UpdateUI')) Hooks.Once('system.UI', () => {
            Main['void UpdateUIStates(GameTime gameTime)'].hook((original, time) => {
                original(time);
                if (!Main.gameMenu) call('UpdateUI', time);
            });
        });
        if (has('PostUpdateInput')) Hooks.Once('system.Input', () => {
            Main['void DoUpdate_HandleInput()'].hook((original, self) => {
                original(self);
                call('PostUpdateInput');
            });
        });
        if (has('ModifyTimeRate')) Hooks.Once('system.TimeRate', () => {
            bl.installSystemStage('ClockAdvance', () => SystemUpdateHooks.#clockRate);
            Main['void UpdateTimeRate()'].hook(original => {
                original();
                const time = new Ref(Main.dayRate);
                const tiles = new Ref(Main.desiredWorldTilesUpdateRate);
                const events = new Ref(Main.dayRate);
                const initialTime = time.value, initialTiles = tiles.value;
                call('ModifyTimeRate', time, tiles, events);
                SystemUpdateHooks.#clockRate = SystemUpdateHooks.#Rate(time.value, initialTime);
                const tileRate = SystemUpdateHooks.#Rate(tiles.value, initialTiles) + SystemUpdateHooks.#tileRemainder;
                const eventRate = SystemUpdateHooks.#Rate(events.value, initialTime) + SystemUpdateHooks.#eventRemainder;
                Main.desiredWorldTilesUpdateRate = Math.floor(tileRate);
                Main.dayRate = Math.floor(eventRate);
                SystemUpdateHooks.#tileRemainder = tileRate % 1;
                SystemUpdateHooks.#eventRemainder = eventRate % 1;
            });
        });
    }

    static #Rate(value, fallback) {
        return Number.isFinite(value) && value >= 0 && value <= 2147483647 ? value : fallback;
    }
}
