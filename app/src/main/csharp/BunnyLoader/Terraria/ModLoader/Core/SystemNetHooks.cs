using System;
using System.IO;
using System.Text;
using BunnyLoader;
using BunnyLoader.Interop;
using Terraria.Localization;

namespace Terraria.ModLoader.Core;

internal static unsafe class SystemNetHooks
{
    public const byte KindWorld = 7;

    const int WorldData = 7;

    static readonly GameClass ByteClass = new("System", "Byte");

    static bool hijackSend, hijackGet;
    static byte[]? pendingWorld;
    static readonly object Gate = new();

    internal static void Install(Func<string, bool> has)
    {
        if (has(nameof(ModSystem.NetSend)) || has(nameof(ModSystem.NetReceive)))
            ItemHooks.Once("SystemNetWorld", () =>
            {
                ItemNetHooks.WantSendData();
                ItemNetHooks.WantProcessData();
                ItemNetHooks.SendingData += Sending;
            });
        if (has(nameof(ModSystem.HijackSendData)))
            ItemHooks.Once("SystemHijackSend", () =>
            {
                hijackSend = true;
                ItemNetHooks.WantSendData();
            });
        if (has(nameof(ModSystem.HijackGetData)))
            ItemHooks.Once("SystemHijackGet", () =>
            {
                hijackGet = true;
                ItemNetHooks.WantProcessData();
            });
    }

    internal static byte[]? WriteWorld()
    {
        ModSystem[] systems = SystemLoader.NetSystems();
        if (systems.Length == 0) return null;
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write(KindWorld);
        writer.Write(systems.Length);
        foreach (ModSystem system in systems)
        {
            using var data = new MemoryStream();
            using (var inner = new BinaryWriter(data, Encoding.UTF8, true)) SystemLoader.NetSend(system, inner);
            writer.Write(SystemLoader.KeyOf(system));
            writer.Write7BitEncodedInt((int)data.Length);
            writer.Write(data.GetBuffer(), 0, (int)data.Length);
        }
        writer.Flush();
        return stream.ToArray();
    }

    internal static void ReadWorld(BinaryReader reader)
    {
        int count = reader.ReadInt32();
        for (int i = 0; i < count; i++)
        {
            string key = reader.ReadString();
            byte[] data = reader.ReadBytes(reader.Read7BitEncodedInt());
            ModSystem? system = SystemLoader.ByKey(key);
            if (system == null) continue;
            using var inner = new BinaryReader(new MemoryStream(data, false));
            SystemLoader.NetReceive(system, inner);
        }
    }

    static void Sending(int msgType, int remote, int ignore, int number)
    {
        if (msgType != WorldData || (ModNet.NetMode & 2) == 0) return;
        byte[]? packet = WriteWorld();
        if (packet != null) ModNet.Send(packet, packet.Length, remote, ignore);
    }

    internal static void Receive(BinaryReader reader, int from)
    {
        if (from != 256) return;
        byte[] rest = reader.ReadBytes((int)(reader.BaseStream.Length - reader.BaseStream.Position));
        lock (Gate) pendingWorld = rest;
    }

    internal static void AfterWorldData()
    {
        byte[]? data;
        lock (Gate)
        {
            data = pendingWorld;
            pendingWorld = null;
        }
        if (data == null) return;
        using var reader = new BinaryReader(new MemoryStream(data, false));
        ReadWorld(reader);
    }

    internal static bool HijackSend(int msgType, int remote, int ignore, IntPtr text, int number, float number2, float number3, float number4, int number5, int number6, int number7)
    {
        if (!hijackSend) return false;
        int netMode = ModNet.NetMode;
        if (netMode == 0) return false;
        int whoAmI = (netMode & 2) != 0 && remote >= 0 ? remote : 256;
        return SystemLoader.HijackSendData(whoAmI, msgType, remote, ignore, new NetworkText(text), number, number2, number3, number4, number5, number6, number7);
    }

    internal static bool HijackGet(int from, IntPtr data, int length, IntPtr typeOut, out GameArray<byte> replacement)
    {
        replacement = default;
        if (!hijackGet || data == IntPtr.Zero || length < 1) return false;
        var array = new GameArray<byte>(data);
        if (length > array.Length) return false;
        byte* bytes = (byte*)(data + 0x20);
        var copy = new byte[length];
        new ReadOnlySpan<byte>(bytes, length).CopyTo(copy);
        var original = new BinaryReader(new MemoryStream(copy, 0, length, false, true));
        original.BaseStream.Position = 1;
        byte type = copy[0];
        BinaryReader reader = original;
        if (SystemLoader.HijackGetData(ref type, ref reader, from))
        {
            *(int*)typeOut = type;
            return true;
        }
        if (reader != original)
        {
            byte[] payload = reader.ReadBytes(65534);
            replacement = GameArray<byte>.New(ByteClass, payload.Length + 1);
            byte* target = (byte*)(replacement.Pointer + 0x20);
            target[0] = type;
            payload.CopyTo(new Span<byte>(target + 1, payload.Length));
        }
        else if (type != copy[0])
        {
            replacement = GameArray<byte>.New(ByteClass, length);
            byte* target = (byte*)(replacement.Pointer + 0x20);
            copy.CopyTo(new Span<byte>(target, length));
            target[0] = type;
        }
        return false;
    }
}
