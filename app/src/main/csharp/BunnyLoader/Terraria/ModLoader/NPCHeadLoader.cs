using System;
using System.Collections.Generic;
using BunnyLoader;

namespace Terraria.ModLoader;

public static unsafe class NPCHeadLoader
{
    static readonly Dictionary<string, int> Types = new(StringComparer.Ordinal);

    internal static void Register(ModNPC npc)
    {
        if (!Attribute.IsDefined(npc.GetType(), typeof(AutoloadHead))) return;
        string texture = npc.HeadTexture;
        string file = ItemLoader.ResolveTexture(npc, texture);
        byte* path = Host.Utf8(file), asset = Host.Utf8(texture);
        try
        {
            if (Host.Native()->ReserveTownHead(npc.Type, 0, path, asset) == 0)
                throw new InvalidOperationException($"{npc.FullName}: town NPC head could not be registered");
            Types[texture] = npc.Type;
        }
        finally { Host.FreeUtf8(path); Host.FreeUtf8(asset); }
    }

    public static int GetHeadSlot(string texture) => Types.TryGetValue(texture, out int type) ? GetNPCHeadSlot(type) : -1;

    public static int GetNPCHeadSlot(int type) => Host.Native()->TownHeadSlot(type, 0);
}
