using System;
using System.Collections.Generic;
using System.IO;
using BunnyLoader;

namespace Terraria.ModLoader.Core;

internal static class ProjectileNetHooks
{
    public const byte KindExtraAI = 5;

    const long MaxAgeMs = 5000;

    internal sealed class Arriving
    {
        public int From;
        public uint Key;
        public int Type;
        public byte[] Data = Array.Empty<byte>();
        public long Time;
    }

    static readonly object Gate = new();
    static readonly Dictionary<(int, uint), Arriving> Pending = new();

    internal static Arriving? Receiving;

    internal static void Install(Func<string, bool> has)
    {
        if (!has(nameof(ModProjectile.SendExtraAI)) && !has(nameof(ModProjectile.ReceiveExtraAI))) return;
        ItemHooks.Once("ProjectileExtraAI", () =>
        {
            ItemNetHooks.WantSendData();
            ItemNetHooks.WantProcessData();
            ItemNetHooks.SendingData += Sending;
        });
    }

    static bool Server => (ModNet.NetMode & 2) != 0;

    static byte[] Packet(uint key, int type, byte[] extraAI)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write(KindExtraAI);
        writer.Write(key);
        writer.Write((short)type);
        ProjectileLoader.SendExtraAI(writer, extraAI);
        writer.Flush();
        return stream.ToArray();
    }

    static void Sending(int msgType, int remote, int ignore, int number)
    {
        if (msgType != 27 || ModNet.NetMode == 0 || (uint)number >= 1000u) return;
        Projectile projectile = Main.projectile[number];
        if (!projectile.active) return;
        uint key = projectile.key.bits;
        byte[]? extraAI;
        if (Server && Receiving is Arriving relay && relay.Key == key && relay.Type == projectile.type) extraAI = relay.Data;
        else
        {
            if (!Server && projectile.owner != Main.myPlayer) return;
            if (!ProjectileHooks.Wants(projectile, nameof(ModProjectile.SendExtraAI))) return;
            extraAI = ProjectileLoader.WriteExtraAI(projectile);
        }
        if (extraAI == null) return;
        byte[] packet = Packet(key, projectile.type, extraAI);
        ModNet.Send(packet, packet.Length, remote, ignore);
    }

    internal static void Receive(BinaryReader reader, int from)
    {
        var arriving = new Arriving
        {
            From = from,
            Key = reader.ReadUInt32(),
            Type = reader.ReadInt16(),
            Time = Environment.TickCount64,
        };
        arriving.Data = ProjectileLoader.ReadExtraAI(reader);
        lock (Gate)
        {
            if (Pending.Count > 32)
            {
                var stale = new List<(int, uint)>();
                foreach (var (id, entry) in Pending)
                    if (arriving.Time - entry.Time > MaxAgeMs) stale.Add(id);
                foreach (var id in stale) Pending.Remove(id);
            }
            Pending[(from, arriving.Key)] = arriving;
        }
    }

    internal static Arriving? Take(int from, uint key)
    {
        lock (Gate)
        {
            if (Pending.Count == 0 || !Pending.Remove((from, key), out Arriving? arriving)) return null;
            return arriving;
        }
    }

    internal static void Apply(Arriving arriving)
    {
        for (int i = 0; i < 1000; i++)
        {
            Projectile p = Main.projectile[i];
            if (!p.active || p.type != arriving.Type || p.key.bits != arriving.Key) continue;
            ProjectileLoader.ReceiveExtraAI(p, arriving.Data);
            return;
        }
    }

    internal static void Forget(int from)
    {
        lock (Gate)
        {
            var gone = new List<(int, uint)>();
            foreach (var id in Pending.Keys)
                if (id.Item1 == from) gone.Add(id);
            foreach (var id in gone) Pending.Remove(id);
        }
    }
}
