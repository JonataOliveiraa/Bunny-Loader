namespace Terraria;

public unsafe partial struct Tile
{
    public byte LiquidAmount
    {
        get => liquid;
        set => liquid = value;
    }

    public int LiquidType
    {
        get => liquidType();
        set => liquidType(value);
    }

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

    public ushort WallType
    {
        get => wall;
        set => wall = value;
    }

    public int WallFrameX
    {
        get => wallFrameX();
        set => wallFrameX(value);
    }

    public int WallFrameY
    {
        get => wallFrameY();
        set => wallFrameY(value);
    }

    public int WallFrameNumber
    {
        get => wallFrameNumber();
        set => wallFrameNumber((byte)value);
    }

    public bool HasTile => active();

    public bool HasUnactuatedTile => nactive();

    public bool HasActuator
    {
        get => actuator();
        set => actuator(value);
    }

    public bool IsActuated
    {
        get => inActive();
        set => inActive(value);
    }
}
