using System;
using System.Collections.Generic;
using CsMount.Content;
using log4net;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CsMount;

public static class Checks
{
    static int pass, fail, frames;
    static bool done;

    static ILog Log => ModContent.GetInstance<CsMountMod>().Logger;

    static int Calls(string name) => TestMount.Calls.GetValueOrDefault(name);

    public static void Reset()
    {
        frames = pass = fail = 0;
        done = false;
    }

    public static void Frame(Player p)
    {
        if (done) return;
        frames++;
        if (frames == 30) p.AddBuff(ModContent.BuffType<TestMountBuff>(), 20);
        if (frames != 150) return;
        done = true;
        try { Run(p); }
        catch (Exception e) { Report("run", false, e.ToString()); }
        p.AddBuff(ModContent.BuffType<TestMountBuff>(), 20);
        Log.Info($"csmount END pass={pass} fail={fail}");
    }

    static void Run(Player p)
    {
        int type = ModContent.MountType<TestMount>();
        TestMount mount = ModContent.GetInstance<TestMount>();
        Report("the mount gets the first type after the game's", type == MountID.Count - 1 && MountLoader.GetMount(type) == mount && Mount.mounts[type] == mount.MountData &&
            MountID.Sets.Cart.Length >= MountID.Count, $"type={type} count={MountID.Count}");
        Report("SetStaticDefaults sees the textures and the arrays", Calls("SetStaticDefaults") == 1 && TestMount.TextureWidth > 20 &&
            mount.MountData.playerYOffsets.Length == 4 && mount.MountData.playerYOffsets[3] == 20, $"width={TestMount.TextureWidth} offsets={mount.MountData.playerYOffsets?.Length}");
        Report("the buff mounts it and SetMount runs once", p.mount.Active && p.mount.Type == type && Calls("SetMount") == 1, $"active={p.mount.Active} type={p.mount.Type} calls={Calls("SetMount")}");
        int ticks = p.mount._mountSpecificData is TestMount.Data data ? data.Ticks : -1;
        Report("UpdateEffects runs every frame with the mount's data", Calls("UpdateEffects") >= 100 && ticks == Calls("UpdateEffects"),
            $"calls={Calls("UpdateEffects")} ticks={ticks}");
        Report("UpdateFrame and JumpHeight run", Calls("UpdateFrame") >= 100 && Calls("JumpHeight") >= 1 && TestMount.JumpHeightSeen == 5,
            $"frame={Calls("UpdateFrame")} jump={Calls("JumpHeight")} height={TestMount.JumpHeightSeen}");
        Report("Draw runs for the back and front layers", Calls("Draw0") >= 30 && Calls("Draw2") >= 30, $"back={Calls("Draw0")} front={Calls("Draw2")}");

        p.ClearBuff(ModContent.BuffType<TestMountBuff>());
        p.mount.Dismount(p);
        Report("Dismount runs and clears the mount's data", !p.mount.Active && Calls("Dismount") == 1 && p.mount._mountSpecificData == null,
            $"active={p.mount.Active} calls={Calls("Dismount")}");
    }

    static void Report(string name, bool ok, string detail)
    {
        if (ok) pass++;
        else fail++;
        Log.Info($"csmount {name}: {(ok ? "ok" : "FAIL")} {detail}");
    }
}
