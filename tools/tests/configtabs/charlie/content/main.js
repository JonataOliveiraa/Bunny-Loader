// Um dos 14 mods de tools/tests/configtabs: só existe para ter uma aba.
export class CharlieConfig extends ModConfig {
    static Options = {
        CharlieHeader: ModConfig.Header({ label: 'Opções de Charlie' }),
        CharlieToggle: ModConfig.Toggle(true, { label: 'Ligado em Charlie' }),
    };
}
bl.log('configtabs charlie FIM: tudo ok');

export default class TestConfigTabsCharlie extends Mod {}
