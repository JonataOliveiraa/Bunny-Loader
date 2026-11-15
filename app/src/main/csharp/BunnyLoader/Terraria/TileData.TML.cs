namespace Terraria;

public readonly unsafe partial struct TileData
{
    [System.ThreadStatic]
    static Tile[]? slots;

    [System.ThreadStatic]
    static int nextSlot;

    public ref Tile this[Microsoft.Xna.Framework.Point position] => ref this[position.X, position.Y];

    public ref Tile this[int x, int y]
    {
        get
        {
            Tile[] ring = slots ??= new Tile[32];
            ref Tile tile = ref ring[++nextSlot & 31];
            if (x < 0 || y < 0 || x >= _width || y >= _height)
                tile = Tile.s_Null;
            else
                tile._tileOffset = _width * y + x;
            return ref tile;
        }
    }

    internal static int RawOffset(int x, int y)
    {
        TileData world = Main.tile;
        if (world.Pointer == 0 || x < 0 || y < 0)
            return -1;
        int width = world._width;
        return x >= width || y >= world._height ? -1 : width * y + x;
    }

    internal static bool RawActive(int offset) => offset >= 0 && (((short*)TileSHeader)[((uint*)TileLookup)[offset]] & 0x20) != 0;

    internal static ushort RawType(int offset) => offset < 0 ? (ushort)0 : ((ushort*)TileType)[((uint*)TileLookup)[offset]];

    internal static short RawFrameX(int offset) => offset < 0 ? (short)0 : ((short*)TileFrameX)[((uint*)TileLookup)[offset]];

    internal static short RawFrameY(int offset) => offset < 0 ? (short)0 : ((short*)TileFrameY)[((uint*)TileLookup)[offset]];

    internal static ushort RawWall(int offset) => offset < 0 ? (ushort)0 : ((ushort*)TileWall)[offset];
}
