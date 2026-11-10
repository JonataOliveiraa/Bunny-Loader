using System;
using System.Collections.Generic;
using System.IO;
using log4net;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameContent.Generation;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.Utilities;
using Terraria.WorldBuilding;

namespace CsSubworld2;

public abstract class FlatDimension : ModSubworld
{
    public override int Width => 1000;

    public override int Height => 500;

    public override List<GenPass> GenerationTasks => new()
    {
        new PassLegacy("Floor", (progress, configuration) =>
        {
            int floor = (int)Main.worldSurface + 1;
            for (int x = 0; x < Main.maxTilesX; x++)
                for (int y = floor; y < floor + 20; y++)
                    WorldGen.PlaceTile(x, y, TileID.Stone, true, true);
        }, 1),
    };
}

public sealed class QuietDimension : FlatDimension
{
    public static int Updates;

    public override void Update() => Updates++;
}

public sealed class ForgetfulDimension : FlatDimension
{
    public override bool SavePlayerChanges => false;
}

public sealed class CsSubworld2Mod : Mod
{
    const int Slot = 49;

    static int frames, phase, pass, fail, overground, slotType;
    static nint seen;
    static bool done;
    static string quietPath = "";

    static ILog Log => ModContent.GetInstance<CsSubworld2Mod>().Logger;

    public override void Load()
    {
        On_Player.Update += Update;
        On_WorldGen.UpdateWorld_OvergroundTile += Overground;
    }

    static void Overground(On_WorldGen.orig_UpdateWorld_OvergroundTile orig, int i, int j, bool checkNPCSpawns, int wallDist)
    {
        overground++;
        orig(i, j, checkNPCSpawns, wallDist);
    }

    static void Update(On_Player.orig_Update orig, Player self, int i)
    {
        orig(self, i);
        if (Main.gameMenu || i != Main.myPlayer || done) return;
        if (Main.ActiveWorldFileData.Pointer != seen)
        {
            seen = Main.ActiveWorldFileData.Pointer;
            frames = 0;
            overground = 0;
        }

        try { Step(++frames); }
        catch (Exception e)
        {
            Report("run", false, e.ToString());
            done = true;
        }

        if (done) Log.Info($"cssubworld2 END pass={pass} fail={fail}");
    }

    static void Step(int f)
    {
        switch (phase)
        {
            case 0 when f == 120:
                Report("random tile updates run in the main world", overground > 0, $"calls={overground}");
                SubworldLoader.Enter<QuietDimension>();
                phase = 1;
                break;
            case 1 when f == 60:
                Log.Info("cssubworld2 SHOT sky");
                break;
            case 1 when f == 120:
                quietPath = Main.ActiveWorldFileData.Path ?? "";
                Report("Update runs every world update", QuietDimension.Updates >= 60, $"updates={QuietDimension.Updates}");
                Report("random tile updates stop in the subworld", overground == 0, $"calls={overground}");
                Main.LocalPlayer.position = new Vector2(Main.maxTilesX * 8, (Main.maxTilesY - 60) * 16);
                break;
            case 1 when f == 180:
                Player player = Main.LocalPlayer;
                Report("the bottom of a small subworld is not the underworld", !player.ZoneUnderworldHeight, $"y={player.position.Y / 16} underworld={player.ZoneUnderworldHeight}");
                SubworldLoader.Exit();
                phase = 2;
                break;
            case 2 when f == 60:
                Report("a temporary subworld deletes its file on the way out", quietPath.Length > 0 && !FileUtilities.Exists(quietPath, false) && !File.Exists(quietPath), $"path={quietPath}");
                slotType = Main.LocalPlayer.inventory[Slot].type;
                SubworldLoader.Enter<ForgetfulDimension>();
                phase = 3;
                break;
            case 3 when f == 60:
                int other = slotType == ItemID.GoldBar ? ItemID.SilverBar : ItemID.GoldBar;
                Main.LocalPlayer.inventory[Slot].SetDefaults(other, default);
                Report("the item changed in the subworld", Main.LocalPlayer.inventory[Slot].type == other, $"type={Main.LocalPlayer.inventory[Slot].type}");
                SubworldLoader.Exit();
                phase = 4;
                break;
            case 4 when f == 60:
                int now = Main.LocalPlayer.inventory[Slot].type;
                Report("SavePlayerChanges false gives the player back as it entered", now == slotType, $"before={slotType} now={now}");
                Report("the main world is active again", !SubworldLoader.InSubworld, "");
                done = true;
                break;
        }
    }

    static void Report(string name, bool ok, string detail)
    {
        if (ok) pass++;
        else fail++;
        Log.Info($"cssubworld2 {name}: {(ok ? "ok" : "FAIL")} {detail}");
    }
}
