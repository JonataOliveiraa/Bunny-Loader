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

        public IEntitySource GetSource_Buff(int buffIndex) => GetProjectileSource_Buff(buffIndex);

        public IEntitySource GetSource_Accessory(Item item, string? context = null) => GetProjectileSource_Accessory(item);

        public IEntitySource GetSource_Accessory_OnHurt(Item item, Entity attacker, string? context = null) => GetProjectileSource_Accessory(item);

        public IEntitySource GetSource_Accessory_OnHurt(Item item, PlayerDeathReason damageSource, string? context = null) => GetProjectileSource_Accessory(item);
    }

    public readonly partial struct NPC
    {
        public IEntitySource GetSource_FromThis(string? context = null) => EntitySource_Parent.New(this);

        public IEntitySource GetSource_Loot(string? context = null) => GetItemSource_Loot();
    }

    public readonly partial struct Projectile
    {
        public IEntitySource GetSource_FromThis(string? context = null) => EntitySource_Parent.New(this);
    }
}
