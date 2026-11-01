using System;
using System.Collections.Generic;
using BunnyLoader;
using BunnyLoader.Interop;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria.Enums;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ObjectData;

namespace Terraria.ModLoader.Core;

internal static unsafe class TreeHooks
{
    static readonly Dictionary<int, ModTree> byGround = new();

    static bool ready;

    internal static ModTree? OfGround(int type) => byGround.GetValueOrDefault(type);

    internal static ModTree? At(int i, int j)
    {
        WorldGen.GetTreeBottom(i, j, out int x, out int y);
        Tile ground = Main.tile[x, y];
        return ground.active() ? OfGround(ground.type) : null;
    }

    static bool Overrides(ModTree tree, string name)
    {
        var method = tree.GetType().GetMethod(name, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        return method != null && method.DeclaringType != typeof(ModTree) && method.GetBaseDefinition().DeclaringType == typeof(ModTree);
    }

    internal static void Setup()
    {
        if (ready)
            return;
        ready = true;
        var trees = new List<(ModTree Tree, Mod Mod)>();
        foreach (var (plant, mod) in PlantLoader.plantList)
            if (plant is ModTree tree)
                trees.Add((tree, mod));
        if (trees.Count == 0)
            return;

        int first = TextureAssets.TreeTop.Length;
        GameArray<GameObject> tops = TextureAssets.TreeTop.Resized(first + trees.Count);
        GameArray<GameObject> branches = TextureAssets.TreeBranch.Resized(first + trees.Count);
        var installed = new List<(ModTree Tree, IntPtr Trunk, int Style)>();
        for (int k = 0; k < trees.Count; k++)
        {
            var (tree, mod) = trees[k];
            string name = tree.GetType().Name;
            try
            {
                tree.SetStaticDefaults();
                Asset<Texture2D> trunk = tree.GetTexture(), top = tree.GetTopTextures(), branch = tree.GetBranchTextures();
                trunk.Wait();
                top.Wait();
                branch.Wait();
                tops[first + k] = new GameObject(top.GameAsset);
                branches[first + k] = new GameObject(branch.GameAsset);
                installed.Add((tree, ((IGameObject)trunk.Value).Pointer, first + k));
            }
            catch (Exception e)
            {
                ItemHooks.Report($"{mod.Name}/{name}.SetStaticDefaults", e);
            }
        }

        TextureAssets.TreeTop = tops;
        TextureAssets.TreeBranch = branches;
        foreach (var (tree, trunk, style) in installed)
        {
            foreach (int ground in tree.GrowsOnTileId ?? Array.Empty<int>())
            {
                if (byGround.TryGetValue(ground, out ModTree? other))
                {
                    Log.Error($"ModTree: {tree.GetType().Name} and {other.GetType().Name} both grow on tile {ground}; {tree.GetType().Name} was left out there");
                    continue;
                }

                byGround[ground] = tree;
                PlantLoader.plantLookup[(TileID.Trees, ground)] = tree;
                Host.Native()->SetModTree(ground, trunk, style);
            }
        }

        if (byGround.Count == 0)
            return;
        On_WorldGen.IsTileTypeFitForTree += FitForTree;
        On_WorldGen.GetTreeType_int += TreeType;
        On_WorldGen.GrowTree += GrowTree;
        On_WorldGen.AttemptToGrowTreeFromSapling += GrowSapling;
        On_TileObject.CanPlace += CanPlace;
        On_WorldGen.KillTile_GetTreeDrops += Drops;
        On_WorldGen.GetTreeLeaf += Leaf;
        foreach (ModTree tree in byGround.Values)
        {
            if (Overrides(tree, nameof(ModTree.CreateDust)))
                On_WorldGen.KillTile_MakeTileDust += Dust;
            if (Overrides(tree, nameof(ModTree.Shake)))
                On_WorldGen.ShakeTree += Shake;
            break;
        }
    }

    static bool FitForTree(On_WorldGen.orig_IsTileTypeFitForTree orig, ushort type) => byGround.ContainsKey(type) || orig(type);

    static TreeTypes TreeType(On_WorldGen.orig_GetTreeType_int orig, int type) => OfGround(type) is ModTree tree ? tree.CountsAsTreeType : orig(type);

    static bool GrowTree(On_WorldGen.orig_GrowTree orig, int i, int y, int addon, bool walls)
    {
        int type = Main.tile[i, y].type;
        if (type == TileID.Saplings || !TileID.Sets.TreeSapling[type])
            return orig(i, y, addon, walls);
        var cells = new List<int>();
        for (int k = y; Main.tile[i, k].active() && Main.tile[i, k].type == type; k++)
            cells.Add(k);
        for (int k = y - 1; Main.tile[i, k].active() && Main.tile[i, k].type == type; k--)
            cells.Add(k);
        foreach (int k in cells)
        {
            Tile cell = Main.tile[i, k];
            cell.type = TileID.Saplings;
        }

        bool grew = orig(i, y, addon, walls);
        if (!grew)
            foreach (int k in cells)
            {
                Tile cell = Main.tile[i, k];
                cell.type = (ushort)type;
            }
        return grew;
    }

    static bool GrowSapling(On_WorldGen.orig_AttemptToGrowTreeFromSapling orig, int x, int y, bool underground, int addon, bool walls)
    {
        Tile tile = Main.tile[x, y];
        if (!tile.active() || TileLoader.GetTile(tile.type) is not ModTile sapling || !TileID.Sets.TreeSapling[tile.type])
            return orig(x, y, underground, addon, walls);
        if (Main.netMode == NetmodeID.MultiplayerClient)
            return false;
        return sapling.GrowSapling(x, y, underground, addon, walls);
    }

    static bool CanPlace(On_TileObject.orig_CanPlace orig, int x, int y, int type, int style, int dir, out TileObject objectData, bool onlyCheck, GameNullable<int> forcedRandom)
    {
        Tile below = Main.tile[x, y + 1];
        if (type == TileID.Saplings && below.active() && OfGround(below.type) is ModTree tree)
        {
            int sapling = style;
            type = tree.SaplingGrowthType(ref sapling);
            style = sapling;
        }

        return orig(x, y, type, style, dir, out objectData, onlyCheck, forcedRandom);
    }

    static void Drops(On_WorldGen.orig_KillTile_GetTreeDrops orig, int i, int j, Tile tile, ref bool wood, ref bool bonus, ref int drop, ref int second)
    {
        orig(i, j, tile, ref wood, ref bonus, ref drop, ref second);
        if (At(i, j) is not ModTree tree)
            return;
        if (wood)
            drop = tree.DropWood();
        if (!tree.CanDropAcorn())
        {
            if (second == ItemID.Acorn)
                second = 0;
        }
        else if (second == 0 && tile.frameX >= 22 && tile.frameY >= 198 && Main.netMode != NetmodeID.MultiplayerClient && WorldGen.genRand.NextBool())
            second = ItemID.Acorn;
    }

    static void Leaf(On_WorldGen.orig_GetTreeLeaf orig, int x, Tile top, Tile ground, ref int height, out int frame, out int pass)
    {
        orig(x, top, ground, ref height, out frame, out pass);
        if (OfGround(ground.type) is ModTree tree)
        {
            int leaf = tree.TreeLeaf();
            pass = leaf >= 0 ? leaf : -1;
        }
    }

    static int Dust(On_WorldGen.orig_KillTile_MakeTileDust orig, int i, int j, Tile tile)
    {
        if (tile.type != TileID.Trees || At(i, j) is not ModTree tree || !Overrides(tree, nameof(ModTree.CreateDust)))
            return orig(i, j, tile);
        int dust = tree.CreateDust();
        return dust >= 0 ? Terraria.Dust.NewDust(new Vector2(i * 16, j * 16), 16, 16, dust, 0, 0, 0, default, 1) : 6000;
    }

    static void Shake(On_WorldGen.orig_ShakeTree orig, int i, int j)
    {
        if (!ShakeModTree(i, j))
            orig(i, j);
    }

    static bool ShakeModTree(int i, int j)
    {
        WorldGen.GetTreeBottom(i, j, out int x, out int bottom);
        Tile ground = Main.tile[x, bottom];
        if (!ground.active() || OfGround(ground.type) is not ModTree tree || !Overrides(tree, nameof(ModTree.Shake)))
            return false;
        int count = WorldGen.numTreeShakes;
        if (count == WorldGen.maxTreeShakes)
            return true;
        for (int k = 0; k < count; k++)
            if (WorldGen.treeShakeX[k] == x && WorldGen.treeShakeY[k] == bottom)
                return true;
        int top = bottom - 1;
        while (top > 10 && Main.tile[x, top].active() && TileID.Sets.IsShakeable[Main.tile[x, top].type])
            top--;
        top++;
        if (!WorldGen.IsTileALeafyTreeTop(x, top) || Collision.SolidTiles(x - 2, x + 2, top - 2, top + 2))
            return false;
        bool leaves = true;
        if (tree.Shake(x, top, ref leaves))
            return false;
        WorldGen.treeShakeX[count] = x;
        WorldGen.treeShakeY[count] = bottom;
        WorldGen.numTreeShakes = count + 1;
        if (!leaves)
            return true;
        int leaf = tree.TreeLeaf();
        if (leaf < 0)
            return true;
        if ((Main.netMode & 2) != 0)
            NetMessage.SendData(MessageID.SpecialFX, -1, -1, default, 1, x, top, 1, leaf);
        if (Main.netMode != NetmodeID.MultiplayerClient && Main.netMode != NetmodeID.Server)
            WorldGen.TreeGrowFX(x, top, 1, leaf, true);
        return true;
    }
}
