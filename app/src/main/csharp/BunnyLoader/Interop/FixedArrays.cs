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

public unsafe partial struct Vector2_DynamicArray_120
{
    [UnscopedRef]
    public ref global::Microsoft.Xna.Framework.Vector2 this[int index]
    {
        get
        {
            if ((uint)index >= (uint)Length || index >= 120) throw new IndexOutOfRangeException();
            return ref Unsafe.Add(ref data0, index);
        }
    }
}

public unsafe partial struct Float_DynamicArray_120
{
    [UnscopedRef]
    public ref float this[int index]
    {
        get
        {
            if ((uint)index >= (uint)Length || index >= 120) throw new IndexOutOfRangeException();
            return ref Unsafe.Add(ref data0, index);
        }
    }
}

public unsafe partial struct Int_DynamicArray_120
{
    [UnscopedRef]
    public ref int this[int index]
    {
        get
        {
            if ((uint)index >= (uint)Length || index >= 120) throw new IndexOutOfRangeException();
            return ref Unsafe.Add(ref data0, index);
        }
    }
}
