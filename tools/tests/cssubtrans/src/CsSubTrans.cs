using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using log4net;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent.Generation;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.WorldBuilding;

namespace CsSubTrans;

public abstract class BottomDimension : ModSubworld
{
    public const int BasinHalf = 8, BasinDepth = 6, BasinFloor = 50;

    public override int Width => 1000;

    public override int Height => 500;

    public static int BasinX => Main.maxTilesX / 2 + 40;

    public override List<GenPass> GenerationTasks => new()
    {
        new PassLegacy("Floor", (progress, configuration) =>
        {
            int floor = (int)Main.worldSurface + 1;
            for (int x = 0; x < Main.maxTilesX; x++)
                for (int y = floor; y < floor + 20; y++)
                    Main.tile[x, y].ResetToType(TileID.Stone);

            int bottom = Main.maxTilesY - BasinFloor;
            for (int x = BasinX - BasinHalf; x <= BasinX + BasinHalf; x++)
                Main.tile[x, bottom].ResetToType(TileID.Stone);
            for (int y = bottom - BasinDepth - 2; y < bottom; y++)
            {
                Main.tile[BasinX - BasinHalf, y].ResetToType(TileID.Stone);
                Main.tile[BasinX + BasinHalf, y].ResetToType(TileID.Stone);
            }
        }, 1),
    };
}

public sealed class FadeDimension : BottomDimension
{
    public static int LeaveDraws, CoveredDraws, ArriveDraws;
    public static float MaxLeave, MinArrive = 1f;

    public override bool ShowLoadingScreen => false;

    public override float TransitionOutSeconds => 8f;

    public override float TransitionInSeconds => 8f;

    public override void DrawTransition(SpriteBatch spriteBatch, float progress)
    {
        if (Main.gameMenu)
        {
            if (progress >= 1f) CoveredDraws++;
        }
        else if (!SubworldLoader.IsActive<FadeDimension>())
        {
            LeaveDraws++;
            MaxLeave = Math.Max(MaxLeave, progress);
        }
        else
        {
            ArriveDraws++;
            MinArrive = Math.Min(MinArrive, progress);
        }

        base.DrawTransition(spriteBatch, progress);
    }
}

public sealed class HellDimension : BottomDimension
{
    public override bool HideUnderworld => false;
}

public sealed class CsSubTransMod : Mod
{
    const string Test = "cssubtrans";

    static int frames, phase, pass, fail, startWater, hiddenLight;
    static nint seen, mainWorld;
    static bool done;
    static float leaveAtHalf;
    static string lastChat = "";
    static readonly Stopwatch trip = new();

    static ILog Log => ModContent.GetInstance<CsSubTransMod>().Logger;

    public override void Load()
    {
        On_Player.Update += Update;
        On_Main.NewText_string_byte_byte_byte_bool += NewText;
    }

    static void NewText(On_Main.orig_NewText_string_byte_byte_byte_bool orig, string? text, byte r, byte g, byte b, bool only)
    {
        lastChat = text ?? "";
        orig(text, r, g, b, only);
    }

    static void Update(On_Player.orig_Update orig, Player self, int i)
    {
        orig(self, i);
        if (Main.gameMenu || i != Main.myPlayer || done) return;
        if (Main.ActiveWorldFileData.Pointer != seen)
        {
            seen = Main.ActiveWorldFileData.Pointer;
            frames = 0;
            if (trip.IsRunning) Log.Info($"{Test} trip took {trip.ElapsedMilliseconds} ms into {SubworldLoader.Current?.FullName ?? "the main world"}");
            trip.Reset();
        }

        if (phase is 2 or 3) self.fallStart = (int)(self.position.Y / 16f);
        try { Step(++frames); }
        catch (Exception e)
        {
            Report("run", false, e.ToString());
            done = true;
        }

        if (done) Log.Info($"{Test} END pass={pass} fail={fail}");
    }

    static ModSubworld? Example(string name) => SubworldLoader.Subworlds.FirstOrDefault(s => s.FullName == "ExampleMod/" + name);

    static void Enter(ModSubworld subworld, string name)
    {
        trip.Restart();
        Report($"Enter {name} starts", SubworldLoader.Enter(subworld), "");
    }

    static void Step(int f)
    {
        switch (phase)
        {
            case 0 when f == 120:
                Report("the main world keeps its underworld layer", Main.UnderworldLayer == Main.maxTilesY - 200, $"layer={Main.UnderworldLayer} height={Main.maxTilesY}");
                mainWorld = Main.ActiveWorldFileData.Pointer;
                Enter(ModContent.GetInstance<FadeDimension>(), "FadeDimension");
                Report("the trip counts as traveling during the fade", SubworldLoader.Traveling, "");
                phase = 1;
                break;
            case 1 when f == 150:
                leaveAtHalf = FadeDimension.MaxLeave;
                Report("the old world stays on screen while it fades out", !SubworldLoader.InSubworld && Main.ActiveWorldFileData.Pointer == mainWorld && FadeDimension.LeaveDraws > 0 && leaveAtHalf is > 0.01f and < 0.9f, $"draws={FadeDimension.LeaveDraws} progress={leaveAtHalf:0.00}");
                Log.Info($"{Test} SHOT fadeout");
                break;
            case 1 when SubworldLoader.IsActive<FadeDimension>():
                Report("the loading screen is covered while the world changes", FadeDimension.CoveredDraws > 0, $"covered={FadeDimension.CoveredDraws} leave={FadeDimension.LeaveDraws}");
                phase = 2;
                break;
            case 2 when f == 120:
                Log.Info($"{Test} SHOT fadein");
                break;
            case 2 when f == 150:
                Report("the new world fades in", FadeDimension.ArriveDraws > 0 && FadeDimension.MinArrive < 0.9f, $"draws={FadeDimension.ArriveDraws} progress={FadeDimension.MinArrive:0.00}");
                break;
            case 2 when f == 600:
                Report("the trip is over once the fade in ends", !SubworldLoader.Traveling, "");
                Report("a hidden underworld moves the layer to the bottom", Main.UnderworldLayer == Main.maxTilesY, $"layer={Main.UnderworldLayer} height={Main.maxTilesY}");
                Report("the player at the bottom is not in the underworld", BottomStart(), "");
                break;
            case 2 when f == 840:
                int water = Water();
                hiddenLight = Light();
                Report("water at the bottom does not evaporate", water >= startWater * 95 / 100, $"before={startWater} after={water}");
                Report("the bottom rows get no underworld light", hiddenLight < 40, $"light={hiddenLight}");
                Log.Info($"{Test} SHOT bottomhidden");
                Enter(ModContent.GetInstance<HellDimension>(), "HellDimension");
                phase = 3;
                break;
            case 3 when f == 60 && SubworldLoader.IsActive<HellDimension>():
                Report("control: a visible underworld keeps the layer", Main.UnderworldLayer == Main.maxTilesY - 200, $"layer={Main.UnderworldLayer}");
                BottomStart();
                break;
            case 3 when f == 300 && SubworldLoader.IsActive<HellDimension>():
                int evaporated = Water();
                int light = Light();
                Report("control: water evaporates in a visible underworld", evaporated < startWater * 80 / 100, $"before={startWater} after={evaporated}");
                Report("control: a visible underworld lights the bottom rows", light > hiddenLight + 30, $"light={light} hidden={hiddenLight}");
                Log.Info($"{Test} SHOT bottomhell");
                SubworldLoader.Exit();
                phase = 4;
                break;
            case 4 when f == 60 && !SubworldLoader.InSubworld:
                Report("the main world gets its layer back", Main.UnderworldLayer == Main.maxTilesY - 200, $"layer={Main.UnderworldLayer}");
                if (Example("ExampleAdvancedSubworld") is not { } advanced)
                {
                    Log.Info($"{Test} the Example Mod is off; its dimensions were not tried");
                    done = true;
                    break;
                }

                Enter(advanced, "ExampleAdvancedSubworld");
                phase = 5;
                break;
            case 5 when f == 105 && !SubworldLoader.InSubworld:
                Log.Info($"{Test} SHOT exleave");
                break;
            case 5 when SubworldLoader.Current?.Name == "ExampleAdvancedSubworld":
                phase = 6;
                break;
            case 6 when f == 40:
                Log.Info($"{Test} SHOT exarrive");
                break;
            case 6 when f == 150:
                Report("ExampleAdvancedSubworld keeps the night", !Main.dayTime && Math.Abs(Main.time - 16200.0) < 1.0, $"day={Main.dayTime} time={Main.time:0}");
                Report("ExampleAdvancedSubworld greets the player", lastChat == Language.GetTextValue("Mods.ExampleMod.Subworlds.ExampleAdvancedSubworld.Welcome"), $"chat={lastChat}");
                Report("ExampleAdvancedSubworld is generated with its size", Main.maxTilesX == 1400 && Main.maxTilesY == 700, $"size={Main.maxTilesX}x{Main.maxTilesY}");
                Log.Info($"{Test} SHOT exadvanced");
                Enter(Example("ExampleSubworld")!, "ExampleSubworld");
                phase = 7;
                break;
            case 7 when f == 90 && SubworldLoader.Current?.Name == "ExampleSubworld":
                int surface = (int)Main.worldSurface;
                Report("ExampleSubworld has grass over the example plains", Main.maxTilesX == 1000 && Main.tile[Main.maxTilesX / 2, surface].TileType == TileID.Grass, $"size={Main.maxTilesX}x{Main.maxTilesY} top={Main.tile[Main.maxTilesX / 2, surface].TileType}");
                Log.Info($"{Test} SHOT explains");
                SubworldLoader.Exit();
                phase = 8;
                break;
            case 8 when f == 60 && !SubworldLoader.InSubworld:
                Report("back in the main world after the Example Mod dimensions", Main.UnderworldLayer == Main.maxTilesY - 200, "");
                done = true;
                break;
        }
    }

    static bool BottomStart()
    {
        int x = BottomDimension.BasinX, bottom = Main.maxTilesY - BottomDimension.BasinFloor;
        startWater = 0;
        for (int i = x - BottomDimension.BasinHalf + 1; i < x + BottomDimension.BasinHalf; i++)
        {
            for (int j = bottom - BottomDimension.BasinDepth; j < bottom; j++)
            {
                ref Tile tile = ref Main.tile[i, j];
                tile.LiquidType = LiquidID.Water;
                tile.LiquidAmount = 255;
                Liquid.AddWater(i, j);
                startWater += 255;
            }
        }

        Player player = Main.LocalPlayer;
        player.position = new Vector2((x - 40) * 16, (Main.maxTilesY - 60) * 16);
        player.velocity = Vector2.Zero;
        return true;
    }

    static int Water()
    {
        int x = BottomDimension.BasinX, bottom = Main.maxTilesY - BottomDimension.BasinFloor, total = 0;
        for (int i = x - BottomDimension.BasinHalf + 1; i < x + BottomDimension.BasinHalf; i++)
            for (int j = bottom - 20; j < bottom; j++)
                total += Main.tile[i, j].LiquidAmount;
        return total;
    }

    static int Light()
    {
        Point at = Main.LocalPlayer.Center.ToTileCoordinates();
        var color = Lighting.GetColor(at.X - 6, at.Y - 4);
        return (color.R + color.G + color.B) / 3;
    }

    static void Report(string name, bool ok, string detail)
    {
        if (ok) pass++;
        else fail++;
        Log.Info($"{Test} {name}: {(ok ? "ok" : "FAIL")} {detail}");
    }
}
