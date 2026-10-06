class SystemDrawHooks {
    static Install(cls) {
        const has = name => Hooks.Overrides(cls, ModSystem, name);
        const Main = Terraria.Main;
        const call = SystemLoader.Call;
        if (has('ModifyScreenPosition')) Hooks.Once('system.Camera', () => {
            PlayerDrawHooks.InstallCamera(false);
        });
        if (has('ModifyTransformMatrix')) Hooks.Once('system.Transform', () => {
            bl.installSystemStage('Transform', () => {
                if (Main.gameMenu) return;
                const transform = new Ref(Main.GameViewMatrix);
                call('ModifyTransformMatrix', transform);
                if (transform.value) Main.GameViewMatrix = transform.value;
            });
        });
        if (has('ModifySunLightColor')) Hooks.Once('system.Sunlight', () => {
            Main['void ApplyColorOfTheSkiesToTiles()'].hook(original => {
                original();
                const tiles = new Ref(Main.tileColor), background = new Ref(Main.ColorOfTheSkies);
                call('ModifySunLightColor', tiles, background);
                Main.tileColor = tiles.value;
                Main.ColorOfTheSkies = background.value;
            });
        });
        if (has('ModifyLightingBrightness')) Hooks.Once('system.Brightness', () => {
            const Lighting = Terraria.Lighting;
            Lighting['void UpdateGlobalBrightness()'].hook(original => {
                original();
                const scale = new Ref(Lighting.GlobalBrightness);
                call('ModifyLightingBrightness', scale);
                if (Number.isFinite(scale.value) && scale.value >= 0) Lighting.GlobalBrightness = scale.value;
            });
        });
    }
}
