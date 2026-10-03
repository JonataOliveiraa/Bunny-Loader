// Um dos 14 mods de tools/tests/configtabs: só existe para ter uma aba.
export class GolfConfig extends ModConfig {
    static Options = {
        GolfHeader: ModConfig.Header({ label: 'Opções de Golf' }),
        GolfToggle: ModConfig.Toggle(true, { label: 'Ligado em Golf' }),
    };
}
bl.log('configtabs golf FIM: tudo ok');

export default class TestConfigTabsGolf extends Mod {}
