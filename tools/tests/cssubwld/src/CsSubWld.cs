using System;
using System.Linq;
using log4net;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CsSubWld;

public sealed class CsSubWldMod : Mod
{
    const string Test = "cssubwld";
    const int FloorX = 200, TorchX = 136;

    static int frames, phase, pass, fail, mainMode;
    static nint seen;
    static bool done;

    static ILog Log => ModContent.GetInstance<CsSubWldMod>().Logger;

    public override void Load() => On_Player.Update += Update;

    static void Update(On_Player.orig_Update orig, Player self, int i)
    {
        orig(self, i);
        if (Main.gameMenu || i != Main.myPlayer || done) return;
        if (Main.ActiveWorldFileData.Pointer != seen)
        {
            seen = Main.ActiveWorldFileData.Pointer;
            frames = 0;
        }

        try { Step(++frames); }
        catch (Exception e)
        {
            Report("run", false, e.ToString());
            done = true;
        }

        if (done) Log.Info($"{Test} END pass={pass} fail={fail}");
    }

    static ModSubworld? Arena => SubworldLoader.Subworlds.FirstOrDefault(s => s.FullName == "ExampleMod/ExampleWorldFileSubworld");

    static int Ground => (int)Main.worldSurface + 10;

    static void Step(int f)
    {
        switch (phase)
        {
            case 0 when f == 120:
                if (Arena is not { } arena)
                {
                    Report("the Example Mod arena is registered", false, "run with SAMPLE=1");
                    done = true;
                    return;
                }

                mainMode = Main.GameMode;
                Main.LocalPlayer.showLastDeath = true;
                Main.LocalPlayer.lastDeathPostion = new Microsoft.Xna.Framework.Vector2(1234f, 567f);
                Report("Enter starts the trip to the arena", SubworldLoader.Enter(arena), "");
                phase = 1;
                break;
            case 1 when f == 90 && SubworldLoader.Current == Arena:
            {
                ushort block = ModContent.TryFind("ExampleMod", "ExampleBlock", out ModTile tile) ? tile.Type : (ushort)0;
                ushort wall = ModContent.TryFind("ExampleMod", "ExampleWall", out ModWall modWall) ? modWall.Type : (ushort)0;
                Report("the arena has the size of its file", Main.maxTilesX == 400 && Main.maxTilesY == 250, $"size={Main.maxTilesX}x{Main.maxTilesY}");
                Report("the arena floor is Example Block, from the .tiles.bl beside the file", block != 0 && Main.tile[FloorX, Ground].HasTile && Main.tile[FloorX, Ground].TileType == block, $"type={Main.tile[FloorX, Ground].TileType} expected={block}");
                Report("the arena wall is Example Wall, from the .walls.bl beside the file", wall != 0 && Main.tile[FloorX, Ground - 5].WallType == wall, $"wall={Main.tile[FloorX, Ground - 5].WallType} expected={wall}");
                Report("the vanilla torches come from the .wld", Main.tile[TorchX, Ground - 1].TileType == TileID.Torches, $"type={Main.tile[TorchX, Ground - 1].TileType}");
                Report("the arena takes the dimension's name", Main.worldName == Arena!.DisplayName.Value, $"name={Main.worldName}");
                Report("the arena takes the main world's difficulty", Main.GameMode == mainMode, $"mode={Main.GameMode} main={mainMode}");
                Report("the player spawns in the arena", Math.Abs(Main.LocalPlayer.Center.X / 16f - FloorX) < 20, $"x={Main.LocalPlayer.Center.X / 16f}");
                Report("the main world's death mark is not shown in the arena", !Main.LocalPlayer.showLastDeath, "");
                WorldGen.KillTile(FloorX, Ground, false, false, true);
                Report("the floor can be broken", !Main.tile[FloorX, Ground].HasTile, "");
                Log.Info($"{Test} SHOT arena");
                break;
            }
            case 1 when f == 300 && SubworldLoader.Current == Arena:
                GUIInstance.Active.GUIMap.OpenFullscreenMap();
                break;
            case 1 when f == 360 && SubworldLoader.Current == Arena:
                Log.Info($"{Test} SHOT arenamap");
                break;
            case 1 when f == 600 && SubworldLoader.Current == Arena:
                Report("the arena has no dungeon, so no Old Man", Main.dungeonX < 0 && !NPC.AnyNPCs(NPCID.OldMan), $"dungeon={Main.dungeonX},{Main.dungeonY}");
                break;
            case 1 when f == 660 && SubworldLoader.Current == Arena:
                GUIInstance.Active.GUIMap.CloseFullscreenMap();
                SubworldLoader.Exit();
                phase = 2;
                break;
            case 2 when f == 90 && !SubworldLoader.InSubworld:
                SubworldLoader.Enter(Arena!);
                phase = 3;
                break;
            case 3 when f == 90 && SubworldLoader.Current == Arena:
                Report("a temporary arena starts over from its file", Main.tile[FloorX, Ground].HasTile, "");
                SubworldLoader.Exit();
                phase = 4;
                break;
            case 4 when f == 90 && !SubworldLoader.InSubworld:
                Report("back in the main world", Main.GameMode == mainMode, "");
                Report("the main world's death mark comes back", Main.LocalPlayer.showLastDeath && Main.LocalPlayer.lastDeathPostion.X == 1234f, $"show={Main.LocalPlayer.showLastDeath} at={Main.LocalPlayer.lastDeathPostion}");
                done = true;
                break;
        }
    }

    static void Report(string name, bool ok, string detail)
    {
        if (ok) pass++;
        else fail++;
        Log.Info($"{Test} {name}: {(ok ? "ok" : "FAIL")} {detail}");
    }
}
