// Um dos 14 mods de tools/tests/configtabs: só existe para ter uma aba.
export class DeltaConfig extends ModConfig {
    static Options = {
        DeltaHeader: ModConfig.Header({ label: 'Opções de Delta' }),
        DeltaToggle: ModConfig.Toggle(true, { label: 'Ligado em Delta' }),
    };
}
bl.log('configtabs delta FIM: tudo ok');

export default class TestConfigTabsDelta extends Mod {}
