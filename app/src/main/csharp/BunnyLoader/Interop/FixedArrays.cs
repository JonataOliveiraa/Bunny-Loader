using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

public unsafe partial struct Float_FixedArray_3
{
    public const int Length = 3;

    [UnscopedRef]
    public ref float this[int index]
    {
        get
        {
            if ((uint)index >= Length) throw new IndexOutOfRangeException();
            return ref Unsafe.Add(ref val0, index);
        }
    }
}
