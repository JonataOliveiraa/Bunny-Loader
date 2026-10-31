using Terraria.Enums;

namespace Terraria.DataStructures;

public partial struct Point16
{
    public Point16(int x, int y) { X = checked((short)x); Y = checked((short)y); }
}

public partial struct AnchorData
{
    public AnchorData(AnchorType type, int tileCount, int checkStart) { this.type = type; this.tileCount = tileCount; this.checkStart = checkStart; }
}
