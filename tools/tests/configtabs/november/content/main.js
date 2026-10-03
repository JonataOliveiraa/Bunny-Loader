// Um dos 14 mods de tools/tests/configtabs: só existe para ter uma aba.
export class NovemberConfig extends ModConfig {
    static Options = {
        NovemberHeader: ModConfig.Header({ label: 'Opções de November' }),
        NovemberToggle: ModConfig.Toggle(true, { label: 'Ligado em November' }),
    };
}
bl.log('configtabs november FIM: tudo ok');

export default class TestConfigTabsNovember extends Mod {}
