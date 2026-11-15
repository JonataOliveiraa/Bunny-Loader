using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using BunnyLoader.Interop;

namespace BunnyLoader.Hooks;

internal static unsafe class HookGates
{
    internal const int Invert = 1;
    const int Always = 2;
    internal const int Value = 4;
    internal const int AbsoluteArg = 8;
    const int TableBits = 1 << 16;

    sealed class Gate
    {
        public Func<GateSpec> Spec = null!;
        public Func<int>? Count;
        public int Bits;
        public byte* Bitmap;
        public int Id = -1;
        public int Tokens;
        public bool External;
        public bool NeedsAll;
        public bool Ready;
        public readonly List<Func<int, bool>> Predicates = new();
    }

    static readonly object Lock = new();
    static readonly Dictionary<Type, Gate> Gates = new();
    static readonly Assembly Self = typeof(HookGates).Assembly;
    static bool contentReady;

    [ThreadStatic] internal static Type? Installing;

    internal static GateSpec Field(int arg, GameClass type, string field, int size = 4)
    {
        int slot = -1;
        return new GateSpec { Arg = arg, PointerOffset = -1, FieldOffset = GameInterop.Offset(ref slot, type, field), FieldSize = size };
    }

    internal static GateSpec Static(GameClass type, string field, int size = 4)
    {
        StaticRef slot = default;
        return new GateSpec { Arg = AbsoluteArg, PointerOffset = -1, FieldOffset = 0, FieldSize = size, Address = GameInterop.StaticAddress(ref slot, type, field) };
    }

    internal static GateSpec Pointer(int arg, int flags = 0) =>
        new() { Arg = arg, PointerOffset = -1, FieldOffset = -1, FieldSize = 4, Flags = flags };

    internal static void Define<THook>(Func<GateSpec> spec) where THook : Delegate => Define(typeof(THook), spec, null);

    internal static void Table<THook>(Func<GateSpec> spec, Func<int> count) where THook : Delegate => Define(typeof(THook), spec, count);

    static void Define(Type hook, Func<GateSpec> spec, Func<int>? count)
    {
        lock (Lock)
        {
            if (Gates.ContainsKey(hook)) return;
            Gates[hook] = new Gate { Spec = spec, Count = count, Bits = count != null ? TableBits : 0 };
        }
    }

    internal static void Want<THook>(Func<int, bool>? predicate = null) where THook : Delegate
    {
        Gate? gate = Find(typeof(THook));
        if (gate == null) return;
        lock (gate)
        {
            gate.Tokens++;
            if (gate.Bits == 0) return;
            if (predicate == null)
            {
                gate.NeedsAll = true;
                Apply(gate);
                return;
            }
            gate.Predicates.Add(predicate);
            if (gate.Ready && gate.Bitmap != null)
            {
                SetAlways(gate, true);
                Mark(gate, predicate);
                Apply(gate);
            }
        }
    }

    internal static void Subscribed(Type hookType, Delegate hook)
    {
        Gate? gate = Find(hookType);
        if (gate == null) return;
        lock (gate)
        {
            if (!FromLoader(hook)) gate.External = true;
            else if (gate.Tokens > 0) gate.Tokens--;
            else gate.NeedsAll = true;
            Apply(gate);
        }
    }

    internal static IntPtr* Install(Type hookType, IntPtr method, IntPtr replacement)
    {
        Gate? gate = Find(hookType);
        if (gate == null) return Host.Native()->InstallHook(method, replacement);
        lock (gate)
        {
            GateSpec spec;
            try
            {
                spec = gate.Spec();
            }
            catch (Exception e)
            {
                Log.Warn($"hook gate for {hookType.FullName} not set: {e.Message}");
                return Host.Native()->InstallHook(method, replacement);
            }
            if (gate.Bits > 0)
            {
                int bytes = gate.Bits / 8;
                gate.Bitmap = (byte*)NativeMemory.Alloc((nuint)bytes);
                new Span<byte>(gate.Bitmap, bytes).Fill(0xFF);
                spec.Bitmap = (IntPtr)gate.Bitmap;
                spec.BitmapBits = gate.Bits;
            }
            spec.Flags |= Always;
            int id;
            IntPtr* slot = Host.Native()->InstallGateHook(method, replacement, &spec, &id);
            if (slot == null) return null;
            gate.Id = id;
            if (gate.Ready) Fill(gate);
            Apply(gate);
            return slot;
        }
    }

    internal static void ContentReady()
    {
        List<Gate> all;
        lock (Lock)
        {
            contentReady = true;
            all = new List<Gate>(Gates.Values);
        }
        foreach (Gate gate in all)
        {
            lock (gate)
            {
                gate.Ready = true;
                Fill(gate);
                Apply(gate);
            }
        }
    }

    static Gate? Find(Type hookType)
    {
        lock (Lock)
        {
            if (!Gates.TryGetValue(hookType, out Gate? gate)) return null;
            if (contentReady) gate.Ready = true;
            return gate;
        }
    }

    static bool FromLoader(Delegate hook)
    {
        foreach (Delegate single in hook.GetInvocationList())
        {
            if (single.Method.Module.Assembly != Self) return false;
        }
        return true;
    }

    static void Fill(Gate gate)
    {
        if (gate.Bitmap == null || gate.Count == null) return;
        int count;
        try
        {
            count = Math.Clamp(gate.Count(), 0, gate.Bits);
        }
        catch (Exception e)
        {
            Log.Warn($"hook gate: type count failed: {e.Message}");
            gate.NeedsAll = true;
            return;
        }
        new Span<byte>(gate.Bitmap, gate.Bits / 8).Fill(0xFF);
        new Span<byte>(gate.Bitmap, count / 8).Clear();
        for (int value = count / 8 * 8; value < count; value++) gate.Bitmap[value >> 3] &= (byte)~(1 << (value & 7));
        foreach (Func<int, bool> predicate in gate.Predicates) Mark(gate, predicate, count);
    }

    static void Mark(Gate gate, Func<int, bool> predicate, int count = -1)
    {
        if (count < 0)
        {
            try { count = Math.Clamp(gate.Count!(), 0, gate.Bits); }
            catch { count = 0; }
        }
        for (int value = 0; value < count; value++)
        {
            bool wanted;
            try
            {
                wanted = predicate(value);
            }
            catch (Exception e)
            {
                Log.Warn($"hook gate: predicate failed for {value}: {e.Message}");
                gate.NeedsAll = true;
                return;
            }
            if (wanted) gate.Bitmap[value >> 3] |= (byte)(1 << (value & 7));
        }
    }

    static void Apply(Gate gate)
    {
        if (gate.Id < 0) return;
        SetAlways(gate, !gate.Ready || gate.External || gate.NeedsAll);
    }

    static void SetAlways(Gate gate, bool always)
    {
        if (gate.Id >= 0) Host.Native()->SetGateAlways(gate.Id, always ? 1 : 0);
    }
}
