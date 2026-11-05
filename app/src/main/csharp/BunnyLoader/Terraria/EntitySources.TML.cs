using Terraria.DataStructures;

namespace Terraria.DataStructures
{
    public readonly partial struct EntitySource_ShakeTree
    {
        public EntitySource_ShakeTree(int x, int y) : this(New(x, y).Pointer)
        {
        }
    }

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

        public IEntitySource GetSource_Death(string? context = null) => EntitySource_Parent.New(this);

        public IEntitySource GetSource_FromAI(string? context = null) => EntitySource_Parent.New(this);

        public IEntitySource GetSource_GiftOrReward(string? context = null) => EntitySource_Parent.New(this);
    }

    public readonly partial struct Entity
    {
        public static IEntitySource GetSource_TownSpawn() => EntitySource_SpawnNPC.New();

        public static IEntitySource GetSource_NaturalSpawn() => EntitySource_SpawnNPC.New();
    }

    public readonly partial struct Projectile
    {
        public IEntitySource GetSource_FromThis(string? context = null) => EntitySource_Parent.New(this);
    }
}
