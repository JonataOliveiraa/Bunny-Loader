namespace Terraria;

public unsafe partial struct Tile
{
    public ushort TileType
    {
        get => type;
        set => type = value;
    }

    public bool HasTile => active();

    public bool HasUnactuatedTile => nactive();
}
