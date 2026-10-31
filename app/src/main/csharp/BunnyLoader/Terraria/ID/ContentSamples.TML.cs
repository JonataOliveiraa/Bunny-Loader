using System.Collections.Generic;
using BunnyLoader.Interop;

namespace Terraria.ID;

public static partial class ContentSamples
{
    public static BestiaryRarityStars NpcBestiaryRarityStars { get; } = new();

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
