using System;
using System.Collections.Generic;
using System.Linq;
using BunnyLoader.Interop;
using log4net;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CsOutline;

public sealed class CsOutlineMod : Mod
{
    static readonly (string Mod, int Vanilla, bool Ceiling)[] Pairs =
    {
        ("ExampleChest", TileID.Containers, false),
        ("ExampleChair", TileID.Chairs, false),
        ("ExampleToilet", TileID.Toilets, false),
        ("ExampleBed", TileID.Beds, false),
        ("ExampleDresser", TileID.Dressers, false),
        ("ExampleDoorClosed", TileID.ClosedDoor, true),
    };

    static int frames, pass, fail, shots, floor, px, stage = -1, waitUntil;
    static bool done;
    static readonly Dictionary<string, string> vanillaSeen = new();

    static ILog Log => ModContent.GetInstance<CsOutlineMod>().Logger;

    public override void Load() => On_Player.Update += Update;

    static void Update(On_Player.orig_Update orig, Player self, int i)
    {
        orig(self, i);
        if (Main.gameMenu)
        {
            frames = 0;
            return;
        }
        if (i != Main.myPlayer || done) return;
        try { Step(++frames); }
        catch (Exception e)
        {
            Report("run", false, e.ToString());
            done = true;
        }
        if (done) Log.Info($"csoutline END pass={pass} fail={fail}");
    }

    static (string Name, int Type, bool Ceiling) Current()
    {
        (string mod, int vanilla, bool ceiling) = Pairs[stage / 2];
        if (stage % 2 == 0) return ($"vanilla of {mod}", vanilla, ceiling);
        return (mod, ModContent.TryFind<ModTile>("ExampleMod/" + mod, out ModTile? tile) ? tile.Type : -1, ceiling);
    }

    static void Step(int f)
    {
        if (f < 30) return;
        if (f == 30)
        {
            px = (int)(Main.LocalPlayer.Center.X / 16);
            floor = FindFloor(px);
            Clear();
            Next(f);
            return;
        }
        if (f == waitUntil - 180) Log.Info($"csoutline SHOT {++shots} {Current().Name}");
        if (f < waitUntil) return;
        Check();
        Clear();
        Next(f);
    }

    static void Clear()
    {
        for (int x = px - 2; x <= px + 8; x++)
            for (int y = floor - 6; y < floor; y++)
                WorldGen.KillTile(x, y, false, false, true);
    }

    static void Next(int f)
    {
        stage++;
        if (stage >= Pairs.Length * 2)
        {
            done = true;
            return;
        }
        (string name, int type, bool ceiling) = Current();
        if (ceiling)
            for (int x = px + 1; x <= px + 3; x++)
                WorldGen.PlaceTile(x, floor - 4, TileID.Stone, true, true);
        bool placed = type > 0 && WorldGen.PlaceObject(px + 2, floor - 1, type, true);
        if (!placed) Report($"{name} is placed", false, $"type={type}");
        waitUntil = f + 300;
    }

    static string Mask(int type)
    {
        var masks = TextureAssets.HighlightMask;
        if (type < 0 || type >= masks.Length) return "out of range";
        if (((GameArray<GameObject>)masks)[type].Pointer == IntPtr.Zero) return "no asset";
        Texture2D value = masks[type].Value;
        return value.IsNull ? "no texture" : "loaded";
    }

    static void Check()
    {
        (string name, int type, _) = Current();
        var selected = new GameDictionary(Main.SmartInteractTileCoordsSelected);
        var entries = selected.IsNull ? Array.Empty<KeyValuePair<int, int>>() : selected.IntEntries();
        bool chosen = entries.Any(e => Math.Abs((e.Key & 0xFFFF) - (px + 2)) <= 3 && e.Value != 0);
        string seen = $"outlines={type > 0 && TileID.Sets.HasOutlines[type]} mask={Mask(type)} selected={chosen}";
        Log.Info($"csoutline {name}: {seen}");
        if (stage % 2 == 0)
        {
            vanillaSeen[Pairs[stage / 2].Mod] = seen;
            return;
        }
        Report($"{name} is highlighted like the game's tile", vanillaSeen.TryGetValue(name, out string? vanilla) && vanilla == seen && chosen, $"mod: {seen} | game: {vanilla}");
    }

    static int FindFloor(int column)
    {
        for (int y = 50; y < Main.maxTilesY - 10; y++)
        {
            Tile tile = Main.tile[column, y];
            if (tile.HasTile && Main.tileSolid[tile.TileType]) return y;
        }
        return Main.spawnTileY;
    }

    static void Report(string name, bool ok, string detail)
    {
        if (ok) pass++;
        else fail++;
        Log.Info($"csoutline {name}: {(ok ? "ok" : "FAIL")} {detail}");
    }
}
