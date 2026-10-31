using System;
using System.Collections.Generic;
using BunnyLoader.Interop;
using Terraria.ModLoader;

namespace Terraria.ID;

public readonly partial struct NPCID
{
    public static partial class Sets
    {
        public static BestiaryDrawOffsets NPCBestiaryDrawOffset { get; } = new();

        public partial struct NPCBestiaryDrawModifiers
        {
            public NPCBestiaryDrawModifiers()
            {
                Scale = 1f;
                PortraitScale = 1f;
            }
        }
    }

    public sealed class BestiaryDrawOffsets
    {
        readonly Dictionary<int, string> modTextures = new();

        static GameDictionary Table => new(Sets.NPCBestiaryDrawOffset_);

        public bool ContainsKey(int npcNetId) => Table.ContainsKey(npcNetId);

        public bool TryGetValue(int npcNetId, out Sets.NPCBestiaryDrawModifiers value)
        {
            if (!Table.TryGetValue(npcNetId, out value)) return false;
            lock (modTextures)
                if (modTextures.TryGetValue(npcNetId, out string? texture)) value.CustomTexturePath = texture;
            return true;
        }

        public Sets.NPCBestiaryDrawModifiers this[int npcNetId]
        {
            get => TryGetValue(npcNetId, out var value) ? value : throw new KeyNotFoundException($"NPCBestiaryDrawOffset has no entry for NPC {npcNetId}");
            set
            {
                string? texture = value.CustomTexturePath;
                lock (modTextures)
                {
                    if (texture != null && !texture.StartsWith("Images/", StringComparison.Ordinal))
                    {
                        modTextures[npcNetId] = texture;
                        value.CustomTexturePath = default;
                    }
                    else modTextures.Remove(npcNetId);
                }
                Table.Set(npcNetId, value);
            }
        }

        public void Add(int npcNetId, Sets.NPCBestiaryDrawModifiers value)
        {
            if (ContainsKey(npcNetId)) throw new ArgumentException($"NPCBestiaryDrawOffset already has an entry for NPC {npcNetId}");
            this[npcNetId] = value;
        }

        public bool Remove(int npcNetId)
        {
            lock (modTextures) modTextures.Remove(npcNetId);
            return Table.Remove(npcNetId);
        }

        internal string? ModTexture(int npcNetId)
        {
            lock (modTextures) return modTextures.TryGetValue(npcNetId, out string? texture) ? texture : null;
        }
    }
}
