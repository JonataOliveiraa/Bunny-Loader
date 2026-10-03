// Um dos 14 mods de tools/tests/configtabs: só existe para ter uma aba.
export class KiloConfig extends ModConfig {
    static Options = {
        KiloHeader: ModConfig.Header({ label: 'Opções de Kilo' }),
        KiloToggle: ModConfig.Toggle(true, { label: 'Ligado em Kilo' }),
    };
}
bl.log('configtabs kilo FIM: tudo ok');

export default class TestConfigTabsKilo extends Mod {}
