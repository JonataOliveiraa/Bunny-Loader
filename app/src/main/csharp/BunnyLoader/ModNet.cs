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

    public static void SetHello(Mod[] mods)
    {
        Mod[] sorted = mods.OrderBy(m => m.Name, StringComparer.Ordinal).ToArray();
        string hello = string.Concat(sorted.Select(m => $"\n{m.Name}\t{m.Version}").Prepend("1"));
        string list = sorted.Length == 0 ? "-" : string.Join(", ", sorted.Select(m => $"{m.DisplayName} {m.Version}"));
        string refusal = LocalizationLoader.Pack(new[]
        {
            ("en-US", $"This server uses other C# mods: {list}.\u001D Missing here: {{0}}.\u001D Not on this server: {{0}}.\u001D Install the same ones to join."),
            ("pt-BR", $"Este servidor usa outros mods C#: {list}.\u001D Faltam aqui: {{0}}.\u001D Não estão no servidor: {{0}}.\u001D Instale os mesmos para entrar."),
        });
        byte* h = Host.Utf8(hello), r = Host.Utf8(refusal);
        try { Host.Native()->NetHello(h, r); }
        finally
        {
            Host.FreeUtf8(h);
            Host.FreeUtf8(r);
        }
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
            if (kind == HairHooks.KindHair)
            {
                name = "hair";
                HairHooks.Receive(reader, from);
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
