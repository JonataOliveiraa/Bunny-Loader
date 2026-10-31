using System;
using System.IO;
using BunnyLoader;

namespace Terraria.ModLoader.Core;

internal static class ProjectileNetHooks
{
    public const byte KindExtraAI = 5;

    internal static void Install(Func<string, bool> has)
    {
        if (!has(nameof(ModProjectile.SendExtraAI)) && !has(nameof(ModProjectile.ReceiveExtraAI))) return;
        ItemHooks.Once("ProjectileExtraAI", () =>
        {
            ItemNetHooks.WantSendData();
            ItemNetHooks.AfterSend += AfterSend;
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

    static void AfterSend(int msgType, int remote, int ignore, int number)
    {
        if (msgType != 27 || ModNet.NetMode == 0 || (uint)number >= 1000u) return;
        Projectile projectile = Main.projectile[number];
        if (!projectile.active) return;
        if (Server && projectile.owner != Main.myPlayer && projectile.owner != 255) return;
        if (!ProjectileHooks.Wants(projectile, nameof(ModProjectile.SendExtraAI))) return;
        if (ProjectileLoader.WriteExtraAI(projectile) is not byte[] extraAI) return;
        byte[] packet = Packet(projectile.key.bits, projectile.type, extraAI);
        ModNet.Send(packet, packet.Length, remote, ignore);
    }

    internal static void Receive(BinaryReader reader, int from)
    {
        uint key = reader.ReadUInt32();
        int type = reader.ReadInt16();
        byte[] extraAI = ProjectileLoader.ReadExtraAI(reader);
        if (Find(key, type) is Projectile projectile) ProjectileLoader.ReceiveExtraAI(projectile, extraAI);
        if (!Server || from >= 256) return;
        byte[] packet = Packet(key, type, extraAI);
        ModNet.Send(packet, packet.Length, -1, from);
    }

    static Projectile? Find(uint key, int type)
    {
        for (int i = 0; i < 1000; i++)
        {
            Projectile p = Main.projectile[i];
            if (p.active && p.type == type && p.key.bits == key) return p;
        }
        return null;
    }
}
