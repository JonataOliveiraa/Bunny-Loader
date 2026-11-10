using System;
using System.Collections.Generic;
using BunnyLoader.Interop;
using Microsoft.Xna.Framework;
using Terraria.UI;
using NativeLayer = BunnyLoader.Interop.UI.GameInterfaceLayer;

namespace Terraria.ModLoader.Core;

internal static class InterfaceLayerHooks
{
    const string Prefix = "bl:";

    sealed class Native
    {
        public required GameHandle Handle;
        public required NativeLayer Layer;
        public InterfaceScaleType Scale;
    }

    static readonly List<GameInterfaceLayer> vanilla = new();
    static readonly List<IntPtr> applied = new();
    static readonly List<IntPtr> final = new();
    static readonly Dictionary<string, Native> natives = new();
    static readonly Dictionary<IntPtr, GameInterfaceLayer> byNative = new();
    static readonly Dictionary<string, int> seen = new();
    static IntPtr capturedList;
    static int counter;

    internal static void Install() => ItemHooks.Once("InterfaceLayers", () =>
    {
        On_Main.DrawInterface += DrawInterface;
        BunnyLoader.Interop.UI.On_GameInterfaceLayer.DrawSelf += DrawSelf;
    });

    static void DrawInterface(On_Main.orig_DrawInterface orig, Main self, GameTime gameTime)
    {
        try { Apply(self); }
        catch (Exception e) { ItemHooks.Report("ModSystem.ModifyInterfaceLayers", e); }
        orig(self, gameTime);
    }

    static bool DrawSelf(BunnyLoader.Interop.UI.On_GameInterfaceLayer.orig_DrawSelf orig, NativeLayer self)
    {
        if (!byNative.TryGetValue(self.Pointer, out GameInterfaceLayer? layer) || layer.Native != IntPtr.Zero) return orig(self);
        try { return layer.DrawFromGame(); }
        catch (Exception e)
        {
            ItemHooks.Report($"interface layer {layer.Name}", e);
            return true;
        }
    }

    static void Capture(GameList list, IntPtr pointer)
    {
        vanilla.Clear();
        applied.Clear();
        for (int i = 0; i < list.Count; i++)
        {
            var native = new NativeLayer(list[i]);
            string name = native.Name ?? "";
            if (name.StartsWith(Prefix)) continue;
            var layer = new GameInterfaceLayer(name, native.ScaleType) { Native = native.Pointer };
            vanilla.Add(layer);
            applied.Add(native.Pointer);
        }
        capturedList = pointer;
    }

    static void Apply(Main main)
    {
        if (main._needToSetupDrawInterfaceLayers)
        {
            capturedList = IntPtr.Zero;
            return;
        }
        GameObject listObject = main._gameInterfaceLayers;
        if (listObject.IsNull) return;
        var list = new GameList(listObject);
        if (list.Count == 0) return;
        if (capturedList != listObject.Pointer || list.Count != applied.Count) Capture(list, listObject.Pointer);

        var layers = new List<GameInterfaceLayer>(vanilla.Count + 8);
        foreach (GameInterfaceLayer layer in vanilla)
        {
            layer.Active = true;
            layers.Add(layer);
        }
        SystemLoader.ModifyInterfaceLayers(layers);
        SubworldVoteUI.ModifyInterfaceLayers(layers);
        SubworldTransition.ModifyInterfaceLayers(layers);

        seen.Clear();
        final.Clear();
        foreach (GameInterfaceLayer layer in layers)
        {
            if (layer == null || !layer.Active) continue;
            final.Add(layer.Native != IntPtr.Zero ? layer.Native : NativeOf(layer));
        }
        bool same = final.Count == applied.Count;
        for (int i = 0; same && i < final.Count; i++) same = final[i] == applied[i];
        if (same) return;
        for (int i = list.Count - 1; i >= 0; i--) list.RemoveAt(i);
        foreach (IntPtr native in final) list.Add(native);
        applied.Clear();
        applied.AddRange(final);
    }

    static IntPtr NativeOf(GameInterfaceLayer layer)
    {
        string name = layer.Name ?? "";
        int repeat = seen.GetValueOrDefault(name);
        seen[name] = repeat + 1;
        string key = repeat == 0 ? name : $"{name}#{repeat}";
        if (!natives.TryGetValue(key, out Native? native))
        {
            NativeLayer created = NativeLayer.New($"{Prefix}{counter++}:{name}", layer.ScaleType);
            native = new Native { Handle = new GameHandle(created.Pointer), Layer = created, Scale = layer.ScaleType };
            natives[key] = native;
        }
        if (native.Scale != layer.ScaleType)
        {
            native.Layer.ScaleType = layer.ScaleType;
            native.Scale = layer.ScaleType;
        }
        byNative[native.Layer.Pointer] = layer;
        return native.Layer.Pointer;
    }
}
