// Um estilo de água de mod, como o ModWaterStyle do tModLoader. Aparece quando o
// ModSceneEffect (ou ModBiome) que o devolve em `WaterStyle` ganha a cena do
// jogador. Slot: o número do estilo, depois dos 15 do jogo. Texturas: a da
// classe (Content/Biomes/X.js -> Content/Biomes/X.png, a superfície,
// como o water_N do jogo), X_Block (o bloco) e X_Slope (a rampa; sem ela, o bloco).
class ModWaterStyle {
    // O número da cachoeira desta água: ModContent.GetInstance(ModWaterfallStyle).Slot
    // ou uma do jogo (0 é a da floresta).
    ChooseWaterfallStyle() { return 0; }

    // O pó do respingo (quem cai na água). O padrão é o do jogo (DustID.Water).
    GetSplashDust() { return 33; }

    // O gore da gota que pinga do bloco. Uma de mod (ModGore.getTypeByName)
    // se comporta como a gota d'água do jogo (GoreID.WaterDrip), como o
    // UpdateType do tModLoader.
    GetDropletGore() { return 706; }

    // r, g, b são Ref (.value): quanto da luz atravessa a água (o motor de luz
    // novo, os modos Cor e Branco). O padrão é o do jogo.
    LightColorMultiplier(r, g, b) {
        r.value = 0.88;
        g.value = 0.96;
        b.value = 1.015;
    }

    // A variante da chuva: a coluna da textura dividida por 4 (a do jogo tem
    // 3 por estilo; 0 a 2 é a da floresta). Com GetRainTexture, a coluna na
    // textura do mod (0 a 7).
    GetRainVariant() { return Rand.Next(3); }

    // A textura da chuva: o caminho de um PNG no mod (ou um
    // Asset<Texture2D>), ou null para a do jogo (o padrão).
    GetRainTexture() { return null; }

    // A cor da tintura de bioma (a do cabelo) com esta água. O padrão é a da
    // floresta, como no tModLoader.
    BiomeHairColor() { return Color.new(28, 216, 94, 255); }

    SetStaticDefaults() {}

    static register(cls) {
        if (typeof cls !== 'function' || !(cls.prototype instanceof ModWaterStyle)) {
            throw new TypeError('ModWaterStyle.register(Classe): passe a classe, que estende ModWaterStyle');
        }

        const inst = new cls();
        Templates.Adopt(cls, inst);
        inst.Texture = ModFiles.ContentTexture(inst, cls);
        WaterStyleLoader.Add(inst);
        Safe.Run(cls.name + '.SetStaticDefaults', () => inst.SetStaticDefaults());
        return inst;
    }
}
