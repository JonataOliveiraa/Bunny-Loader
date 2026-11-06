using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria.Enums;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader.Core;

namespace Terraria.ModLoader;

public interface IPlant : ILoadable
{
    public int PlantTileId { get; }

    public int VanillaCount { get; }

    public int[] GrowsOnTileId { get; set; }

    public abstract Asset<Texture2D> GetTexture();

    public abstract void SetStaticDefaults();

    void ILoadable.Load(Mod mod)
    {
        PlantLoader.Add(this, mod);
    }

    void ILoadable.Unload()
    {
    }
}

public interface ITree : IPlant
{
    TreeTypes CountsAsTreeType { get; }

    int TreeLeaf();

    bool Shake(int x, int y, ref bool createLeaves);
}

public static class PlantLoader
{
    internal static readonly List<(IPlant Plant, Mod Mod)> plantList = new();

    internal static readonly Dictionary<(int, int), IPlant> plantLookup = new();

    internal static void Add(IPlant plant, Mod mod) => plantList.Add((plant, mod));

    public static T Get<T>(int plantTileID, int growsOnTileID) where T : IPlant
    {
        return plantLookup.TryGetValue((plantTileID, growsOnTileID), out IPlant? plant) ? (T)plant : default!;
    }

    public static bool Exists(int plantTileID, int growsOnTileID) => plantLookup.ContainsKey((plantTileID, growsOnTileID));

    public static Asset<Texture2D>? GetCactusFruitTexture(int type) => Get<ModCactus>(TileID.Cactus, type)?.GetFruitTexture();

    internal static void FinishSetup()
    {
        TreeHooks.Setup();
        CactusHooks.Setup();
    }
}

public abstract class ModCactus : IPlant
{
    public int PlantTileId => TileID.Cactus;

    public int VanillaCount => 1;

    public int[] GrowsOnTileId { get; set; } = Array.Empty<int>();

    public abstract void SetStaticDefaults();

    public abstract Asset<Texture2D> GetTexture();

    public abstract Asset<Texture2D> GetFruitTexture();
}

public abstract class ModTree : ITree
{
    public int PlantTileId => TileID.Trees;

    public const int VanillaStyleCount = 7;

    public int VanillaCount => VanillaStyleCount;

    public const int VanillaTopTextureCount = 100;

    public abstract TreePaintingSettings TreeShaderSettings { get; }

    public int[] GrowsOnTileId { get; set; } = Array.Empty<int>();

    public abstract void SetStaticDefaults();

    public abstract Asset<Texture2D> GetTexture();

    public virtual TreeTypes CountsAsTreeType => TreeTypes.Custom;

    public virtual int CreateDust() => 7;

    public virtual int TreeLeaf() => -1;

    public virtual bool Shake(int x, int y, ref bool createLeaves) => true;

    public virtual bool CanDropAcorn() => true;

    public virtual int SaplingGrowthType(ref int style)
    {
        style = 0;
        return 20;
    }

    public abstract int DropWood();

    public virtual void SetTreeFoliageSettings(int i, int j, Tile tile, int xoffset, ref int treeFrame, int floorY, ref int topTextureFrameWidth, ref int topTextureFrameHeight)
    {
    }

    public abstract Asset<Texture2D> GetTopTextures();

    public abstract Asset<Texture2D> GetBranchTextures();
}
