using System;
using System.Collections.Generic;
using log4net;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameContent.Generation;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.WorldBuilding;

namespace CsSubMap;

public abstract class SizedDimension : ModSubworld
{
    public override List<GenPass> GenerationTasks => new()
    {
        new PassLegacy("Floor", (progress, configuration) =>
        {
            int floor = (int)Main.worldSurface + 1;
            for (int x = 0; x < Main.maxTilesX; x++)
            {
                for (int y = floor; y < Math.Min(floor + 30, Main.maxTilesY); y++)
                    Main.tile[x, y].ResetToType(y < floor + 6 ? TileID.Dirt : TileID.Stone);
                Main.tile[x, floor].ResetToType(TileID.Grass);
            }
        }, 1),
    };
}

public sealed class WideDimension : SizedDimension
{
    public override int Width => 1000;

    public override int Height => 500;
}

public sealed class OceanDimension : SizedDimension
{
    public override int Width => 1000;

    public override int Height => 500;

    public override bool HideOcean => false;
}

public sealed class SmallDimension : SizedDimension
{
    public override int Width => 400;

    public override int Height => 300;
}

public sealed class TinyDimension : SizedDimension
{
    public override int Width => 250;

    public override int Height => 200;
}

public sealed class MicroDimension : SizedDimension
{
    public override int Width => 120;

    public override int Height => 100;
}

public sealed class CsSubMapMod : Mod
{
    const string Test = "cssubmap";

    static int frames, step, pass, fail;
    static nint seen;
    static bool done;

    static ILog Log => ModContent.GetInstance<CsSubMapMod>().Logger;

    static readonly Func<ModSubworld>[] order =
    {
        () => ModContent.GetInstance<WideDimension>(),
        () => ModContent.GetInstance<OceanDimension>(),
        () => ModContent.GetInstance<SmallDimension>(),
        () => ModContent.GetInstance<TinyDimension>(),
        () => ModContent.GetInstance<MicroDimension>(),
    };

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

    static string Name => SubworldLoader.Current?.Name.ToLowerInvariant() ?? "main";

    static void Step(int f)
    {
        int index = step / 2;
        bool inside = step % 2 == 1;
        if (!inside)
        {
            if (f != 120) return;
            if (index >= order.Length)
            {
                Report("the main world keeps its beaches", WorldGen.oceanDepths(20, (int)Main.worldSurface), "");
                done = true;
                return;
            }

            if (order[index]() is MicroDimension micro)
            {
                Report("a dimension under 200x160 is refused", !SubworldLoader.Enter(micro) && !SubworldLoader.Traveling, "");
                step += 2;
                frames = 0;
                return;
            }

            Report($"enter {order[index]().Name}", SubworldLoader.Enter(order[index]()), "");
            step++;
            return;
        }

        if (SubworldLoader.Current != order[index]()) return;
        Player player = Main.LocalPlayer;
        int surface = (int)Main.worldSurface;
        switch (f)
        {
            case 60 when index <= 1:
                player.position = new Vector2(30 * 16, (surface - 3) * 16);
                player.velocity = Vector2.Zero;
                break;
            case 180 when index == 0:
                Report("an edge of a dimension is not a beach", !player.ZoneBeach && !WorldGen.oceanDepths(30, surface), $"beach={player.ZoneBeach}");
                break;
            case 180 when index == 1:
                Report("control: HideOcean off keeps the beach at the edge", player.ZoneBeach && WorldGen.oceanDepths(30, surface), $"beach={player.ZoneBeach}");
                player.position = new Vector2(Main.spawnTileX * 16, (Main.spawnTileY - 3) * 16);
                break;
            case 200:
                Log.Info($"{Test} {Name} size={Main.maxTilesX}x{Main.maxTilesY} screen={Main.screenPosition} player={player.position}");
                Log.Info($"{Test} SHOT {Name}mini");
                break;
            case 500:
                GUIInstance.Active.GUIMap.OpenFullscreenMap();
                break;
            case 530:
                Main.mapFullscreenScale = 0.5f;
                Main.mapFullscreenPos = new Vector2(Main.maxTilesX / 2f, Main.maxTilesY / 2f);
                break;
            case 560:
                Log.Info($"{Test} SHOT {Name}full");
                break;
            case 860:
                GUIInstance.Active.GUIMap.CloseFullscreenMap();
                Report($"{Name} is still running", true, $"size={Main.maxTilesX}x{Main.maxTilesY}");
                SubworldLoader.Exit();
                step++;
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
