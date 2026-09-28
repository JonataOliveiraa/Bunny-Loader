// O par do tools/tests/modbiome: um bioma com o mesmo nome de classe do bioma
// de lá. O outro mod pega a classe e o liga pelo Call.
export class SameNameBiome extends ModBiome {
    static on = false;

    SetStaticDefaults() {
        this.Music = -1;
    }

    IsBiomeActive(player) {
        return SameNameBiome.on;
    }
}

// O run.sh espera um FIM de cada pasta de teste.
bl.log('modbiomeb FIM: carregado');

export default class ModBiomeB extends Mod {
    Call(what, value) {
        if (what === 'biome') return SameNameBiome;
        if (what === 'set') SameNameBiome.on = !!value;
        return undefined;
    }
}
