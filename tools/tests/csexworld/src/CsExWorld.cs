using System;
using log4net;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Enums;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CsExWorld;

public sealed class CsExWorldMod : Mod
{
    const int End = 700;

    static int frame, pass, fail, ground, cx;

    static ILog Log => ModContent.GetInstance<CsExWorldMod>().Logger;

    public override void Load() => On_Player.Update += Update;

    static void Report(string name, bool ok, string detail = "")
    {
        if (ok) pass++;
        else fail++;
        Log.Info($"csexworld {name}: {(ok ? "ok" : "FAIL")} {detail}");
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
        if (frame == End) Log.Info($"csexworld END pass={pass} fail={fail}");
    }

    static bool Place(string tile, int x, int y, int style = 0)
    {
        int type = TileType(tile);
        WorldGen.PlaceObject(x, y, type, true, style);
        Tile placed = Main.tile[x, y];
        return placed.HasTile && placed.TileType == type;
    }

    static bool Block(string tile, int x, int y)
    {
        int type = TileType(tile);
        WorldGen.PlaceTile(x, y, type, true, true);
        return Main.tile[x, y].HasTile && Main.tile[x, y].TileType == type;
    }

    static void Step(Player player)
    {
        switch (frame)
        {
            case 30:
                Main.dayTime = true;
                Main.time = 27000;
                ground = (int)((player.position.Y + player.height) / 16);
                cx = (int)(player.Center.X / 16);
                Sets();
                Placements();
                break;
            case 120:
                Log.Info("csexworld SHOT tiles");
                Rubble();
                Switches(player);
                break;
            case 200:
                Log.Info("csexworld SHOT later");
                Herb();
                break;
            case 300:
                Trap();
                break;
        }
    }

    static void Sets()
    {
        Report("the cut tile item does not become its drop", ItemID.Sets.DisableAutomaticPlaceableDrop[ItemType("ExampleCutTile")]);
        Report("the sunflower accepts the block", Array.IndexOf<int>(Terraria.ObjectData.TileObjectData.GetTileData(TileID.Sunflower, 0).AnchorValidTiles, TileType("ExampleBlock")) >= 0);
        Report("the crate item places its tile", new Item(ItemType("ExampleFishingCrate")).createTile == TileType("ExampleFishingCrate"));
        Report("a game text reference in a tooltip resolves", !ModContent.Find<ModItem>("ExampleMod/ExampleFishingCrate").Tooltip.Value.Contains("{$"), ModContent.Find<ModItem>("ExampleMod/ExampleFishingCrate").Tooltip.Value);
        Report("the example block drops its own item", TileLoader.GetItemDropFromTypeAndStyle(TileType("ExampleBlock")) == ItemType("ExampleBlock"));
    }

    static void Placements()
    {
        int x = cx - 18;
        Report("statue placed", Place("ExampleStatue", x, ground - 1));
        Report("crate placed", Place("ExampleFishingCrate", x + 4, ground - 1));
        Report("gemspark placed", Block("ExampleGemsparkBlockOn", x + 7, ground - 1));
        Report("transparent tile placed", Block("ExampleTransparentShapedTile", x + 9, ground - 1));
        Report("living fire placed", Block("ExampleLivingFireTile", x + 11, ground - 3));
        Report("sloped tile placed", Block("ExampleSlopeTile", x + 13, ground - 2));
        Block("ExampleBlock", x + 15, ground - 4);
        Report("animated tile placed", Place("ExampleAnimatedTile", x + 15, ground - 3));
        Report("animated glow tile placed", Place("ExampleAnimatedGlowmaskTile", x + 18, ground - 1));
        Report("exposed gem placed", Block("ExampleExposedGem", x + 21, ground - 1));
        Block("ExampleBlock", x + 24, ground - 5);
        Report("cut tile placed", Place("ExampleCutTile", x + 24, ground - 4));
        Report("trap placed", Block("ExampleTrap", x + 26, ground - 1));
        Report("herb seed placed", Place("ExampleHerb", x + 28, ground - 1));
    }

    static void Rubble()
    {
        int x = cx + 12;
        int small = ModContent.Find<ModTile>("ExampleMod/Example1x1RubbleFake").Type;
        Report("the fake rubble is placed by the small rubble wand", Place("Example1x1RubbleFake", x, ground - 1));
        Report("the natural rubble breaks", Place("Example3x2RubbleNatural", x + 2, ground - 1));
        int before = NPC.CountNPCS(NPCID.Worm);
        WorldGen.KillTile(x + 2, ground - 1);
        Report("the natural rubble dropped nothing and went", !Main.tile[x + 2, ground - 1].HasTile, $"worms {before}->{NPC.CountNPCS(NPCID.Worm)}");
    }

    static void Switches(Player player)
    {
        int x = cx - 18 + 13;
        int slope = TileType("ExampleSlopeTile");
        Main.tile[x, ground - 2].TileFrameX = 0;
        bool hit = Collision.SwitchTiles(player, new Vector2(x * 16 + 18, (ground - 2) * 16), 20, 20, new Vector2(x * 16 + 4, (ground - 2) * 16), 1);
        Report("SwitchTiles reaches the mod tile", hit && Main.tile[x, ground - 2].TileType == slope, $"hit={hit}");
        Report("the shake hook uses the tree type", WorldGen.GetTreeType(TileID.Grass) == TreeTypes.Forest);
    }

    static void Herb()
    {
        int x = cx - 18 + 28;
        Tile herb = Main.tile[x, ground - 1];
        short before = herb.TileFrameX;
        herb.TileFrameX = 18;
        WorldGen.KillTile(x, ground - 1);
        Report("the growing herb breaks", !Main.tile[x, ground - 1].HasTile, $"frame {before}->18");
    }

    static void Trap()
    {
        int x = cx - 18 + 26;
        ModTile trap = ModContent.Find<ModTile>("ExampleMod/ExampleTrap");
        int before = 0;
        for (int k = 0; k < Main.maxProjectiles; k++)
            if (Main.projectile[k].active) before++;
        trap.HitWire(x, ground - 1);
        int after = 0;
        for (int k = 0; k < Main.maxProjectiles; k++)
            if (Main.projectile[k].active) after++;
        Report("the trap shoots on a wire", after > before, $"{before}->{after}");
    }
}
