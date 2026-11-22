using System;
using System.Collections.Generic;
using BunnyLoader.Interop;
using log4net;
using Terraria;
using Terraria.ModLoader;

namespace TouchProfileTest;

public sealed class CsTouchProfileMod : Mod
{
    static int frames, pass, fail;
    static bool done;
    static VirtualControlsProfile_Layout original, other;

    static ILog Log => ModContent.GetInstance<CsTouchProfileMod>().Logger;

    public override void Load() => On_Player.Update += Update;

    static void Update(On_Player.orig_Update orig, Player self, int i)
    {
        orig(self, i);
        if (Main.gameMenu)
        {
            frames = pass = fail = 0;
            done = false;
            return;
        }
        if (i != Main.myPlayer || done) return;
        frames++;
        try { Step(); }
        catch (Exception e)
        {
            Report("run", false, e.ToString());
            Finish();
        }
    }

    static void Step()
    {
        VirtualControlsStyles_Layout styles = VirtualControlsStyles_Layout.Instance;
        switch (frames)
        {
            case 120:
            {
                original = styles.ActiveProfile;
                GameArray<VirtualControlsProfile_Layout> all = styles.Profiles;
                var names = new List<string>();
                for (int k = 0; !all.IsNull && k < all.Length; k++)
                {
                    VirtualControlsProfile_Layout profile = all[k];
                    if (profile.IsNull) continue;
                    names.Add($"{profile.Id}/{profile.Hardware}");
                    if (profile.Pointer == original.Pointer) continue;
                    if (other.IsNull || profile.Id == "Legacy") other = profile;
                }
                Log.Info($"cstouchprofile profiles: {string.Join(", ", names)}; active={original.Id}; other={(other.IsNull ? "none" : other.Id)}");
                Report("there is another profile to switch to", !original.IsNull && !other.IsNull, "");
                Log.Info("cstouchprofile SHOT before");
                break;
            }
            case 150:
                if (other.IsNull) break;
                styles.SetActiveProfile(other);
                Report("SetActiveProfile switches the active touch profile", styles.ActiveProfile.Pointer == other.Pointer,
                    $"active={styles.ActiveProfile.Id} wanted={other.Id}");
                break;
            case 210:
                Log.Info("cstouchprofile SHOT after");
                break;
            case 260:
                if (!original.IsNull) styles.SetActiveProfile(original);
                Report("the original profile comes back", styles.ActiveProfile.Pointer == original.Pointer, $"active={styles.ActiveProfile.Id}");
                Finish();
                break;
        }
    }

    static void Finish()
    {
        done = true;
        Log.Info($"cstouchprofile END pass={pass} fail={fail}");
    }

    static void Report(string name, bool ok, string detail)
    {
        if (ok) pass++;
        else fail++;
        Log.Info($"cstouchprofile {name}: {(ok ? "ok" : "FAIL")} {detail}");
    }
}
