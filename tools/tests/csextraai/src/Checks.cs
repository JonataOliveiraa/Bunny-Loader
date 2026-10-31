using System;
using System.IO;
using CsExtraAI.Content;
using log4net;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CsExtraAI;

public static class Checks
{
    const int HostNumber = 4242, ClientNumber = 777;

    static int pass, fail, frames, own = -1;
    static bool done, reported;

    static ILog Log => ModContent.GetInstance<CsExtraAIMod>().Logger;

    public static void Reset()
    {
        pass = fail = frames = 0;
        own = -1;
        done = reported = false;
    }

    public static void Frame(Player p)
    {
        if (done) return;
        frames++;
        try
        {
            if (Main.netMode == 0)
            {
                if (frames == 60) Local(p);
            }
            else Network(p, Main.netMode == 1);
        }
        catch (Exception e)
        {
            Report("run", false, e.ToString());
            done = true;
        }
        if (done)
        {
            if ((uint)own < 1000u && Main.projectile[own].active) Main.projectile[own].Kill();
            Log.Info($"csextraai END pass={pass} fail={fail}");
        }
    }

    static Projectile Spawn(Player p, Vector2 offset, int number, string text, bool first, bool second, float value)
    {
        int index = Projectile.NewProjectile(p.GetSource_FromThis(), p.Center + offset, Vector2.Zero, ModContent.ProjectileType<DataBolt>(), 0, 0f, p.whoAmI);
        Projectile bolt = Main.projectile[index];
        var data = (DataBolt)bolt.ModProjectile;
        data.Number = number;
        data.Text = text;
        BitsGlobal bits = bolt.GetGlobalProjectile<BitsGlobal>();
        bits.First = first;
        bits.Second = second;
        bits.Value = value;
        data.Dirty = true;
        return bolt;
    }

    static string Describe(Projectile bolt)
    {
        var data = (DataBolt)bolt.ModProjectile;
        BitsGlobal bits = bolt.GetGlobalProjectile<BitsGlobal>();
        return $"number={data.Number} text='{data.Text}' bits={bits.First},{bits.Second} value={bits.Value}";
    }

    static bool Matches(Projectile bolt, int number, string text, bool first, bool second, float value)
    {
        var data = (DataBolt)bolt.ModProjectile;
        BitsGlobal bits = bolt.GetGlobalProjectile<BitsGlobal>();
        return data.Number == number && data.Text == text && bits.First == first && bits.Second == second && bits.Value == value;
    }

    static void Local(Player p)
    {
        Projectile source = Spawn(p, new Vector2(0f, -80f), 42, "hello", true, false, 3.5f);
        byte[]? bytes = ProjectileLoader.WriteExtraAI(source);
        Report("WriteExtraAI lays out the global bits first", bytes != null && bytes[0] == 2 && bytes[1] == 1 && bytes.Length == 2 + 4 + 6 + 4,
            bytes == null ? "null" : BitConverter.ToString(bytes));

        Projectile target = Spawn(p, new Vector2(16f, -80f), 0, "", false, true, 0f);
        ProjectileLoader.ReceiveExtraAI(target, bytes!);
        Report("ReceiveExtraAI restores the ModProjectile and its global", Matches(target, 42, "hello", true, false, 3.5f), Describe(target));

        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, true)) ProjectileLoader.SendExtraAI(writer, bytes!);
        stream.Position = 0;
        using var reader = new BinaryReader(stream);
        byte[] read = ProjectileLoader.ReadExtraAI(reader);
        Report("SendExtraAI and ReadExtraAI frame the bytes", read.AsSpan().SequenceEqual(bytes) && stream.Position == stream.Length, $"read={read.Length} of {bytes!.Length}");

        int vanilla = Projectile.NewProjectile(p.GetSource_FromThis(), p.Center + new Vector2(0f, -120f), Vector2.Zero, ProjectileID.WoodenArrowFriendly, 0, 0f, p.whoAmI);
        Report("a projectile without extra AI writes nothing", ProjectileLoader.WriteExtraAI(Main.projectile[vanilla]) == null, $"index={vanilla}");
        Main.projectile[vanilla].Kill();

        ProjectileLoader.ReceiveExtraAI(target, new byte[] { 2, 3 });
        Report("a short payload is reported, not thrown", target.active, Describe(target));
        source.Kill();
        target.Kill();
        done = true;
    }

    static Projectile? FindOther(int owner)
    {
        for (int i = 0; i < 1000; i++)
        {
            Projectile bolt = Main.projectile[i];
            if (bolt.active && bolt.owner != owner && bolt.ModProjectile is DataBolt) return bolt;
        }
        return null;
    }

    static void Network(Player p, bool client)
    {
        if (frames == 120)
        {
            Projectile bolt = client ? Spawn(p, new Vector2(48f, -96f), ClientNumber, "from client", false, true, -2.25f)
                : Spawn(p, new Vector2(-48f, -96f), HostNumber, "from host", true, true, 8.5f);
            own = bolt.whoAmI;
            Log.Info($"csextraai spawned {(client ? "client" : "host")} bolt {Describe(bolt)}");
        }
        if ((uint)own < 1000u && Main.projectile[own].active && frames % 120 == 0 && Main.projectile[own].ModProjectile is DataBolt dirty) dirty.Dirty = true;
        if (!reported && frames >= 300 && frames % 60 == 0)
        {
            Projectile? other = FindOther(Main.myPlayer);
            bool ok = other is Projectile seen && (client ? Matches(seen, HostNumber, "from host", true, true, 8.5f) : Matches(seen, ClientNumber, "from client", false, true, -2.25f));
            if (ok || frames >= 3000)
            {
                Report(client ? "the client receives the host's extra AI" : "the host receives the client's extra AI", ok,
                    other is Projectile found ? Describe(found) : "no bolt from the other side");
                reported = true;
            }
        }
        if (reported && frames >= 3600) done = true;
    }

    static void Report(string name, bool ok, string detail)
    {
        if (ok) pass++;
        else fail++;
        Log.Info($"csextraai {name}: {(ok ? "ok" : "FAIL")} {detail}");
    }
}
