namespace Terraria;

public readonly unsafe partial struct TileData
{
    static readonly Tile[] Slots = new Tile[32];

    static int nextSlot;

    public ref Tile this[Microsoft.Xna.Framework.Point position] => ref this[position.X, position.Y];

    public ref Tile this[int x, int y]
    {
        get
        {
            ref Tile tile = ref Slots[System.Threading.Interlocked.Increment(ref nextSlot) & 31];
            if (x < 0 || y < 0 || x >= _width || y >= _height)
                tile = Tile.s_Null;
            else
                tile._tileOffset = _width * y + x;
            return ref tile;
        }
    }
}
