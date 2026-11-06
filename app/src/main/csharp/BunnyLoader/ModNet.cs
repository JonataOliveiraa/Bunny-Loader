using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System;
using Terraria.ModLoader.Core;
using Terraria.ModLoader;

namespace BunnyLoader;

internal static unsafe class ModNet
{
    public const byte KindPacket = 1;

    public static int NetMode => Host.Native()->NetMode();

    public static void Install() => Host.Native()->NetInstall(&Receive);

    public static bool Send(byte[] buffer, int length, int toClient, int ignoreClient)
    {
        fixed (byte* data = buffer) return Host.Native()->NetSend(data, length, toClient, ignoreClient) != 0;
    }

    public static void SetHello(string hello)
    {
        byte* h = Host.Utf8(hello);
        try { Host.Native()->NetHello(h, null); }
        finally { Host.FreeUtf8(h); }
    }

    [UnmanagedCallersOnly]
    static void Receive(byte* data, int length, int from)
    {
        string name = "?";
        try
        {
            using var stream = new UnmanagedMemoryStream(data, length);
            using var reader = new BinaryReader(stream);
            byte kind = reader.ReadByte();
            if (kind == ItemNetHooks.KindItem)
            {
                name = "item data";
                ItemNetHooks.Queue(reader, from);
                return;
            }
            if (kind == ItemPvpHooks.KindPvp)
            {
                name = "PvP item";
                ItemPvpHooks.Queue(reader, from);
                return;
            }
            if (kind == ProjectileNetHooks.KindExtraAI)
            {
                name = "projectile extra AI";
                ProjectileNetHooks.Receive(reader, from);
                return;
            }
            if (kind == NPCNetHooks.KindExtraAI)
            {
                name = "NPC extra AI";
                NPCNetHooks.Receive(reader, from);
                return;
            }
            if (kind == BiomeHooks.KindBiomes)
            {
                name = "biomes";
                BiomeHooks.Receive(reader, from);
                return;
            }
            if (kind == SystemNetHooks.KindWorld)
            {
                name = "world data";
                SystemNetHooks.Receive(reader, from);
                return;
            }
            if (kind == TileEntityHooks.KindTileEntity)
            {
                name = "tile entities";
                TileEntityHooks.Receive(reader, from);
                return;
            }
            if (kind == ChestHooks.KindChest)
            {
                name = "chests";
                ChestHooks.Receive(reader, from);
                return;
            }
            if (kind == HairHooks.KindHair)
            {
                name = "hair";
                HairHooks.Receive(reader, from);
                return;
            }
            if (kind == ServerSyncHooks.KindSyncPlan)
            {
                name = "server sync";
                ServerSyncHooks.Receive(reader, from);
                return;
            }
            if (kind == ConfigNetHooks.KindConfig)
            {
                name = "configs";
                ConfigNetHooks.Receive(reader, from);
                return;
            }
            if (kind == InvasionLoader.KindInvasion)
            {
                name = "invasion";
                InvasionLoader.Receive(reader, from);
                return;
            }
            if (kind != KindPacket) return;
            name = reader.ReadString();
            Mod? mod = Host.FindMod(name);
            if (mod == null)
            {
                Log.Warn($"net: a packet for the C# mod '{name}', which is not loaded here");
                return;
            }
            mod.HandlePacket(reader, from);
        }
        catch (Exception e)
        {
            Log.Error($"net: {name}.HandlePacket threw: {e}");
        }
    }
}
