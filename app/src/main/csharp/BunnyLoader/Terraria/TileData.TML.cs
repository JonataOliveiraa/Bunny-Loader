namespace Terraria;

public readonly unsafe partial struct TileData
{
    public Tile this[int x, int y]
    {
        get
        {
            if (x < 0 || y < 0 || x >= _width || y >= _height) return Tile.s_Null;
            Tile tile = default;
            tile._tileOffset = _width * y + x;
            return tile;
        }
    }
}
