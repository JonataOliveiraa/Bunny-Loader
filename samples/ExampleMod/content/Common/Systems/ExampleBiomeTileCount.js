// Quantos ExampleTile há em volta do jogador: os biomas de exemplo pedem 40.
export class ExampleBiomeTileCount extends ModSystem {
    exampleBlockCount = 0;

    TileCountsAvailable(tileCounts) {
        this.exampleBlockCount = tileCounts[ModContent.TileType('ExampleTile')];
    }
}
