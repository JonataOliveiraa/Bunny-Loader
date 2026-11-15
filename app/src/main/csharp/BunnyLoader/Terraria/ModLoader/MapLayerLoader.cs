using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using BunnyLoader;
using BunnyLoader.Interop;
using Terraria.Map;
using Terraria.ModLoader.Core;

namespace Terraria.ModLoader;

public static unsafe class MapLayerLoader
{
    const int ArrayData = 0x20;

    static readonly List<ModMapLayer> Layers = new();
    static List<ModMapLayer>[]? slots;
    static int sortedFor = -1;
    static bool atEnds;

    static MethodRef drawMethod;
    static IntPtr[] classes = new IntPtr[8], infos = new IntPtr[8], pointers = new IntPtr[8];
    static int resolved;

    static IntPtr listClass;
    static int sizeOffset, itemsOffset;

    public static int MapLayerCount => Layers.Count;

    internal static bool Any => Layers.Count > 0;

    internal static void Add(ModMapLayer layer)
    {
        Layers.Add(layer);
        slots = null;
        SystemDrawHooks.WantMapIcons();
    }

    internal static int Snapshot(IntPtr list, ref IntPtr[] items)
    {
        if (list == IntPtr.Zero) return 0;
        IntPtr klass = *(IntPtr*)list;
        if (klass != listClass)
        {
            sizeOffset = Offset(klass, "_size");
            itemsOffset = Offset(klass, "_items");
            listClass = klass;
        }
        int count = *(int*)(list + sizeOffset);
        if (count <= 0) return 0;
        IntPtr array = *(IntPtr*)(list + itemsOffset);
        if (items.Length < count) items = new IntPtr[count];
        new ReadOnlySpan<IntPtr>((void*)(array + ArrayData), count).CopyTo(items);
        return count;
    }

    static int Offset(IntPtr klass, string field)
    {
        byte* name = Host.Utf8(field);
        int offset;
        try { offset = Host.Native()->FieldOffset(klass, name); }
        finally { Host.FreeUtf8(name); }
        if (offset < 0) throw new MissingFieldException($"game List: no field {field}");
        return offset;
    }

    internal static bool OnlyAtEnds(IntPtr[] items, int count)
    {
        Prepare(items, count);
        return atEnds;
    }

    internal static void DrawSlot(int index, ref MapOverlayDrawContext context, ref GameString text)
    {
        List<ModMapLayer>[]? sorted = slots;
        if (sorted == null || (uint)index >= (uint)sorted.Length) return;
        List<ModMapLayer> layers = sorted[index];
        for (int i = 0; i < layers.Count; i++) DrawModded(layers[i], ref context, ref text);
    }

    internal static void Draw(IntPtr[] items, int count, ref MapOverlayDrawContext context, ref GameString text, Func<nint, bool> hidden)
    {
        Prepare(items, count);
        for (int i = 0; i <= count; i++)
        {
            DrawSlot(i, ref context, ref text);
            if (i == count) break;
            IntPtr vanilla = items[i];
            if (vanilla != IntPtr.Zero && !hidden(vanilla)) DrawVanilla(vanilla, ref context, ref text);
        }
    }

    static void Prepare(IntPtr[] items, int count)
    {
        if (slots == null || sortedFor != count) Sort(items, count);
    }

    static void DrawVanilla(IntPtr layer, ref MapOverlayDrawContext context, ref GameString text)
    {
        IntPtr klass = *(IntPtr*)layer;
        int slot = 0;
        while (slot < resolved && classes[slot] != klass) slot++;
        if (slot == resolved) slot = Resolve(layer, klass);
        CallFrame frame;
        Unsafe.SkipInit(out frame);
        fixed (MapOverlayDrawContext* contextPointer = &context)
        fixed (GameString* textPointer = &text)
        {
            frame.X[0] = (long)layer;
            frame.X[1] = (long)contextPointer;
            frame.X[2] = (long)textPointer;
            frame.X[3] = (long)infos[slot];
            frame.Ret = RetKind.Int;
            GameInterop.Call(pointers[slot], &frame);
        }
    }

    static int Resolve(IntPtr layer, IntPtr klass)
    {
        ref readonly MethodRef method = ref GameInterop.Method(ref drawMethod, IMapLayer.Class, "void Draw(ref MapOverlayDrawContext context, ref string text)", "1\u0000\u0000");
        IntPtr info = Host.Native()->VirtualMethod(layer, method.Info);
        IntPtr pointer = info == method.Info ? method.Fn : Host.Native()->MethodPointer(info);
        if (info == IntPtr.Zero || pointer == IntPtr.Zero) throw new MissingMethodException($"{GameInterop.ClassNameOf(layer)}: IMapLayer.Draw not found");
        if (resolved == classes.Length)
        {
            Array.Resize(ref classes, resolved * 2);
            Array.Resize(ref infos, resolved * 2);
            Array.Resize(ref pointers, resolved * 2);
        }
        classes[resolved] = klass;
        infos[resolved] = info;
        pointers[resolved] = pointer;
        return resolved++;
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

    static void Sort(IntPtr[] items, int count)
    {
        sortedFor = count;
        atEnds = false;
        var sorted = new List<ModMapLayer>[count + 1];
        for (int i = 0; i < sorted.Length; i++) sorted[i] = new();
        slots = sorted;
        foreach (ModMapLayer layer in Layers)
        {
            ModMapLayer.Position position = layer.GetDefaultPosition();
            int index = -1;
            for (int i = 0; i < count && index < 0; i++) if (items[i] == position.Vanilla.Pointer) index = i;
            if (index < 0) throw new ArgumentException($"ModMapLayer {layer.FullName} did not refer to a vanilla map layer in GetDefaultPosition()");
            sorted[position is ModMapLayer.After ? index + 1 : index].Add(layer);
        }
        bool ends = true;
        for (int i = 0; i < sorted.Length; i++)
        {
            sorted[i] = Ordered(sorted[i]);
            if (i > 0 && i < count && sorted[i].Count > 0) ends = false;
        }
        atEnds = ends;
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
