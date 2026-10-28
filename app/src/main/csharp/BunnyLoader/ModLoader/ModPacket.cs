using System;
using System.IO;
using BunnyLoader;

namespace Terraria.ModLoader;

public sealed class ModPacket : BinaryWriter
{
    public const int MaxLength = 65000;

    readonly Mod mod;

    internal ModPacket(Mod mod, int capacity) : base(new MemoryStream(Math.Max(capacity, 16)))
    {
        this.mod = mod;
        Write(ModNet.KindPacket);
        Write(mod.Name);
    }

    public void Send(int toClient = -1, int ignoreClient = -1)
    {
        if (ModNet.NetMode == 0) throw new InvalidOperationException($"{mod.Name}: packets cannot be sent in single player");
        Flush();
        var stream = (MemoryStream)OutStream;
        if (stream.Length > MaxLength) throw new InvalidOperationException($"{mod.Name}: the packet has {stream.Length} bytes; the limit is {MaxLength}");
        if (!ModNet.Send(stream.GetBuffer(), (int)stream.Length, toClient, ignoreClient))
            Log.Error($"mod {mod.Uid}: the packet was not sent");
    }
}
