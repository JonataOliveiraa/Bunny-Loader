using System;
using System.Collections.Generic;
using BunnyLoader.Interop;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria.ModLoader;

namespace Terraria.ID;

public readonly partial struct NPCID
{
    public static partial class Sets
    {
        public static PortraitTable NPCPortraits { get; } = new();

        public static PortraitOffsetTable NPCPortraitsCloseUpOffsets { get; } = new(() => NPCPortraitsCloseUpOffsets_);

        public static PortraitOffsetTable NPCPortraitsFullBodyRetroOffsets { get; } = new(() => NPCPortraitsFullBodyRetroOffsets_);

        public static BasicNPCPortrait BasicPortrait(string texturePath)
        {
            BasicNPCPortrait portrait = BasicPortrait_(texturePath);
            if (!texturePath.StartsWith("Images/", StringComparison.Ordinal)) portrait._image = ModContent.Request<Texture2D>(texturePath);
            return portrait;
        }
    }

    public sealed class PortraitTable
    {
        static GameDictionary Table => new(Sets.NPCPortraits_);

        public bool ContainsKey(int npcNetId) => Table.ContainsKey(npcNetId);

        public bool TryGetValue(int npcNetId, out Sets.NPCPortraitProvider value) => Table.TryGetValue(npcNetId, out value);

        public Sets.NPCPortraitProvider this[int npcNetId]
        {
            get => TryGetValue(npcNetId, out var value) ? value : throw new KeyNotFoundException($"NPCPortraits has no entry for NPC {npcNetId}");
            set => Table.Set(npcNetId, value);
        }

        public void Add(int npcNetId, Sets.NPCPortraitProvider value)
        {
            if (ContainsKey(npcNetId)) throw new ArgumentException($"NPCPortraits already has an entry for NPC {npcNetId}");
            this[npcNetId] = value;
        }

        public bool Remove(int npcNetId) => Table.Remove(npcNetId);
    }

    public sealed class PortraitOffsetTable
    {
        readonly Func<GameObject> dictionary;

        internal PortraitOffsetTable(Func<GameObject> dictionary) => this.dictionary = dictionary;

        GameDictionary Table => new(dictionary());

        public bool ContainsKey(int npcNetId) => Table.ContainsKey(npcNetId);

        public bool TryGetValue(int npcNetId, out Vector2 value) => Table.TryGetValue(npcNetId, out value);

        public Vector2 this[int npcNetId]
        {
            get => TryGetValue(npcNetId, out var value) ? value : throw new KeyNotFoundException($"portrait offsets have no entry for NPC {npcNetId}");
            set => Table.Set(npcNetId, value);
        }

        public void Add(int npcNetId, Vector2 value)
        {
            if (ContainsKey(npcNetId)) throw new ArgumentException($"portrait offsets already have an entry for NPC {npcNetId}");
            this[npcNetId] = value;
        }

        public bool Remove(int npcNetId) => Table.Remove(npcNetId);
    }
}
