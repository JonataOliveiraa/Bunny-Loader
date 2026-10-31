using System;
using System.Collections.Generic;
using System.IO;
using BunnyLoader;

namespace Terraria.ModLoader.Core;

internal static class NPCNetHooks
{
    public const byte KindExtraAI = 6;

    const long MaxAgeMs = 5000;

    internal sealed class Arriving
    {
        public int From;
        public int Index;
        public int Type;
        public byte[] Data = Array.Empty<byte>();
        public long Time;
    }

    static readonly object Gate = new();
    static readonly Dictionary<(int, int), Arriving> Pending = new();

    internal static void Install(NPCMethod methods)
    {
        if ((methods & NPCLoader.NetMethods) == 0) return;
        ItemHooks.Once("NPCExtraAI", () =>
        {
            ItemNetHooks.WantSendData();
            ItemNetHooks.WantProcessData();
            ItemNetHooks.SendingData += Sending;
        });
    }

    static void Sending(int msgType, int remote, int ignore, int number)
    {
        if (msgType != 23 || (ModNet.NetMode & 2) == 0 || (uint)number >= 200u) return;
        NPC npc = Main.npc[number];
        if (!npc.active || !NPCLoader.HasHooks(npc, NPCMethod.SendExtraAI)) return;
        byte[]? extraAI = NPCLoader.WriteExtraAI(npc);
        if (extraAI == null) return;
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write(KindExtraAI);
        writer.Write((short)number);
        writer.Write((short)npc.type);
        NPCLoader.SendExtraAI(writer, extraAI);
        writer.Flush();
        byte[] packet = stream.ToArray();
        ModNet.Send(packet, packet.Length, remote, ignore);
    }

    internal static void Receive(BinaryReader reader, int from)
    {
        var arriving = new Arriving
        {
            From = from,
            Index = reader.ReadInt16(),
            Type = reader.ReadInt16(),
            Time = Environment.TickCount64,
        };
        arriving.Data = NPCLoader.ReadExtraAI(reader);
        if ((uint)arriving.Index >= 200u) return;
        lock (Gate)
        {
            if (Pending.Count > 64)
            {
                var stale = new List<(int, int)>();
                foreach (var (id, entry) in Pending)
                    if (arriving.Time - entry.Time > MaxAgeMs) stale.Add(id);
                foreach (var id in stale) Pending.Remove(id);
            }
            Pending[(from, arriving.Index)] = arriving;
        }
    }

    internal static Arriving? Take(int from, int index)
    {
        lock (Gate)
        {
            if (Pending.Count == 0 || !Pending.Remove((from, index), out Arriving? arriving)) return null;
            return arriving;
        }
    }

    internal static void Apply(Arriving arriving)
    {
        NPC npc = Main.npc[arriving.Index];
        if (npc.active && npc.type == arriving.Type) NPCLoader.ReceiveExtraAI(npc, arriving.Data);
    }

    internal static void Forget(int from)
    {
        lock (Gate)
        {
            var gone = new List<(int, int)>();
            foreach (var id in Pending.Keys)
                if (id.Item1 == from) gone.Add(id);
            foreach (var id in gone) Pending.Remove(id);
        }
    }
}
