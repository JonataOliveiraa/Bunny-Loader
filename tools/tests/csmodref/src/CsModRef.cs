using System;
using System.Collections.Generic;
using System.Linq;
using CsModTarget;
using Terraria.ModLoader;

namespace CsModRef;

public sealed class CsModRefMod : Mod
{
    int pass, fail;

    public override void Load()
    {
        Report("dependency loaded first", TargetApi.Loaded, "");
        Report("dependency types", TargetApi.Answer == 42 && TargetApi.Greet("ref") == "hello, ref", "");

        bool byId = ModLoader.TryGetMod("test-csmodtarget", out Mod target);
        Report("TryGetMod by manifest id", byId && target is CsModTargetMod, $"{target?.Name}");
        if (!byId) return;
        Report("TryGetMod by Name and uid",
            ModLoader.TryGetMod("CsModTarget", out Mod byName) && byName == target &&
            ModLoader.TryGetMod("0f9f4a32-f6d1-4e05-897e-e845271b066a", out Mod byUid) && byUid == target, "");
        Report("TryGetMod of a missing mod", !ModLoader.TryGetMod("no-such-mod", out Mod missing) && missing == null, "");
        Report("HasMod", ModLoader.HasMod("test-csmodtarget") && !ModLoader.HasMod("no-such-mod"), "");
        Report("GetMod finds itself in Load", ModLoader.GetMod("test-csmodref") == this, "");
        try
        {
            ModLoader.GetMod("no-such-mod");
            Report("GetMod of a missing mod throws", false, "no exception");
        }
        catch (KeyNotFoundException)
        {
            Report("GetMod of a missing mod throws", true, "");
        }

        Report("manifest data", target.DisplayName == "Teste: alvo do C#" && target.Version == new Version(2, 1, 0),
            $"name={target.DisplayName} version={target.Version}");
        Report("Call", target.Call("Add", 2, 3) is 5 && "x".Equals(target.Call("Echo", "x")) && target.Call("Unknown") == null, "");
        try
        {
            target.Call("Throw");
            Report("an error in Call reaches the caller", false, "no exception");
        }
        catch (InvalidOperationException)
        {
            Report("an error in Call reaches the caller", true, "");
        }
        Report("Call of a mod without Call", Call("Anything") == null, "");
    }

    public override void PostSetupContent()
    {
        string[] names = ModLoader.Mods.Select(m => m.Name).ToArray();
        int t = Array.IndexOf(names, "CsModTarget"), me = Array.IndexOf(names, Name);
        Report("Mods in load order", t >= 0 && me > t, string.Join(",", names));
        Logger.Info($"csmodref END pass={pass} fail={fail}");
    }

    void Report(string name, bool ok, string detail)
    {
        if (ok) pass++;
        else fail++;
        Logger.Info($"csmodref {name}: {(ok ? "ok" : "FAIL")} {detail}");
    }
}
