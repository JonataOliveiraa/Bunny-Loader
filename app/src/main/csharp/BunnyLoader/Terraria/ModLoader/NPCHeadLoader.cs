using System;
using System.Collections.Generic;
using System.IO;
using BunnyLoader;

namespace Terraria.ModLoader;

public static unsafe class NPCHeadLoader
{
    static readonly Dictionary<string, (int Type, int Variant)> Types = new(StringComparer.Ordinal);
    static readonly List<(Mod Mod, string Texture)> Pending = new();

    internal static void Register(ModNPC npc)
    {
        if (Attribute.IsDefined(npc.GetType(), typeof(AutoloadHead)))
            Reserve(npc.Type, 0, ItemLoader.ResolveTexture(npc, npc.HeadTexture), npc.HeadTexture, npc.FullName);
        for (int i = Pending.Count - 1; i >= 0; i--)
        {
            if (Pending[i].Mod != npc.Mod) continue;
            Reserve(npc.Type, 1, File(Pending[i].Mod, Pending[i].Texture), Pending[i].Texture, npc.FullName);
            Pending.RemoveAt(i);
        }
    }

    internal static int Add(Mod mod, int npcType, string texture)
    {
        if (NPCLoader.GetNPC(npcType) is ModNPC npc && npc.Type == npcType)
        {
            Reserve(npcType, 1, File(mod, texture), texture, npc.FullName);
            return GetHeadSlot(texture);
        }
        File(mod, texture);
        Pending.Add((mod, texture));
        return -1;
    }

    static string File(Mod mod, string texture)
    {
        string clean = texture.Replace('\\', '/');
        if (clean.StartsWith(mod.Name + "/", StringComparison.Ordinal)) clean = clean[(mod.Name.Length + 1)..];
        if (!clean.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) clean += ".png";
        string? file = mod.ResolveFile(clean);
        return file != null && System.IO.File.Exists(file) ? file : throw new FileNotFoundException($"{mod.Name}: NPC head texture '{texture}' not found", texture);
    }

    static void Reserve(int type, int variant, string file, string texture, string owner)
    {
        byte* path = Host.Utf8(file), asset = Host.Utf8(texture);
        try
        {
            if (Host.Native()->ReserveTownHead(type, variant, path, asset) == 0)
                throw new InvalidOperationException($"{owner}: town NPC head could not be registered");
            Types[texture] = (type, variant);
        }
        finally { Host.FreeUtf8(path); Host.FreeUtf8(asset); }
    }

    public static int GetHeadSlot(string texture) => Types.TryGetValue(texture, out var head) ? Host.Native()->TownHeadSlot(head.Type, head.Variant) : -1;

    public static int GetNPCHeadSlot(int type) => Host.Native()->TownHeadSlot(type, 0);
}
