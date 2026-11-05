namespace Terraria.DataStructures;

public readonly struct EntitySource_TileUpdate(int x, int y)
{
    public static implicit operator IEntitySource(EntitySource_TileUpdate source) => EntitySource_TileBreak.New(source.X, source.Y);

    public int X => x;
    public int Y => y;
}
