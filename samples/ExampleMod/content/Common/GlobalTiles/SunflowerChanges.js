const { TileID } = Terraria.ID;

// Um GlobalTile que muda um tile do jogo (o SunflowerChanges do tModLoader):
// o Girassol passa a aceitar o Bloco de Exemplo como chão.
export class SunflowerChanges extends GlobalTile {
    SetStaticDefaults() {
        const data = Terraria.ObjectData.TileObjectData['TileObjectData GetTileData(int type, int style, int alternate)'](TileID.Sunflower, 0, 0);
        const valid = data.AnchorValidTiles;
        const exampleTile = ModContent.TileType('ExampleTile');

        const grown = valid.cloneResized(valid.length + 1);
        grown[valid.length] = exampleTile;
        data.AnchorValidTiles = grown;
    }
}
