// Um dos 14 mods de tools/tests/configtabs: só existe para ter uma aba.
export class EchoConfig extends ModConfig {
    static Options = {
        EchoHeader: ModConfig.Header({ label: 'Opções de Echo' }),
        EchoToggle: ModConfig.Toggle(true, { label: 'Ligado em Echo' }),
    };
}
bl.log('configtabs echo FIM: tudo ok');

export default class TestConfigTabsEcho extends Mod {}
