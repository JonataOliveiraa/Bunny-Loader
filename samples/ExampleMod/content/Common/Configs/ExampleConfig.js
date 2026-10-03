export class ExampleConfig extends ModConfig {
    static Options = {
        ToggleHeader: ModConfig.Header(),
        ShowFlavorText: ModConfig.Toggle(true),
        BossMusic: ModConfig.Toggle(true),
        ShowBossBar: ModConfig.Toggle(true),
        ExtraLoot: ModConfig.Toggle(false),

        RangeHeader: ModConfig.Header(),
        BossHealth: ModConfig.Range(100, { min: 50, max: 200, step: 10, suffix: '%' }),
        BossDamage: ModConfig.Range(100, { min: 25, max: 300, step: 25, suffix: '%' }),
        TorchLight: ModConfig.Range(1, { min: 0.5, max: 2, step: 0.1, suffix: 'x' }),

        RadioHeader: ModConfig.Header(),
        Particles: ModConfig.Radio('normal', ['low', 'normal', 'high']),
        BossSpeed: ModConfig.Radio('normal', ['slow', 'normal', 'fast']),

        DropdownHeader: ModConfig.Header(),
        FavoriteBiome: ModConfig.Dropdown('forest', ['forest', 'desert', 'snow', 'jungle', 'corruption', 'crimson', 'hallow', 'ocean', 'mushroom']),
        ShopPrices: ModConfig.Dropdown('normal', ['cheap', 'normal', 'expensive']),

        CycleHeader: ModConfig.Header(),
        Weather: ModConfig.Cycle('clear', ['clear', 'rain', 'snow', 'storm']),
        TimeOfDay: ModConfig.Cycle('day', ['dawn', 'day', 'dusk', 'night']),

        ColorHeader: ModConfig.Header(),
        GlowColor: ModConfig.Color('#FFB347'),

        ButtonHeader: ModConfig.Header(),
        RandomGlow: ModConfig.Button('RandomizeGlow'),
        ResetAll: ModConfig.Button('ResetToDefaults'),

        LinkHeader: ModConfig.Header(),
        Repository: ModConfig.Link('https://github.com/JonataOliveiraa/Bunny-Loader'),
        Wiki: ModConfig.Link('https://github.com/JonataOliveiraa/Bunny-Loader/wiki'),

        DisabledHeader: ModConfig.Header(),
        EnablePets: ModConfig.Toggle(false),
        PetGlow: ModConfig.Toggle(true, { enabledWhen: 'EnablePets' }),
        PetSize: ModConfig.Radio('medium', ['small', 'medium', 'large'], { enabledWhen: 'EnablePets' }),
        PetDistance: ModConfig.Range(3, { min: 1, max: 10, step: 1, enabledWhen: 'EnablePets' }),
    };

    RandomizeGlow() {
        const hex = Math.floor(Math.random() * 0x1000000).toString(16).padStart(6, '0');
        this.SetOption('GlowColor', '#' + hex);
    }

    OnChanged(key) {
        bl.log(`ExampleConfig: ${key} = ${this[key]}`);
    }
}
