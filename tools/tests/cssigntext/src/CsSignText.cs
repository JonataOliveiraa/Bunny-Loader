using System;
using System.Collections.Generic;
using log4net;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CsSignText;

public sealed class CsSignTextMod : Mod
{
    static readonly (string Name, bool Mod, string Text)[] Stages =
    {
        ("warm-up", false, ""),
        ("vanilla text", false, "Bunny Loader"),
        ("mod text", true, "Bunny Loader"),
        ("vanilla empty", false, ""),
        ("mod empty", true, ""),
    };

    static int frames, pass, fail, taps, floor, px, stage = -1, start, signId;
    static bool done, smartCursor, hovered, icon, opened;
    static int iconId, hover;
    static Item? kept;
    static readonly Dictionary<bool, (bool Icon, bool Hover, bool Opened)> vanillaSeen = new();

    static ILog Log => ModContent.GetInstance<CsSignTextMod>().Logger;

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
        try { Step(self, ++frames); }
        catch (Exception e)
        {
            Report("run", false, e.ToString());
            done = true;
        }
        if (!done) return;
        Main.SmartCursorWanted = smartCursor;
        if (kept != null) self.inventory[self.selectedItem] = kept.Value;
        Log.Info($"cssigntext END pass={pass} fail={fail}");
    }

    static void Step(Player player, int f)
    {
        if (f < 180) return;
        if (f == 180)
        {
            smartCursor = Main.SmartCursorWanted;
            Main.SmartCursorWanted = true;
            kept = player.inventory[player.selectedItem];
            player.inventory[player.selectedItem] = new Item(ItemID.Gel);
            px = (int)(player.Center.X / 16);
            floor = FindFloor(px);
            Next(f);
            return;
        }
        int t = f - start;
        Vector2 center = SignCenter();
        if (t == 60)
        {
            Vector2 at = ScreenFraction(center);
            Log.Info($"cssigntext FHOLD {++taps} {at.X:0.0000} {at.Y:0.0000} 2500");
        }
        if (t > 60 && t < 420 && OnSign(Player.tileTargetX, Player.tileTargetY))
        {
            hovered = true;
            icon = player.cursorItemIconEnabled;
            iconId = player.cursorItemIconID;
            hover = Main.signHover;
        }
        if (t == 420)
        {
            Vector2 at = ScreenFraction(center);
            Log.Info($"cssigntext FTAP {++taps} {at.X:0.0000} {at.Y:0.0000}");
        }
        if (t > 420 && t < 660 && player.sign == signId && signId >= 0)
            opened = true;
        if (t == 600) Log.Info($"cssigntext SHOT {++taps} {Stages[stage].Name}");
        if (t < 660) return;
        Check(player);
        Next(f);
    }

    static void Check(Player player)
    {
        (string name, bool mod, string text) = Stages[stage];
        Log.Info($"cssigntext {name}: hovered={hovered} icon={icon} id={iconId} hover={hover} opened={opened} sign={signId}");
        player.sign = -1;
        Main.editSign = false;
        Main.npcChatText = "";
        if (stage == 0)
            return;
        if (!mod)
        {
            vanillaSeen[text.Length > 0] = (icon, hover >= 0, opened);
            return;
        }
        bool known = vanillaSeen.TryGetValue(text.Length > 0, out var game);
        Report($"{name} shows the cursor icon like the game's sign", known && hovered && icon == game.Icon && (hover >= 0) == game.Hover, $"mod: icon={icon} hover={hover >= 0} | game: icon={game.Icon} hover={game.Hover}");
        Report($"{name} opens on tap like the game's sign", known && opened == game.Opened, $"mod: {opened} | game: {game.Opened}");
    }

    static void Next(int f)
    {
        for (int x = px - 2; x <= px + 8; x++)
            for (int y = floor - 6; y < floor; y++)
                WorldGen.KillTile(x, y, false, false, true);
        stage++;
        if (stage >= Stages.Length)
        {
            done = true;
            return;
        }
        (string name, bool mod, string text) = Stages[stage];
        int type = mod ? (ModContent.TryFind<ModTile>("ExampleMod/ExampleSign", out ModTile? tile) ? tile.Type : -1) : TileID.Signs;
        bool placed = type > 0 && WorldGen.PlaceObject(px + 2, floor - 1, type, true);
        signId = placed ? Sign.ReadSign(px + 2, floor - 1, true) : -1;
        if (signId >= 0)
            Sign.TextSign(signId, text);
        if (signId < 0) Report($"{name} is placed", false, $"type={type}");
        start = f;
        hovered = icon = opened = false;
        iconId = 0;
        hover = -1;
    }

    static Vector2 SignCenter() => signId >= 0 ? new Vector2(Main.sign[signId].x * 16 + 16, Main.sign[signId].y * 16 + 16) : Vector2.Zero;

    static bool OnSign(int x, int y) => signId >= 0 && x >= Main.sign[signId].x && x <= Main.sign[signId].x + 1 && y >= Main.sign[signId].y && y <= Main.sign[signId].y + 1;

    static Vector2 ScreenFraction(Vector2 world)
    {
        var size = new Vector2(Main.screenWidth, Main.screenHeight);
        Vector2 zoom = Main.GameViewMatrix.Zoom;
        Vector2 screen = (world - Main.screenPosition - size / 2f) * zoom + size / 2f;
        return screen / size;
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
        Log.Info($"cssigntext {name}: {(ok ? "ok" : "FAIL")} {detail}");
    }
}
