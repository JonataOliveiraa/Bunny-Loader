using System;
using System.Collections.Generic;
using System.Linq;
using BunnyLoader.Interop;
using Terraria.GameContent.Generation;
using Terraria.ID;
using Terraria.IO;
using Terraria.WorldBuilding;
using NativePass = BunnyLoader.Interop.WorldGen.GenPass;
using NativeLegacyHooks = BunnyLoader.Interop.WorldGen.On_PassLegacy;

namespace Terraria.ModLoader.Core;

internal static class WorldGenHooks
{
    static readonly object gate = new();

    static readonly Dictionary<IntPtr, WeakReference<GenPass>> custom = new();

    [ThreadStatic]
    static GenerationContext? current;

    sealed class GenerationContext
    {
        internal readonly IntPtr Generator;

        internal readonly double Weight;

        internal GenerationContext(WorldGenerator generator, double weight)
        {
            Generator = generator.Pointer;
            Weight = weight;
        }
    }

    sealed class NativeStage : GenPass
    {
        internal NativeStage(NativePass native) : base(native)
        {
        }

        protected override void ApplyPass(GenerationProgress progress, GameConfiguration configuration)
        {
            Native.Apply(progress, configuration);
        }
    }

    internal static void Install(Type type)
    {
        bool Has(string name) => ItemHooks.Overrides(type, typeof(ModSystem), name);
        if (Has(nameof(ModSystem.PreWorldGen)) || Has(nameof(ModSystem.ModifyWorldGenTasks)) || Has(nameof(ModSystem.PostWorldGen)))
        {
            ItemHooks.Once("WorldGenTasks", () =>
            {
                On_WorldGenerator.GenerateWorld += Generate;
                On_WorldGenerator.RunPass += RunPass;
            });
        }

        if (Has(nameof(ModSystem.ModifyHardmodeTasks)))
            ItemHooks.Once("WorldGenHardmode", () => On_WorldGen.initializeHardMode += Hardmode);
    }

    internal static void Track(GenPass pass)
    {
        lock (gate)
        {
            foreach (IntPtr pointer in custom.Where(pair => !pair.Value.TryGetTarget(out _)).Select(pair => pair.Key).ToArray())
                custom.Remove(pointer);
            custom[pass.Pointer] = new WeakReference<GenPass>(pass);
        }

        ItemHooks.Once("WorldGenPassLegacy", () => NativeLegacyHooks.ApplyPass += ApplyPass);
    }

    static GenPass Wrap(NativePass native)
    {
        lock (gate)
        {
            if (custom.TryGetValue(native.Pointer, out var weak) && weak.TryGetTarget(out GenPass? pass))
                return pass;
        }

        return new NativeStage(native);
    }

    static void ApplyPass(NativeLegacyHooks.orig_ApplyPass orig, BunnyLoader.Interop.WorldGen.PassLegacy native, GenerationProgress progress, GameConfiguration configuration)
    {
        GenPass? pass;
        lock (gate)
        {
            pass = custom.TryGetValue(native.Pointer, out var weak) && weak.TryGetTarget(out GenPass? found) ? found : null;
        }

        if (pass == null)
        {
            orig(native, progress, configuration);
            return;
        }

        try
        {
            pass.Invoke(progress, configuration);
        }
        catch (Exception error)
        {
            ItemHooks.Report($"GenPass.{pass.Name}", error);
        }
    }

    internal static double WeightOf(List<GenPass> tasks)
    {
        double weight = 0;
        foreach (GenPass pass in tasks)
        {
            if (pass == null || !double.IsFinite(pass.Weight) || pass.Weight < 0)
                throw new ArgumentException("World generation tasks must contain valid passes with finite non-negative weights");
            weight += pass.Weight;
        }

        if (!double.IsFinite(weight))
            throw new ArgumentException("World generation total weight must be finite");
        return weight;
    }

    static bool Generate(On_WorldGenerator.orig_GenerateWorld orig, WorldGenerator generator)
    {
        foreach (ModSystem system in SystemLoader.With(nameof(ModSystem.PreWorldGen)))
        {
            try
            {
                system.PreWorldGen();
            }
            catch (Exception error)
            {
                ItemHooks.Report(system.FullName + ".PreWorldGen", error);
            }
        }

        var native = new GameList(generator._passes);
        var tasks = new List<GenPass>(native.Count);
        for (int i = 0; i < native.Count; i++)
            tasks.Add(Wrap(new NativePass(native[i])));
        double totalWeight = WeightOf(tasks);
        foreach (ModSystem system in SystemLoader.With(nameof(ModSystem.ModifyWorldGenTasks)))
        {
            GenPass[] previous = tasks.ToArray();
            double previousWeight = totalWeight;
            try
            {
                system.ModifyWorldGenTasks(tasks, ref totalWeight);
                WeightOf(tasks);
                if (!double.IsFinite(totalWeight) || totalWeight < 0)
                    throw new ArgumentException("World generation total weight must be finite and non-negative");
            }
            catch (Exception error)
            {
                ItemHooks.Report(system.FullName + ".ModifyWorldGenTasks", error);
                tasks.Clear();
                tasks.AddRange(previous);
                totalWeight = previousWeight;
            }
        }

        KeepModChests(tasks);
        for (int i = native.Count - 1; i >= 0; i--)
            native.RemoveAt(i);
        foreach (GenPass pass in tasks)
            native.Add(pass.Pointer);
        GenerationContext? outer = current;
        current = new GenerationContext(generator, totalWeight);
        try
        {
            bool completed = orig(generator);
            if (completed)
            {
                foreach (ModSystem system in SystemLoader.With(nameof(ModSystem.PostWorldGen)))
                {
                    try
                    {
                        system.PostWorldGen();
                    }
                    catch (Exception error)
                    {
                        ItemHooks.Report(system.FullName + ".PostWorldGen", error);
                    }
                }
            }

            return completed;
        }
        finally
        {
            current = outer;
            GC.KeepAlive(tasks);
        }
    }

    static GenPassResult RunPass(On_WorldGenerator.orig_RunPass orig, WorldGenerator generator, NativePass pass)
    {
        if (current is GenerationContext context && context.Generator == generator.Pointer)
            generator._progress.TotalWeight = context.Weight;
        return orig(generator, pass);
    }

    static void Hardmode(On_WorldGen.orig_initializeHardMode orig)
    {
        var conversion = new PassLegacy("Hardmode Conversion", (progress, configuration) => orig());
        var tasks = new List<GenPass>
        {
            conversion
        };
        foreach (ModSystem system in SystemLoader.With(nameof(ModSystem.ModifyHardmodeTasks)))
        {
            GenPass[] previous = tasks.ToArray();
            try
            {
                system.ModifyHardmodeTasks(tasks);
                WeightOf(tasks);
            }
            catch (Exception error)
            {
                ItemHooks.Report(system.FullName + ".ModifyHardmodeTasks", error);
                tasks.Clear();
                tasks.AddRange(previous);
            }
        }

        GenerationProgress progress = new();
        using var progressHandle = new GameHandle(progress.Pointer);
        progress.TotalWeight = WeightOf(tasks);
        foreach (GenPass pass in tasks)
        {
            if (!pass.Enabled)
                continue;
            progress.Start(pass.Weight);
            pass.Apply(progress, default);
            progress.End();
        }

        GC.KeepAlive(tasks);
    }

    readonly record struct ChestTile(int X, int Y, ushort Type);

    static void KeepModChests(List<GenPass> tasks)
    {
        int index = tasks.FindIndex(pass => pass.Name == "Tile Cleanup");
        if (index < 0 || tasks[index] is not NativeStage cleanup)
            return;
        var replacement = new PassLegacy(cleanup.Name, (progress, configuration) =>
        {
            var chests = new List<ChestTile>();
            for (int i = 0; i < Main.chest.Length; i++)
            {
                Chest chest = Main.chest[i];
                if (chest.IsNull)
                    continue;
                Tile tile = Main.tile[chest.x, chest.y];
                if (tile.HasTile && tile.type >= TileID.Count && TileID.Sets.BasicChest[tile.type])
                    chests.Add(new ChestTile(chest.x, chest.y, tile.type));
            }

            try
            {
                cleanup.Apply(progress, configuration);
            }
            finally
            {
                foreach (ChestTile chest in chests)
                {
                    for (int x = chest.X; x < chest.X + 2; x++)
                    {
                        for (int y = chest.Y; y < chest.Y + 2; y++)
                        {
                            Tile tile = Main.tile[x, y];
                            if (tile.HasTile && TileID.Sets.BasicChest[tile.type])
                                tile.type = chest.Type;
                        }
                    }
                }
            }
        }, cleanup.Weight);
        if (!cleanup.Enabled)
            replacement.Disable();
        tasks[index] = replacement;
    }
}
