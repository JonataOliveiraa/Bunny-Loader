using System;
using System.Collections.Generic;
using System.IO;
using BunnyLoader;

namespace Terraria.ModLoader;

public static unsafe class NPCHeadLoader
{
    static readonly Dictionary<string, int> Slots = new(StringComparer.Ordinal);
    static readonly Dictionary<int, int> Defaults = new();
    static int extras;

    internal static void Register(ModNPC npc)
    {
        if (Attribute.IsDefined(npc.GetType(), typeof(AutoloadHead)))
            Defaults[npc.Type] = Reserve(npc.Type, 0, ItemLoader.ResolveTexture(npc, npc.HeadTexture), npc.HeadTexture, npc.FullName);
    }

    internal static int Add(Mod mod, int npcType, string texture) =>
        Slots.TryGetValue(texture, out int slot) ? slot : Reserve(npcType, ++extras, File(mod, texture), texture, mod.Name);

    static string File(Mod mod, string texture)
    {
        string clean = texture.Replace('\\', '/');
        if (clean.StartsWith(mod.Name + "/", StringComparison.Ordinal)) clean = clean[(mod.Name.Length + 1)..];
        if (!clean.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) clean += ".png";
        string? file = mod.ResolveFile(clean);
        return file != null && System.IO.File.Exists(file) ? file : throw new FileNotFoundException($"{mod.Name}: NPC head texture '{texture}' not found", texture);
    }

    static int Reserve(int type, int variant, string file, string texture, string owner)
    {
        byte* path = Host.Utf8(file), asset = Host.Utf8(texture);
        try
        {
            int slot = Host.Native()->ReserveTownHead(type, variant, path, asset);
            if (slot <= 0)
                throw new InvalidOperationException($"{owner}: town NPC head could not be registered");
            Slots[texture] = slot;
            return slot;
        }
        finally { Host.FreeUtf8(path); Host.FreeUtf8(asset); }
    }

    public static int GetHeadSlot(string texture) => Slots.TryGetValue(texture, out int slot) ? slot : -1;

    public static int GetNPCHeadSlot(int type) => Defaults.TryGetValue(type, out int slot) ? slot : -1;
}
