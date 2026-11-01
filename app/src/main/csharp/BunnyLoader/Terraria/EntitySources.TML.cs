using Terraria.DataStructures;

namespace Terraria.DataStructures
{
    public readonly partial struct EntitySource_TileBreak
    {
        public EntitySource_TileBreak(int tileCoordsX, int tileCoordsY, string? context = null) : this(New(tileCoordsX, tileCoordsY).Pointer) { }
    }
}

namespace Terraria
{
    public readonly partial struct Player
    {
        public IEntitySource GetSource_FromThis(string? context = null) => EntitySource_Parent.New(this);
    }

    public readonly partial struct NPC
    {
        public IEntitySource GetSource_FromThis(string? context = null) => EntitySource_Parent.New(this);
    }

    public readonly partial struct Projectile
    {
        public IEntitySource GetSource_FromThis(string? context = null) => EntitySource_Parent.New(this);
    }
}
