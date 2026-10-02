// As opções do Example Mod, na tela "Config. dos Mods" do menu de pausa (o
// ModConfig do tModLoader). Os textos estão no Localization, em
// Configs.ExampleConfig.<Opção>.Label.
//
// É a demonstração dos três controles (e da rolagem, com 20 opções): nenhum
// conteúdo do mod lê estas opções ainda. Um mod lê assim:
//
//   ModContent.GetInstance(ExampleConfig).BossHealth   // 50 a 200
export class ExampleConfig extends ModConfig {
    static Options = {
        // Interruptor: true ou false.
        ShowFlavorText: ModConfig.Toggle(true),
        // Faixa: de 50 a 200, de 10 em 10, mostrada com "%".
        BossHealth: ModConfig.Range(100, { min: 50, max: 200, step: 10, suffix: '%' }),
        // Escolha única: o valor é o texto da escolha.
        Particles: ModConfig.Radio('normal', ['low', 'normal', 'high']),

        BossMusic: ModConfig.Toggle(true),
        BossDamage: ModConfig.Range(100, { min: 25, max: 300, step: 25, suffix: '%' }),
        BossSpeed: ModConfig.Radio('normal', ['slow', 'normal', 'fast']),
        ShowBossBar: ModConfig.Toggle(true),
        ExtraLoot: ModConfig.Toggle(false),
        DropChance: ModConfig.Range(10, { min: 0, max: 100, step: 5, suffix: '%' }),
        PetGlow: ModConfig.Toggle(true),
        PetSize: ModConfig.Radio('medium', ['small', 'medium', 'large']),
        TorchLight: ModConfig.Range(1, { min: 0.5, max: 2, step: 0.1, suffix: 'x' }),
        WingTrail: ModConfig.Toggle(true),
        WingSpeed: ModConfig.Range(100, { min: 50, max: 150, step: 10, suffix: '%' }),
        ShopPrices: ModConfig.Radio('normal', ['cheap', 'normal', 'expensive']),
        TownNPCChat: ModConfig.Toggle(true),
        BiomeMusic: ModConfig.Toggle(true),
        BiomeSize: ModConfig.Radio('normal', ['small', 'normal', 'big']),
        OreAmount: ModConfig.Range(100, { min: 0, max: 200, step: 20, suffix: '%' }),
        DebugText: ModConfig.Toggle(false),
    };

    OnChanged(key) {
        bl.log(`ExampleConfig: ${key} = ${this[key]}`);
    }
}
