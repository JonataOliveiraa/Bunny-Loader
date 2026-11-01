using System;
using log4net;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace CsTree;

public sealed class TestTree : ModTree
{
    public static int Setups, Shakes, Dusts, Woods;

    Asset<Texture2D> trunk = null!, tops = null!, branches = null!;

    public override TreePaintingSettings TreeShaderSettings => new TreePaintingSettings { UseSpecialGroups = true };

    public override void SetStaticDefaults()
    {
        Setups++;
        GrowsOnTileId = [ModContent.TileType<TestGround>()];
        trunk = ModContent.Request<Texture2D>("CsTree/TestTree");
        tops = ModContent.Request<Texture2D>("CsTree/TestTree_Tops");
        branches = ModContent.Request<Texture2D>("CsTree/TestTree_Branches");
    }

    public override Asset<Texture2D> GetTexture() => trunk;

    public override Asset<Texture2D> GetTopTextures() => tops;

    public override Asset<Texture2D> GetBranchTextures() => branches;

    public override int SaplingGrowthType(ref int style)
    {
        style = 0;
        return ModContent.TileType<TestSapling>();
    }

    public override int DropWood()
    {
        Woods++;
        return ItemID.Gel;
    }

    public override int CreateDust()
    {
        Dusts++;
        return DustID.Stone;
    }

    public override int TreeLeaf() => GoreID.TreeLeaf_Normal;

    public override bool Shake(int x, int y, ref bool createLeaves)
    {
        Shakes++;
        return false;
    }
}

public sealed class TestGround : ModTile
{
    public override void SetStaticDefaults()
    {
        Main.tileSolid[Type] = true;
        Main.tileBlockLight[Type] = true;
        AddMapEntry(new Color(120, 120, 200));
    }
}

public sealed class TestSapling : ModTile
{
    public static int Grows;

    public override void SetStaticDefaults()
    {
        Main.tileFrameImportant[Type] = true;
        Main.tileNoAttach[Type] = true;
        TileObjectData.newTile.CopyFrom(TileObjectData.Style1x2);
        TileObjectData.newTile.Origin = new Point16(0, 1);
        TileObjectData.newTile.CoordinateHeights = [16, 18];
        TileObjectData.newTile.AnchorValidTiles = [ModContent.TileType<TestGround>()];
        TileObjectData.addTile(Type);
        TileID.Sets.TreeSapling[Type] = true;
        TileID.Sets.CommonSapling[Type] = true;
        AdjTiles = [TileID.Saplings];
    }

    public override bool GrowSapling(int i, int j, bool underground, int treeHeightAddon, bool ignoreWalls)
    {
        Grows++;
        return WorldGen.GrowTree(i, j, treeHeightAddon, ignoreWalls);
    }
}

public sealed class CsTreeMod : Mod
{
    static int frames, pass, fail, x, floor;
    static bool done;

    static ILog Log => ModContent.GetInstance<CsTreeMod>().Logger;

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
        if (done) Log.Info($"cstree END pass={pass} fail={fail}");
    }

    static int Ground => ModContent.TileType<TestGround>();

    static void Step(int f)
    {
        switch (f)
        {
            case 10:
                x = Main.spawnTileX + 10;
                floor = FindFloor(x);
                Registration();
                break;
            case 20:
                Prepare();
                Sapling();
                break;
            case 30:
                Grow();
                break;
            case 40:
                Foliage();
                ShakeIt();
                Cut();
                Regrow();
                done = true;
                break;
        }
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

    static void Registration()
    {
        var tree = ModContent.GetInstance<TestTree>();
        Report("the tree is set up once and found through PlantLoader", TestTree.Setups == 1 && PlantLoader.Get<ModTree>(TileID.Trees, Ground) == tree,
            $"setups={TestTree.Setups} ground={Ground} tops={TextureAssets.TreeTop.Length}");
        Report("its ground is fit for trees and counts as TreeTypes.Custom", WorldGen.IsTileTypeFitForTree((ushort)Ground) && WorldGen.GetTreeType(Ground) == TreeTypes.Custom && !WorldGen.IsTileTypeFitForTree((ushort)ModContent.TileType<TestSapling>()),
            $"type={WorldGen.GetTreeType(Ground)}");
    }

    static void Prepare()
    {
        for (int i = x - 3; i <= x + 3; i++)
        {
            for (int j = floor - 25; j < floor; j++)
                WorldGen.KillTile(i, j, false, false, true);
            WorldGen.KillTile(i, floor, false, false, true);
            WorldGen.PlaceTile(i, floor, Ground, true, true);
        }
        for (int k = 0; k < Main.item.Length; k++)
            if (Main.item[k].active)
                Main.item[k].TurnToAir();
    }

    static void Sapling()
    {
        bool can = TileObject.CanPlace(x, floor - 1, TileID.Saplings, 0, 1, out TileObject data, false, default);
        bool placed = can && TileObject.Place(data);
        Tile tile = Main.tile[x, floor - 1];
        Report("an acorn on the tree's ground becomes its sapling", Main.tile[x, floor].type == Ground && placed && tile.HasTile && tile.TileType == ModContent.TileType<TestSapling>(),
            $"can={can} placed={placed} type={tile.TileType} sapling={ModContent.TileType<TestSapling>()}");
    }

    static void Grow()
    {
        bool grew = false;
        for (int attempt = 0; attempt < 10 && !grew; attempt++)
            grew = WorldGen.AttemptToGrowTreeFromSapling(x, floor - 1, false, 0, true);
        Report("GrowSapling grows a tree on the mod ground", grew && TestSapling.Grows > 0 && Main.tile[x, floor - 1].type == TileID.Trees && Main.tile[x, floor - 4].type == TileID.Trees,
            $"grew={grew} grows={TestSapling.Grows} bottom={Main.tile[x, floor - 1].type}");
    }

    static int Top()
    {
        int y = floor - 1;
        while (y > 10 && Main.tile[x, y - 1].HasTile && Main.tile[x, y - 1].TileType == TileID.Trees) y--;
        return y;
    }

    static void Foliage()
    {
        int top = Top();
        int frame = 0, style = 0;
        bool found = WorldGen.GetCommonTreeFoliageData(x, top, 0, ref frame, ref style, out int floorY, out int width, out int height);
        Report("the tree top uses the mod's top texture", found && style >= TextureAssets.TreeTop.Length - 1 && floorY == floor && width == 80,
            $"found={found} style={style} tops={TextureAssets.TreeTop.Length} floor={floorY}/{floor}");
        int treeHeight = 1;
        WorldGen.GetTreeLeaf(x, Main.tile[x, top], Main.tile[x, floor], ref treeHeight, out _, out int leaf);
        Report("TreeLeaf sets the falling leaf gore", leaf == GoreID.TreeLeaf_Normal, $"leaf={leaf}");
    }

    static void ShakeIt()
    {
        int shakes = WorldGen.numTreeShakes;
        WorldGen.ShakeTree(x, floor - 2);
        WorldGen.ShakeTree(x, floor - 2);
        Report("Shake runs once per tree and marks it shaken", TestTree.Shakes == 1 && WorldGen.numTreeShakes == shakes + 1, $"shakes={TestTree.Shakes} marked={WorldGen.numTreeShakes - shakes}");
    }

    static void Cut()
    {
        int dusts = TestTree.Dusts, woods = TestTree.Woods;
        WorldGen.KillTile(x, floor - 1);
        int gel = 0;
        for (int k = 0; k < Main.item.Length; k++)
            if (Main.item[k].active && Main.item[k].type == ItemID.Gel)
                gel += Main.item[k].stack;
        Report("cutting the tree drops DropWood and makes CreateDust's dust", Main.tile[x, floor - 1].type != TileID.Trees && TestTree.Woods > woods && gel > 0 && TestTree.Dusts > dusts,
            $"woods={TestTree.Woods - woods} gel={gel} dusts={TestTree.Dusts - dusts}");
    }

    static void Regrow()
    {
        TileObject.CanPlace(x, floor - 1, TileID.Saplings, 0, 1, out TileObject data, false, default);
        TileObject.Place(data);
        bool grew = false;
        for (int attempt = 0; attempt < 10 && !grew; attempt++)
            grew = WorldGen.AttemptToGrowTreeFromSapling(x, floor - 1, false, 4, true);
        Log.Info($"cstree regrown for the screenshot: {grew} at {x},{floor}");
    }

    static void Report(string name, bool ok, string detail)
    {
        if (ok) pass++;
        else fail++;
        Log.Info($"cstree {name}: {(ok ? "ok" : "FAIL")} {detail}");
    }
}
