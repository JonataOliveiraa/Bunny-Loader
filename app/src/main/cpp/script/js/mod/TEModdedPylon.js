// A tile entity de um pilar de mod, como a TEModdedPylon do tModLoader: o mod
// estende esta classe (vazia basta) e o ModPylon a coloca e tira. Colocar e
// tirar avisam o sistema de pilares do jogo (PylonLoader).
class TEModdedPylon extends ModTileEntity {
    // A célula de cima à esquerda de um pilar de mod (o padrão do jogo para os dele).
    IsTileValidForEntity(x, y) {
        const m = TileLoader.At(x, y);
        if (!(m instanceof ModPylon)) return false;
        const tile = TileLoader.Tile(x, y);
        const data = Terraria.ObjectData.TileObjectData['TileObjectData GetTileData(Tile getTile)'](tile);
        return !!data && tile.frameY === 0 && tile.frameX % data.CoordinateFullWidth === 0;
    }

    // O pilar desta entidade (um ModPylon), ou null.
    TryGetModPylon() {
        const m = this.Position ? TileLoader.At(this.Position.X, this.Position.Y) : null;
        return m instanceof ModPylon ? m : null;
    }

    // Para o HookPostPlaceMyPlayer com o PlacementHook(false) do molde: a
    // entidade no canto de cima à esquerda, pela origem do TileObjectData.
    Hook_AfterPlacement(i, j, type, style, direction, alternate) {
        return this.Generic_Hook_AfterPlacement(i, j, type, style, direction, alternate);
    }
}
