using System;
using System.Collections.Generic;
using log4net;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;
using Terraria.ModLoader.Config;

namespace CsIssuesMp;

public sealed class BigConfig : ModConfig
{
    public const int Count = 2500;

    public static BigConfig Instance = null!;

    public override ConfigScope Mode => ConfigScope.ServerSide;

    public int Marker;

    public List<string> Entries = new();

    public static string Entry(int i) => $"entry-{i:D5}-" + new string('x', 40);
}

public sealed class HurtPlayer : ModPlayer
{
    public static int LocalModify, RemoteModify, LocalHurts, RemoteHurts, LocalDamage, RemoteDamage;

    public override void ModifyHurt(ref Player.HurtModifiers modifiers)
    {
        if (Player.whoAmI == Main.myPlayer) LocalModify++;
        else RemoteModify++;
        modifiers.SourceDamage *= 2f;
    }

    public override void OnHurt(Player.HurtInfo info)
    {
        if (Player.whoAmI == Main.myPlayer)
        {
            LocalHurts++;
            LocalDamage = info.Damage;
        }
        else
        {
            RemoteHurts++;
            RemoteDamage = info.Damage;
        }
    }
}

public sealed class CsIssuesMpMod : Mod
{
    const int Timeout = 2400;

    static int frames, pass, fail, step, since;
    static bool done;

    static ILog Log => ModContent.GetInstance<CsIssuesMpMod>().Logger;

    public override void Load() => On_Player.Update += Update;

    static void Report(string name, bool ok, string detail)
    {
        if (ok) pass++;
        else fail++;
        Log.Info($"csissuesmp {name}: {(ok ? "ok" : "FAIL")} {detail}");
    }

    static void Update(On_Player.orig_Update orig, Player self, int i)
    {
        if (!Main.gameMenu && i == Main.myPlayer && !done) self.creativeGodMode = true;
        orig(self, i);
        if (Main.gameMenu)
        {
            frames = step = since = 0;
            return;
        }
        if (i != Main.myPlayer || done || Main.netMode == 0) return;
        frames++;
        try
        {
            if (Main.netMode == 1) Client(self);
            else Host();
        }
        catch (Exception e)
        {
            Report("run", false, e.ToString());
            done = true;
        }
        if (done) Log.Info($"csissuesmp END pass={pass} fail={fail}");
    }

    static void Next()
    {
        step++;
        since = frames;
    }

    static void Host()
    {
        if (step == 0)
        {
            if (frames < 5) return;
            var entries = new List<string>(BigConfig.Count);
            for (int k = 0; k < BigConfig.Count; k++) entries.Add(BigConfig.Entry(k));
            BigConfig.Instance.Entries = entries;
            BigConfig.Instance.Marker = 42;
            Log.Info($"csissuesmp host config ready: {BigConfig.Count} entries");
            Next();
            return;
        }
        if (step == 1)
        {
            if (HurtPlayer.RemoteHurts == 0 && frames - since < Timeout) return;
            Report("the host does not modify the client's hurt again", HurtPlayer.RemoteHurts > 0 && HurtPlayer.RemoteModify == 0 && HurtPlayer.RemoteDamage <= 40,
                $"remoteHurts={HurtPlayer.RemoteHurts} remoteModify={HurtPlayer.RemoteModify} remoteDamage={HurtPlayer.RemoteDamage}");
            done = true;
        }
    }

    static void Client(Player self)
    {
        if (step == 0)
        {
            if (frames < 120) return;
            BigConfig config = BigConfig.Instance;
            int count = config.Entries?.Count ?? -1;
            bool last = count == BigConfig.Count && config.Entries![BigConfig.Count - 1] == BigConfig.Entry(BigConfig.Count - 1);
            Report("a server config larger than one packet reaches the client", config.Marker == 42 && last, $"marker={config.Marker} entries={count}");
            Next();
            return;
        }
        if (step == 1)
        {
            self.creativeGodMode = false;
            self.immune = false;
            self.immuneTime = 0;
            self.statLife = self.statLifeMax2;
            self.Hurt(PlayerDeathReason.ByCustomReason("csissuesmp"), 20, 0);
            self.creativeGodMode = true;
            Report("the client modifies its own hurt once", HurtPlayer.LocalModify == 1 && HurtPlayer.LocalHurts == 1,
                $"localModify={HurtPlayer.LocalModify} localHurts={HurtPlayer.LocalHurts} localDamage={HurtPlayer.LocalDamage} defense={self.statDefense}");
            Next();
            return;
        }
        if (frames - since < 120) return;
        self.statLife = self.statLifeMax2;
        done = true;
    }
}
