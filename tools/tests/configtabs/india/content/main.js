// Um dos 14 mods de tools/tests/configtabs: só existe para ter uma aba.
export class IndiaConfig extends ModConfig {
    static Options = {
        IndiaHeader: ModConfig.Header({ label: 'Opções de India' }),
        IndiaToggle: ModConfig.Toggle(true, { label: 'Ligado em India' }),
    };
}
bl.log('configtabs india FIM: tudo ok');

export default class TestConfigTabsIndia extends Mod {}
