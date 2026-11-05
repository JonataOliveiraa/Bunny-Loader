using System;
using System.Collections.Generic;
using BunnyLoader.Interop;
using Terraria.Map;
using Terraria.ModLoader.Core;

namespace Terraria.ModLoader;

public static class MapLayerLoader
{
    static readonly List<ModMapLayer> Layers = new();
    static List<ModMapLayer>[]? slots;
    static int sortedFor = -1;

    public static int MapLayerCount => Layers.Count;

    internal static bool Any => Layers.Count > 0;

    internal static void Add(ModMapLayer layer)
    {
        Layers.Add(layer);
        slots = null;
        SystemDrawHooks.WantMapIcons();
    }

    internal static void Draw(GameList list, ref MapOverlayDrawContext context, ref GameString text, Func<IMapLayer, bool> hidden)
    {
        if (slots == null || sortedFor != list.Count) Sort(list);
        for (int i = 0; i <= list.Count; i++)
        {
            foreach (ModMapLayer layer in slots![i]) DrawModded(layer, ref context, ref text);
            if (i == list.Count) break;
            IMapLayer vanilla = new(list[i]);
            if (!hidden(vanilla)) vanilla.Draw(ref context, ref text);
        }
    }

    static void DrawModded(ModMapLayer layer, ref MapOverlayDrawContext context, ref GameString text)
    {
        if (!layer.Visible) return;
        string before = text.ToString() ?? "";
        string after = before;
        try { layer.Draw(ref context, ref after); }
        catch (Exception e) { ItemHooks.Report($"{layer.FullName}.Draw", e); }
        if (after != before) text = after;
    }

    static void Sort(GameList list)
    {
        sortedFor = list.Count;
        slots = new List<ModMapLayer>[list.Count + 1];
        for (int i = 0; i < slots.Length; i++) slots[i] = new();
        foreach (ModMapLayer layer in Layers)
        {
            ModMapLayer.Position position = layer.GetDefaultPosition();
            int index = -1;
            for (int i = 0; i < list.Count && index < 0; i++) if (list[i] == position.Vanilla.Pointer) index = i;
            if (index < 0) throw new ArgumentException($"ModMapLayer {layer.FullName} did not refer to a vanilla map layer in GetDefaultPosition()");
            slots[position is ModMapLayer.After ? index + 1 : index].Add(layer);
        }
        for (int i = 0; i < slots.Length; i++) slots[i] = Ordered(slots[i]);
    }

    static List<ModMapLayer> Ordered(List<ModMapLayer> layers)
    {
        var after = new Dictionary<ModMapLayer, List<ModMapLayer>>();
        foreach (ModMapLayer layer in layers) after[layer] = new();
        foreach (ModMapLayer layer in layers)
        {
            foreach (ModMapLayer.Position position in layer.GetModdedConstraints() ?? Array.Empty<ModMapLayer.Position>())
            {
                if (position.Modded is not ModMapLayer other || !after.ContainsKey(other)) continue;
                if (position is ModMapLayer.After) after[layer].Add(other);
                else after[other].Add(layer);
            }
        }
        var sorted = new List<ModMapLayer>();
        var seen = new HashSet<ModMapLayer>();
        void Visit(ModMapLayer layer)
        {
            if (!seen.Add(layer)) return;
            foreach (ModMapLayer previous in after[layer]) Visit(previous);
            sorted.Add(layer);
        }
        foreach (ModMapLayer layer in layers) Visit(layer);
        return sorted;
    }
}
