using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using BunnyLoader.Hooks;
using BunnyLoader.Interop;

namespace Terraria.ModLoader.Core;

internal static unsafe class NPCGates
{
    internal const int TypeBits = 1 << 16;

    static readonly object Lock = new();
    static readonly List<Action> BorrowSensitive = new();
    static bool[] borrowed = Array.Empty<bool>();

    internal static void Type<THook>(int arg, Func<int, bool> wanted) where THook : Delegate
    {
        HookGates.Table<THook>(() => HookGates.Field(arg, NPC.Class, "type"), () => NPCLoader.NPCCount);
        HookGates.Want<THook>(wanted);
    }

    internal static void Type<THook>(int arg, NPCMethod method) where THook : Delegate => Type<THook>(arg, type => Wants(type, method));

    internal static void Type<THook>(int arg, NPCHook hook) where THook : Delegate => Type<THook>(arg, type => Wants(type, hook));

    internal static void Borrowable<THook>(int arg, NPCMethod method) where THook : Delegate
    {
        Type<THook>(arg, type => Wants(type, method) || IsBorrowed(type) && AnyOwn(method));
        lock (Lock) BorrowSensitive.Add(() => HookGates.Want<THook>());
    }

    internal static byte* Bitmap<THook>(int arg, string field, int bits) where THook : Delegate
    {
        byte* map = (byte*)NativeMemory.AllocZeroed((nuint)((bits + 7) / 8));
        IntPtr address = (IntPtr)map;
        HookGates.Define<THook>(() => new GateSpec
        {
            Arg = arg, PointerOffset = -1, FieldOffset = ItemHooks.FieldOffset(NPC.Class, field), FieldSize = 4, Bitmap = address, BitmapBits = bits,
        });
        HookGates.Want<THook>();
        return map;
    }

    internal static int* Flag<THook>() where THook : Delegate => Flag<THook>((int*)NativeMemory.AllocZeroed((nuint)sizeof(int)));

    internal static int* Flag<THook>(int* flag) where THook : Delegate
    {
        IntPtr address = (IntPtr)flag;
        HookGates.Define<THook>(() => new GateSpec { Arg = HookGates.AbsoluteArg, PointerOffset = -1, FieldOffset = 0, FieldSize = 4, Address = address });
        HookGates.Want<THook>();
        return flag;
    }

    internal static void Mark(byte* map, int bits, int value)
    {
        if (map != null && (uint)value < (uint)bits) map[value >> 3] |= (byte)(1 << (value & 7));
    }

    internal static bool Marked(byte* map, int bits, int value) => map != null && (uint)value < (uint)bits && (map[value >> 3] & (1 << (value & 7))) != 0;

    internal static bool Wants(int type, NPCMethod method)
    {
        foreach (GlobalNPC global in NPCLoader.Globals.Templates)
            if ((global.Hooks & method) != 0) return true;
        return NPCLoader.GetNPC(type) is ModNPC own && (own.Hooks & method) != 0;
    }

    internal static bool Wants(int type, NPCHook hook)
    {
        foreach (GlobalNPC global in NPCLoader.Globals.Templates)
            if ((global.Extra & hook) != 0) return true;
        return NPCLoader.GetNPC(type) is ModNPC own && (own.Extra & hook) != 0;
    }

    static bool AnyOwn(NPCMethod method)
    {
        int count = NPCLoader.NPCCount;
        for (int type = NPCLoader.VanillaCount; type < count; type++)
            if (NPCLoader.GetNPC(type) is ModNPC own && (own.Hooks & method) != 0) return true;
        return false;
    }

    static bool IsBorrowed(int type)
    {
        bool[] known = Volatile.Read(ref borrowed);
        return (uint)type < (uint)known.Length && known[type];
    }

    internal static void Borrow(int type)
    {
        if (type <= 0 || type >= TypeBits || IsBorrowed(type)) return;
        Action[] always;
        lock (Lock)
        {
            if (type >= borrowed.Length)
            {
                var grown = new bool[Math.Max(type + 1, borrowed.Length * 2)];
                Array.Copy(borrowed, grown, borrowed.Length);
                Volatile.Write(ref borrowed, grown);
            }
            if (borrowed[type]) return;
            borrowed[type] = true;
            if (!MenuLoader.Ready) return;
            always = BorrowSensitive.ToArray();
        }
        foreach (Action want in always) want();
    }

    internal sealed class TypeSet
    {
        readonly Func<int, bool> wanted;
        bool[]? types;

        internal TypeSet(Func<int, bool> wanted) => this.wanted = wanted;

        internal bool Contains(int type)
        {
            bool[]? known = Volatile.Read(ref types);
            if (known == null)
            {
                if (!MenuLoader.Ready) return true;
                int count = NPCLoader.NPCCount;
                known = new bool[count];
                for (int value = 0; value < count; value++) known[value] = wanted(value);
                Volatile.Write(ref types, known);
            }
            return (uint)type >= (uint)known.Length || known[type];
        }
    }
}
