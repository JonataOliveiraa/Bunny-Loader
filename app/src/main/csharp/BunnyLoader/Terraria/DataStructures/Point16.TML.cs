namespace Terraria.DataStructures;

public partial struct Point16
{
    public void Deconstruct(out int x, out int y)
    {
        x = X;
        y = Y;
    }
}
