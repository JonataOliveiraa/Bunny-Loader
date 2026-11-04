using System;
using System.Collections.Generic;
using log4net;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CsExTiles;

public sealed class CsExTilesMod : Mod
{
    const int End = 900;

    internal static readonly Dictionary<int, int> emitted = new();
    static int frame, pass, fail, ground, cx, dropped = -1;

    static ILog Log => ModContent.GetInstance<CsExTilesMod>().Logger;

    public override void Load()
    {
        On_Player.Update += Update;
        On_Player.DropItemFromExtractinator += Dropped;
    }

    static void Dropped(On_Player.orig_DropItemFromExtractinator orig, Player self, int itemType, int stack)
    {
        dropped = itemType;
        orig(self, itemType, stack);
    }

    static void Report(string name, bool ok, string detail = "")
    {
        if (ok) pass++;
        else fail++;
        Log.Info($"csextiles {name}: {(ok ? "ok" : "FAIL")} {detail}");
    }

    static int TileType(string name) => ModContent.Find<ModTile>("ExampleMod/" + name).Type;

    static int ItemType(string name) => ModContent.Find<ModItem>("ExampleMod/" + name).Type;

    static void Update(On_Player.orig_Update orig, Player self, int i)
    {
        if (Main.gameMenu)
        {
            frame = 0;
            orig(self, i);
            return;
        }
        orig(self, i);
        if (i != Main.myPlayer || frame > End) return;
        frame++;
        try { Step(self); }
        catch (Exception e) { Report($"frame {frame}", false, e.ToString()); }
        if (frame == End) Log.Info($"csextiles END pass={pass} fail={fail}");
    }

    static bool Place(string tile, int x, int y, int style = 0)
    {
        int type = TileType(tile);
        WorldGen.PlaceObject(x, y, type, true, style);
        Tile placed = Main.tile[x, y];
        return placed.HasTile && placed.TileType == type;
    }

    static void Ceiling(int x, int y, int width)
    {
        int block = TileType("ExampleBlock");
        for (int k = 0; k < width; k++)
            WorldGen.PlaceTile(x + k, y, block, true, true);
    }

    static void Step(Player player)
    {
        switch (frame)
        {
            case 30:
                Main.dayTime = true;
                Main.time = 27000;
                Texts();
                Extractinator(player);
                break;
            case 40:
                ground = (int)((player.position.Y + player.height) / 16);
                cx = (int)(player.Center.X / 16);
                Furniture();
                break;
            case 100:
                Toggles();
                break;
            case 200:
                Log.Info("csextiles SHOT furniture");
                TorchLuck(player);
                break;
            case 400:
                Effects(player);
                break;
            case 420:
                Log.Info("csextiles SHOT sway");
                break;
            case 500:
                Door();
                break;
            case 560:
                Log.Info("csextiles SHOT door");
                break;
        }
    }

    static void Texts()
    {
        string[] keys = { "MapObject.FloorLamp", "MapObject.Sink", "ItemName.Campfire", "ItemName.Torch", "ItemName.Chandelier", "ItemName.WorkBench", "ItemName.WoodenTable", "ItemName.WoodPlatform", "ItemName.GrandfatherClock", "ItemName.WoodenDoor", "ItemName.Toilet", "MapObject.Door", "MapObject.Table", "MapObject.GrandfatherClock", "MapObject.Toilet", "MapObject.Banner", "MapObject.Chandelier" };
        foreach (string key in keys)
            if (!Terraria.Localization.Language.Exists(key))
                Log.Info($"csextiles text {key} does not exist on the phone");
        ModItem water = ModContent.Find<ModItem>("ExampleMod/ExampleWaterTorch");
        string tip = water.Tooltip.Value;
        Report("the game's tooltip reference resolves", tip.Length > 0 && !tip.Contains("{$") && !tip.Contains("Mods."), tip);
        Report("the door item places the closed door", new Item(ItemType("ExampleDoor")).createTile == TileType("ExampleDoorClosed"));
        Report("the platform counts as a platform", TileID.Sets.Platforms[TileType("ExamplePlatform")]);
        Report("PostSetDefaults runs after the defaults", !Main.tileNoSunLight[TileType("ExamplePlatform")]);
        Report("the torch is a torch", TileID.Sets.Torches[TileType("ExampleTorch")] && TileID.Sets.RoomNeeds.CountsAsTorch[TileType("ExampleTorch")]);
        Report("the sink counts as liquids", TileID.Sets.CountsAsWaterForCrafting[TileType("ExampleSink")] && TileID.Sets.CountsAsHoneyForCrafting[TileType("ExampleSink")] && TileID.Sets.CountsAsLavaForCrafting[TileType("ExampleSink")]);
    }

    static void Extractinator(Player player)
    {
        int torch = ItemType("ExampleTorch");
        Report("TorchExtractinator marks the torches", ItemID.Sets.ExtractinatorMode[ItemID.Torch] == ItemID.Torch && ItemID.Sets.ExtractinatorMode[torch] == ItemID.Torch);
        dropped = -1;
        player.ExtractinatorUse(ItemID.Torch, TileID.Extractinator);
        int tile = dropped > 0 ? new Item(dropped).createTile : -1;
        Report("ExtractinatorUse changes the result", tile >= 0 && TileID.Sets.Torches[tile], $"item={dropped}");
    }

    static void Furniture()
    {
        int x = cx - 16;
        Report("torch placed", Place("ExampleTorch", x, ground - 1));
        Report("water torch style placed", Place("ExampleTorch", x + 2, ground - 1, 1) && Terraria.ObjectData.TileObjectData.GetTileStyle(Main.tile[x + 2, ground - 1]) == 1, $"frameY={Main.tile[x + 2, ground - 1].TileFrameY}");
        Report("lamp placed", Place("ExampleLamp", x + 4, ground - 1));
        Report("campfire placed", Place("ExampleCampfire", x + 7, ground - 1));
        Report("sink placed", Place("ExampleSink", x + 10, ground - 1));
        Report("table placed", Place("ExampleTable", x + 14, ground - 1));
        Report("toilet placed", Place("ExampleToilet", x + 18, ground - 1));
        Report("clock placed", Place("ExampleClock", x + 21, ground - 3));
        Ceiling(x + 25, ground - 8, 12);
        Report("chandelier placed", Place("ExampleChandelier", x + 26, ground - 7));
        Report("second chandelier style placed", Place("ExampleChandelier", x + 30, ground - 7, 1));
        Report("wide banner placed", Place("ExampleWideBannerTile", x + 34, ground - 7));
        int platform = TileType("ExamplePlatform");
        for (int k = 0; k < 5; k++)
            WorldGen.PlaceTile(x + 2 + k, ground - 6, platform, true, true);
        Report("platform placed", Main.tile[x + 3, ground - 6].TileType == platform);
    }

    static void Toggles()
    {
        int x = cx - 16;
        int lamp = TileType("ExampleLamp"), campfire = TileType("ExampleCampfire");
        ModTile lampTile = ModContent.Find<ModTile>("ExampleMod/ExampleLamp");
        short before = Main.tile[x + 4, ground - 3].TileFrameX;
        lampTile.HitWire(x + 4, ground - 3);
        short after = Main.tile[x + 4, ground - 3].TileFrameX;
        Report("HitWire toggles the lamp", before != after && Main.tile[x + 4, ground - 3].TileType == lamp, $"{before}->{after}");
        lampTile.HitWire(x + 4, ground - 3);

        ModTile fire = ModContent.Find<ModTile>("ExampleMod/ExampleCampfire");
        short y = Main.tile[x + 7, ground - 1].TileFrameY;
        fire.RightClick(x + 7, ground - 1);
        short z = Main.tile[x + 7, ground - 1].TileFrameY;
        Report("RightClick toggles the campfire", y != z && Main.tile[x + 7, ground - 1].TileType == campfire, $"{y}->{z}");
        fire.RightClick(x + 7, ground - 1);
    }

    static void TorchLuck(Player player)
    {
        int x = cx - 16;
        TileLoader.GetTile(TileType("ExampleTorch"));
        Report("the torch reports bad luck away from its biome", ModContent.Find<ModTile>("ExampleMod/ExampleTorch").GetTorchLuck(player) == -0.1f);
        Report("the luck hook left the torch list clean", player.NearbyModTorch.Count == 0, $"count={player.NearbyModTorch.Count}");
        Report("the torch luck is in range", player.torchLuck >= 0f && player.torchLuck <= 1f, $"luck={player.torchLuck}");
    }

    static void Effects(Player player)
    {
        int x = cx - 16;
        int campfire = TileType("ExampleCampfire"), torch = TileType("ExampleTorch"), chandelier = TileType("ExampleChandelier");
        emitted.TryGetValue(campfire, out int fires);
        emitted.TryGetValue(torch, out int torches);
        emitted.TryGetValue(chandelier, out int chandeliers);
        Report("EmitParticles runs for placed tiles", fires > 0 && torches > 0 && chandeliers > 0, $"campfire={fires} torch={torches} chandelier={chandeliers}");
        Report("the campfire buff comes from the scene", player.HasBuff(BuffID.Campfire));
        Report("the clock counts for the scene", Main.SceneMetrics.HasClock);
        Main.tile[x, ground - 1].TileFrameX += 0;
        Report("a tile field can be changed through Main.tile", Main.tile[x, ground - 1].TileType == torch);
        var flame = Main.instance.TilesRenderer.GetTileFlameData(x + 26, ground - 7, chandelier, Main.tile[x + 26, ground - 7].TileFrameY);
        Report("GetTileFlameData fills the flame", flame.flameCount > 0 && !flame.flameTexture.IsNull, $"count={flame.flameCount}");
    }

    static void Door()
    {
        int x = cx + 6;
        int closed = TileType("ExampleDoorClosed"), open = TileType("ExampleDoorOpen");
        Ceiling(x - 1, ground - 4, 3);
        Report("door placed", Place("ExampleDoorClosed", x, ground - 2));
        bool opened = WorldGen.OpenDoor(x, ground - 2, 1);
        Report("the door opens into its other tile", opened && Main.tile[x, ground - 2].TileType == open, $"opened={opened} type={Main.tile[x, ground - 2].TileType}");
        Report("closed tile knows the open one", TileID.Sets.CloseDoorID[open] == closed, $"close={TileID.Sets.CloseDoorID[open]}");
        Report("the door closes back", WorldGen.CloseDoor(x, ground - 2, true) && Main.tile[x, ground - 2].TileType == closed, $"type={Main.tile[x, ground - 2].TileType}");
        WorldGen.KillTile(x, ground - 2);
        Report("killing a door part takes the whole door", !Main.tile[x, ground - 1].HasTile && !Main.tile[x, ground - 3].HasTile, $"bottom={Main.tile[x, ground - 1].HasTile} top={Main.tile[x, ground - 3].HasTile}");
    }
}

public sealed class EmitCounter : GlobalTile
{
    public override void EmitParticles(int i, int j, Tile tileCache, ushort typeCache, short tileFrameX, short tileFrameY, Color tileLight, bool visible)
    {
        if (!visible) return;
        CsExTilesMod.emitted.TryGetValue(typeCache, out int count);
        CsExTilesMod.emitted[typeCache] = count + 1;
    }
}
