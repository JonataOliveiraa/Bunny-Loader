using System;
using System.Collections.Generic;
using System.Linq;
using Terraria.DataStructures;
using Terraria.ModLoader.Core;
using static Terraria.ModLoader.PlayerDrawLayer;

namespace Terraria.ModLoader;

public static class PlayerDrawLayerLoader
{
    sealed class Ordering
    {
        public readonly PlayerDrawLayer[] Order;
        public readonly PlayerDrawLayer[] Modded;
        public readonly bool Vanilla;

        public Ordering(PlayerDrawLayer[] order, PlayerDrawLayer[] modded, bool vanilla)
        {
            Order = order;
            Modded = modded;
            Vanilla = vanilla;
        }
    }

    static readonly List<PlayerDrawLayer> layers = new(PlayerDrawLayers.VanillaLayers);
    static Ordering? ordering;

    public static IReadOnlyList<PlayerDrawLayer> Layers => layers;

    public static IReadOnlyList<PlayerDrawLayer> DrawOrder => Current.Order;

    internal static bool HasModded => layers.Count > PlayerDrawLayers.VanillaLayers.Count;

    static Ordering Current => ordering ??= Build();

    internal static void Add(PlayerDrawLayer layer)
    {
        lock (layers)
        {
            layers.Add(layer);
            ordering = null;
        }
        PlayerDrawHooks.InstallLayers();
    }

    internal static void ResizeArrays() => ordering = Build();

    static Ordering Build()
    {
        PlayerDrawLayer[] order = Order();
        PlayerDrawLayer[] modded = layers.Skip(PlayerDrawLayers.VanillaLayers.Count).ToArray();
        return new Ordering(order, modded, MatchesVanilla(order));
    }

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

    static bool MatchesVanilla(PlayerDrawLayer[] order)
    {
        if (PlayerLoader.OrdersDrawLayers) return false;
        var expected = new List<(PlayerDrawLayer Layer, int Slot)>();
        foreach (PlayerDrawLayer layer in PlayerDrawLayers.FixedVanillaLayers)
        {
            expected.Add((layer, -1));
            if (layer == PlayerDrawLayers.BalloonAcc) expected.Add((PlayerDrawLayers.HeldItem, 0));
            else if (layer == PlayerDrawLayers.FaceAcc) expected.Add((PlayerDrawLayers.FrontAccFront, 0));
            else if (layer == PlayerDrawLayers.SolarShield) expected.Add((PlayerDrawLayers.HeldItem, 1));
            else if (layer == PlayerDrawLayers.BladedGlove)
            {
                expected.Add((PlayerDrawLayers.FrontAccFront, 1));
                expected.Add((PlayerDrawLayers.HeldItem, 2));
            }
        }
        int next = 0;
        foreach (PlayerDrawLayer layer in order)
        {
            if (!layer.IsVanilla) continue;
            (PlayerDrawLayer Layer, int Slot) entry = layer is PlayerDrawLayerSlot slot ? (slot.Layer, slot.Index) : (layer, -1);
            if (next >= expected.Count || expected[next].Layer != entry.Layer || expected[next].Slot != entry.Slot) return false;
            next++;
        }
        return next == expected.Count;
    }

    public static PlayerDrawLayer[] GetDrawLayers(PlayerDrawSet drawInfo) => GetDrawLayers(drawInfo, out _);

    internal static PlayerDrawLayer[] GetDrawLayers(PlayerDrawSet drawInfo, out bool vanilla)
    {
        Ordering current = Current;
        PlayerDrawLayer[] order = current.Order;
        PlayerDrawLayer.HiddenVanilla = 0;
        foreach (PlayerDrawLayer layer in order) layer.ResetVisibility(drawInfo);
        PlayerLoader.HideDrawLayers(drawInfo);
        vanilla = current.Vanilla && PlayerDrawLayer.HiddenVanilla == 0 && !AnyVisible(current.Modded);
        return order;
    }

    static bool AnyVisible(PlayerDrawLayer[] modded)
    {
        foreach (PlayerDrawLayer layer in modded)
        {
            if (layer.Visible) return true;
        }
        return false;
    }
}
