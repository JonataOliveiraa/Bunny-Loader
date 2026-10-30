using System;
using System.IO;
using BunnyLoader.Interop;
using CsHair.Content.Hairs;
using log4net;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.IO;
using Terraria.ModLoader;

namespace CsHair;

public static class Checks
{
    static int pass, fail, frames;
    static bool done;

    static ILog Log => ModContent.GetInstance<CsHairMod>().Logger;

    public static void Reset()
    {
        frames = pass = fail = 0;
        done = false;
    }

    public static void Frame(Player p)
    {
        if (done || ++frames != 120) return;
        done = true;
        int hair = p.hair;
        try { Run(p); }
        catch (Exception e) { Report("run", false, e.ToString()); }
        p.hair = ModContent.GetInstance<TestHair>().Type;
        Log.Info($"cshair END pass={pass} fail={fail}");
    }

    static bool Listed(bool stylist)
    {
        HairstyleUnlocksHelper helper = HairstyleUnlocksHelper.New();
        helper._isAtStylist = stylist;
        helper.UpdateUnlocks();
        return new GameList(helper.AvailableHairstyles).Contains(ModContent.GetInstance<TestHair>().Type);
    }

    static void Run(Player p)
    {
        TestHair hair = ModContent.GetInstance<TestHair>();
        Report("the hair gets the first type after the game's", hair.Type == HairID.Count && HairLoader.Count == HairID.Count + 1 && HairLoader.GetHair(hair.Type) == hair &&
            TestHair.StaticDefaults == 1, $"type={hair.Type} count={HairLoader.Count} static={TestHair.StaticDefaults}");
        Report("its textures are in TextureAssets.PlayerHair", TextureAssets.PlayerHair[hair.Type].Value.Width > 0 && TextureAssets.PlayerHairAlt[hair.Type].Value.Width > 0 &&
            TextureAssets.PlayerHair.Length >= HairLoader.Count, $"width={TextureAssets.PlayerHair[hair.Type].Value.Width} length={TextureAssets.PlayerHair.Length}");

        p.hair = hair.Type;
        p.GetHairSettings(out _, out _, out _, out bool back, out _);
        p.hair = 0;
        p.GetHairSettings(out _, out _, out _, out bool vanillaBack, out _);
        Report("HairID.Sets.DrawBackHair decides the back hair", back && !vanillaBack, $"mod={back} hair0={vanillaBack}");

        TestHair.Unlock = false;
        bool locked = Listed(true);
        TestHair.Unlock = true;
        bool unlocked = Listed(true);
        Report("the stylist lists it when its conditions are met", !locked && unlocked, $"locked={locked} unlocked={unlocked}");

        PlayerFileData data = Main.ActivePlayerFileData;
        string path = ((FileData)data).Path;
        p.hair = hair.Type;
        Player.SavePlayer(data, true, false);
        string json = File.Exists(path + ".bl.json") ? File.ReadAllText(path + ".bl.json") : "";
        PlayerFileData loaded = Player.LoadPlayer(path, false);
        Report("the save keeps the mod hair by name", json.Contains("\"bunny:hair\"") && json.Contains("/TestHair") && p.hair == hair.Type && loaded.Player.hair == hair.Type,
            $"json={json.Contains("bunny:hair")} live={p.hair} loaded={loaded.Player.hair}");
    }

    static void Report(string name, bool ok, string detail)
    {
        if (ok) pass++;
        else fail++;
        Log.Info($"cshair {name}: {(ok ? "ok" : "FAIL")} {detail}");
    }
}
