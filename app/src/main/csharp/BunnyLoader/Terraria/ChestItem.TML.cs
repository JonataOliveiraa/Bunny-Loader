namespace Terraria;

public unsafe partial struct ChestItem
{
    public void SetDefaults(int itemType)
    {
        type = (short)itemType;
        stack = 1;
        prefix = 0;
    }
}
