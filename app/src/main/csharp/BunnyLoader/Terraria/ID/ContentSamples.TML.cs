using System.Collections.Generic;
using BunnyLoader.Interop;
using Terraria.ModLoader;

namespace Terraria.ID;

public static partial class ContentSamples
{
    public static BestiaryRarityStars NpcBestiaryRarityStars { get; } = new();

    public static ProjectileSamples ProjectilesByType { get; } = new();

    public static ItemSamples ItemsByType { get; } = new();

    public sealed class ItemSamples
    {
        readonly Dictionary<int, Item> samples = new();

        public Item this[int type] => TryGetValue(type, out Item sample) ? sample : throw new KeyNotFoundException($"ItemsByType has no item {type}");

        public bool ContainsKey(int type) => type >= 0 && type < ItemLoader.ItemCount;

        public bool TryGetValue(int type, out Item sample)
        {
            sample = default;
            if (!ContainsKey(type)) return false;
            lock (samples)
            {
                if (samples.TryGetValue(type, out sample)) return true;
                sample = new Item(type);
                samples[type] = sample;
                return true;
            }
        }
    }

    public sealed class ProjectileSamples
    {
        readonly Dictionary<int, Projectile> samples = new();
        readonly List<GameHandle> kept = new();

        public Projectile this[int type] => TryGetValue(type, out Projectile sample) ? sample : throw new KeyNotFoundException($"ProjectilesByType has no projectile {type}");

        public bool ContainsKey(int type) => type > 0 && type < ProjectileLoader.ProjectileCount;

        public bool TryGetValue(int type, out Projectile sample)
        {
            sample = default;
            if (!ContainsKey(type)) return false;
            lock (samples)
            {
                if (samples.TryGetValue(type, out sample)) return true;
                sample = Projectile.New();
                kept.Add(new GameHandle(sample.Pointer));
                sample.SetDefaults(type);
                samples[type] = sample;
                return true;
            }
        }
    }

    public sealed class BestiaryRarityStars
    {
        readonly Dictionary<int, int> assigned = new();

        static GameDictionary Table => new(NpcBestiaryRarityStars_);

        public int this[int npcNetId]
        {
            get => Table.TryGetValue(npcNetId, out int stars) ? stars : throw new KeyNotFoundException($"NpcBestiaryRarityStars has no entry for NPC {npcNetId}");
            set
            {
                lock (assigned) assigned[npcNetId] = value;
                Table.Set(npcNetId, value);
            }
        }

        public bool ContainsKey(int npcNetId) => Table.ContainsKey(npcNetId);

        public bool TryGetValue(int npcNetId, out int stars) => Table.TryGetValue(npcNetId, out stars);

        internal void Reapply(int npcNetId)
        {
            int stars;
            lock (assigned)
                if (!assigned.TryGetValue(npcNetId, out stars)) return;
            Table.Set(npcNetId, stars);
        }
    }
}
