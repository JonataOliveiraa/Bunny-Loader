using System;
using BunnyLoader.Interop;

namespace BunnyLoader;

public sealed unsafe class Hook
{
    readonly IntPtr* original;

    Hook(IntPtr* original) => this.original = original;

    public IntPtr Original => *original;

    public IntPtr* OriginalSlot => original;

    public static Hook Install(IntPtr method, IntPtr replacement)
    {
        IntPtr* slot = Host.Native()->InstallHook(method, replacement);
        if (slot == null) throw new InvalidOperationException("hook install failed");
        return new Hook(slot);
    }

    public static Hook Install(string ns, string cls, string signature, IntPtr replacement)
    {
        var gameClass = new GameClass(ns, cls);
        MethodRef method = default;
        GameInterop.Method(ref method, gameClass, signature, LayoutUnchecked(signature));
        return Install(method.Info, replacement);
    }

    static int[] LayoutUnchecked(string signature)
    {
        int open = signature.IndexOf('('), close = signature.LastIndexOf(')');
        string inner = signature.Substring(open + 1, close - open - 1).Trim();
        int count = inner.Length == 0 ? 0 : 1;
        int depth = 0;
        foreach (char c in inner)
        {
            if (c == '<') depth++;
            else if (c == '>') depth--;
            else if (c == ',' && depth == 0) count++;
        }
        int[] layout = new int[count + 1];
        Array.Fill(layout, -1);
        return layout;
    }
}
