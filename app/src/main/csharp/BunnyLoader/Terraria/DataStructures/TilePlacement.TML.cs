using Terraria.Enums;

namespace Terraria.DataStructures;

public partial struct Point16
{
    public static readonly Point16 Zero = new(0, 0);

    public static readonly Point16 NegativeOne = new(-1, -1);

    public Point16(int x, int y)
    {
        X = checked((short)x);
        Y = checked((short)y);
    }
}

public partial struct AnchorData
{
    public static AnchorData Empty => default;

    public AnchorData(AnchorType type, int tileCount, int checkStart)
    {
        this.type = type;
        this.tileCount = tileCount;
        this.checkStart = checkStart;
    }
}
