using System;
using System.IO;
using BunnyLoader.Interop;
using log4net;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.IO;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CsSubworld;

public sealed class CsSubworldMod : Mod
{
    const int PersistX = 120;

    static int frames, phase, pass, fail, pauseDraws;
    static bool done, sawLoadingText;
    static string mainPath = "", mainName = "";
    static int mainWidth, mainHeight;
    static Guid mainId;
    static nint seenWorld;

    internal static ILog Log => ModContent.GetInstance<CsSubworldMod>().Logger;

    public override void Load()
    {
        On_Player.Update += Update;
        On_GUIStatusMenu.Draw += StatusDraw;
        On_GUISettingsPauseMenu.Draw += PauseDraw;
    }

    static void StatusDraw(On_GUIStatusMenu.orig_Draw orig, GUIStatusMenu self)
    {
        orig(self);
        if (SubworldLoader.Traveling && Main.statusText == Language.GetTextValue("BunnyLoader.LoadingDimension"))
            sawLoadingText = true;
    }

    static void PauseDraw(On_GUISettingsPauseMenu.orig_Draw orig, GUISettingsPauseMenu self)
    {
        orig(self);
        if (phase != 4 || ++pauseDraws != 30) return;
        Rectangle region = GUITransactionButton.GetRegion(SettingsPauseMenu_Layout.Instance.Home);
        int x = region.X + region.Width / 2, y = region.Y + region.Height / 2;
        Log.Info("cssubworld SHOT pause");
        Log.Info($"cssubworld TAP {x} {y} {Main.screenHeight} region={region} screen={Main.screenWidth}x{Main.screenHeight}");
    }

    static void Update(On_Player.orig_Update orig, Player self, int i)
    {
        orig(self, i);
        if (Main.gameMenu || i != Main.myPlayer || done) return;
        if (Main.ActiveWorldFileData.Pointer != seenWorld)
        {
            seenWorld = Main.ActiveWorldFileData.Pointer;
            frames = 0;
        }

        try { Step(++frames); }
        catch (Exception e)
        {
            Report("run", false, e.ToString());
            done = true;
        }

        if (done) Log.Info($"cssubworld END pass={pass} fail={fail}");
    }

    static void Step(int f)
    {
        if (f != 60) return;
        switch (phase)
        {
            case 0:
                Main0();
                break;
            case 1:
                First();
                break;
            case 2:
                Back();
                break;
            case 3:
                Second();
                break;
            case 4 when !SubworldLoader.InSubworld:
                Last();
                break;
        }
    }

    static void Main0()
    {
        WorldFileData main = Main.ActiveWorldFileData;
        mainPath = main.Path ?? "";
        mainName = Main.worldName ?? "";
        mainId = main.UniqueId;
        mainWidth = Main.maxTilesX;
        mainHeight = Main.maxTilesY;
        TestDimension.Score = 7;
        Report("starts in the main world", !SubworldLoader.InSubworld && SubworldLoader.Current == null, $"path={mainPath} size={mainWidth}x{mainHeight}");
        bool entered = SubworldLoader.Enter<TestDimension>();
        Report("Enter starts the trip on the loading screen", entered && SubworldLoader.Traveling && Main.gameMenu, $"entered={entered} traveling={SubworldLoader.Traveling} menu={Main.gameMenu}");
        Report("a second Enter during the trip is refused", !SubworldLoader.Enter<TestDimension>(), "");
        phase = 1;
    }

    static void First()
    {
        var dimension = ModContent.GetInstance<TestDimension>();
        string path = Main.ActiveWorldFileData.Path ?? "";
        string expected = Path.Combine(Main.WorldPath ?? "", mainId.ToString(), "CsSubworld_TestDimension.subworld");
        Report("the subworld is current", SubworldLoader.InSubworld && SubworldLoader.IsActive<TestDimension>() && !SubworldLoader.Traveling, $"current={SubworldLoader.Current?.FullName}");
        Report("the world has the subworld size", Main.maxTilesX == 1000 && Main.maxTilesY == 500, $"size={Main.maxTilesX}x{Main.maxTilesY}");
        Report("the world file is in the main world's folder", path == expected && Terraria.Utilities.FileUtilities.Exists(path, false), $"path={path} expected={expected}");
        Report("the world name is the display name", Main.worldName == dimension.DisplayName.Value, $"name={Main.worldName}");
        Report("generation ran once", TestDimension.Generated == 1, $"generated={TestDimension.Generated}");
        Report("OnEnter and PostWorldReady ran once", TestDimension.Entered == 1 && TestDimension.Ready == 1, $"entered={TestDimension.Entered} ready={TestDimension.Ready}");
        Tile marker = Main.tile[TestDimension.MarkerX, TestDimension.FloorY - 1];
        Report("the generated tiles were loaded", marker.HasTile && marker.type == TileID.GrayBrick, $"marker={marker.HasTile}/{marker.type}");
        Report("the carried data came from the main world", TestDimension.Score == 7, $"score={TestDimension.Score}");
        Report("the loading screen showed Loading Dimension", sawLoadingText, "");
        Player player = Main.LocalPlayer;
        Report("the player spawned inside the subworld", player.position.X > 0 && player.position.X < Main.maxTilesX * 16 && player.position.Y < Main.maxTilesY * 16,
            $"position={player.position} day={Main.dayTime} time={Main.time} surface={Main.worldSurface} spawn={Main.spawnTileX},{Main.spawnTileY}");
        Log.Info("cssubworld SHOT subworld");
        WorldGen.PlaceTile(PersistX, TestDimension.FloorY - 1, TileID.RedBrick, true, true);
        TestDimension.Score = 11;
        bool left = SubworldLoader.Exit();
        Report("Exit starts the trip back", left && SubworldLoader.Traveling, $"left={left}");
        phase = 2;
    }

    static void Back()
    {
        Report("back in the main world", !SubworldLoader.InSubworld && Main.ActiveWorldFileData.Path == mainPath && Main.worldName == mainName,
            $"current={SubworldLoader.Current?.FullName} path={Main.ActiveWorldFileData.Path}");
        Report("the main world has its size back", Main.maxTilesX == mainWidth && Main.maxTilesY == mainHeight, $"size={Main.maxTilesX}x{Main.maxTilesY}");
        Report("OnExit ran once", TestDimension.Exited == 1, $"exited={TestDimension.Exited}");
        Report("the carried data came back from the subworld", TestDimension.Score == 11, $"score={TestDimension.Score}");
        bool entered = SubworldLoader.Enter<TestDimension>();
        Report("Enter works again", entered, "");
        phase = 3;
    }

    static void Second()
    {
        Report("a persistent subworld loads from its file", TestDimension.Generated == 1 && SubworldLoader.IsActive<TestDimension>(), $"generated={TestDimension.Generated}");
        Tile kept = Main.tile[PersistX, TestDimension.FloorY - 1];
        Report("a block placed in the subworld is still there", kept.HasTile && kept.type == TileID.RedBrick, $"block={kept.HasTile}/{kept.type}");
        Report("OnEnter and PostWorldReady ran twice", TestDimension.Entered == 2 && TestDimension.Ready == 2, $"entered={TestDimension.Entered} ready={TestDimension.Ready}");
        GUIInstance gi = GUIInstance.Active;
        gi.GUISettingsPauseMenu.Enabled = true;
        gi.GUISettings.Visible = true;
        Main.ingameOptionsWindow = true;
        phase = 4;
    }

    static void Last()
    {
        Report("Exit Dimension in the pause menu goes back to the main world", !SubworldLoader.InSubworld && Main.ActiveWorldFileData.Path == mainPath,
            $"current={SubworldLoader.Current?.FullName} path={Main.ActiveWorldFileData.Path}");
        Report("OnExit ran twice", TestDimension.Exited == 2, $"exited={TestDimension.Exited}");
        string folder = Path.Combine(Main.WorldPath ?? "", mainId.ToString());
        try { Directory.Delete(folder, true); }
        catch (Exception e) { Log.Info($"cssubworld cleanup: {e.Message}"); }
        done = true;
    }

    static void Report(string name, bool ok, string detail)
    {
        if (ok) pass++;
        else fail++;
        Log.Info($"cssubworld {name}: {(ok ? "ok" : "FAIL")} {detail}");
    }
}
