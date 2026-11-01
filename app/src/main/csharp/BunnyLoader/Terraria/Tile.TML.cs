namespace Terraria;

public unsafe partial struct Tile
{
    public ushort TileType
    {
        get => type;
        set => type = value;
    }

    public short TileFrameX
    {
        get => frameX;
        set => frameX = value;
    }

    public short TileFrameY
    {
        get => frameY;
        set => frameY = value;
    }

    public bool HasTile => active();

    public bool HasUnactuatedTile => nactive();
}
