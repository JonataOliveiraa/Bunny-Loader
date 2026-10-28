using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
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
        string json = JsonSerializer.Serialize(sorted.Select(m => new[] { m.Name, m.Version.ToString() }).ToArray());
        string list = sorted.Length == 0 ? "-" : string.Join(", ", sorted.Select(m => $"{m.DisplayName} {m.Version}"));
        string refusal = LocalizationLoader.Pack(new[]
        {
            ("en-US", $"This server uses other C# mods: {list}. Install the same ones to join."),
            ("pt-BR", $"Este servidor usa outros mods C#: {list}. Instale os mesmos para entrar."),
        });
        byte* j = Host.Utf8(json), r = Host.Utf8(refusal);
        try { Host.Native()->NetHello(j, r); }
        finally
        {
            Host.FreeUtf8(j);
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
            if (reader.ReadByte() != KindPacket) return;
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
