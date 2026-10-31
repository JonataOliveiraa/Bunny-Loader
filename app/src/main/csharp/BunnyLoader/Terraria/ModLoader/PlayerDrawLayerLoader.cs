using System;
using System.Collections.Generic;
using System.Linq;
using Terraria.DataStructures;
using Terraria.ModLoader.Core;
using static Terraria.ModLoader.PlayerDrawLayer;

namespace Terraria.ModLoader;

public static class PlayerDrawLayerLoader
{
    static readonly List<PlayerDrawLayer> layers = new(PlayerDrawLayers.VanillaLayers);
    static PlayerDrawLayer[]? drawOrder;

    public static IReadOnlyList<PlayerDrawLayer> Layers => layers;

    public static IReadOnlyList<PlayerDrawLayer> DrawOrder => drawOrder ??= Order();

    internal static bool HasModded => layers.Count > PlayerDrawLayers.VanillaLayers.Count;

    internal static void Add(PlayerDrawLayer layer)
    {
        lock (layers)
        {
            layers.Add(layer);
            drawOrder = null;
        }
        PlayerDrawHooks.InstallLayers();
    }

    internal static void ResizeArrays() => drawOrder = Order();

    static PlayerDrawLayer[] Order()
    {
        foreach (PlayerDrawLayer layer in layers) layer.ClearChildren();
        var positions = layers.ToDictionary(l => l, l => l.GetDefaultPosition());
        PlayerLoader.ModifyDrawLayerOrdering(positions);
        var betweens = new Dictionary<PlayerDrawLayer, Between>();
        foreach ((PlayerDrawLayer layer, Position position) in positions.ToArray())
        {
            switch (position)
            {
                case Between between:
                    betweens.Add(layer, between);
                    break;
                case BeforeParent before:
                    before.Parent.AddChildBefore(layer);
                    break;
                case AfterParent after:
                    after.Parent.AddChildAfter(layer);
                    break;
                case Multiple multiple:
                    int slot = 0;
                    foreach ((Between between, Multiple.Condition condition) in multiple.Positions)
                        betweens.Add(new PlayerDrawLayerSlot(layer, condition, slot++), between);
                    break;
                default:
                    throw new ArgumentException($"PlayerDrawLayer {layer} has unknown Position type {position}");
            }
        }
        foreach ((PlayerDrawLayer layer, Between between) in betweens)
        {
            if (between.Layer1 is { } after && !betweens.ContainsKey(after))
                throw new ArgumentException($"{layer.FullName} cannot be positioned after {after.FullName} because {after.FullName} does not have a fixed position. Consider using AfterParent or referring to a different layer (or null)");
            if (between.Layer2 is { } before && !betweens.ContainsKey(before))
                throw new ArgumentException($"{layer.FullName} cannot be positioned before {before.FullName} because {before.FullName} does not have a fixed position. Consider using BeforeParent or referring to a different layer (or null)");
        }
        return Sort(betweens.Keys.ToList(), betweens);
    }

    static PlayerDrawLayer[] Sort(List<PlayerDrawLayer> nodes, Dictionary<PlayerDrawLayer, Between> betweens)
    {
        var after = nodes.ToDictionary(n => n, _ => new HashSet<PlayerDrawLayer>());
        foreach (PlayerDrawLayer node in nodes)
        {
            Between between = betweens[node];
            if (between.Layer1 is { } first) after[node].Add(first);
            if (between.Layer2 is { } second) after[second].Add(node);
        }
        var result = new List<PlayerDrawLayer>(nodes.Count);
        var placed = new HashSet<PlayerDrawLayer>();
        var left = new List<PlayerDrawLayer>(nodes);
        while (left.Count > 0)
        {
            int index = left.FindIndex(n => after[n].All(placed.Contains));
            if (index < 0) throw new ArgumentException($"PlayerDrawLayer ordering has a cycle: {string.Join(", ", left)}");
            PlayerDrawLayer next = left[index];
            left.RemoveAt(index);
            placed.Add(next);
            result.Add(next);
        }
        return result.ToArray();
    }

    public static PlayerDrawLayer[] GetDrawLayers(PlayerDrawSet drawInfo)
    {
        PlayerDrawLayer[] order = drawOrder ??= Order();
        foreach (PlayerDrawLayer layer in order) layer.ResetVisibility(drawInfo);
        PlayerLoader.HideDrawLayers(drawInfo);
        return order;
    }
}
