// Um dos 14 mods de tools/tests/configtabs: só existe para ter uma aba.
export class HotelConfig extends ModConfig {
    static Options = {
        HotelHeader: ModConfig.Header({ label: 'Opções de Hotel' }),
        HotelToggle: ModConfig.Toggle(true, { label: 'Ligado em Hotel' }),
    };
}
bl.log('configtabs hotel FIM: tudo ok');

export default class TestConfigTabsHotel extends Mod {}
