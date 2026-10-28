using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace BunnyLoader.Interop;

[StructLayout(LayoutKind.Sequential)]
public unsafe struct CallFrame
{
    public fixed long X[16];
    public fixed ulong D[8];
    public IntPtr Indirect;
    public RetKind Ret;
    public int RetSize;
    public fixed ulong Out[4];
}

public enum RetKind
{
    Int = 0,
    F32 = 1,
    F64 = 2,
    S16 = 3,
    H1F = 4, H2F = 5, H3F = 6, H4F = 7,
    H1D = 8, H2D = 9, H3D = 10, H4D = 11,
    Indirect = 12,
}

[StructLayout(LayoutKind.Sequential)] public struct RetS16 { public long A, B; }
[StructLayout(LayoutKind.Sequential)] public struct RetH1F { public float A; }
[StructLayout(LayoutKind.Sequential)] public struct RetH2F { public float A, B; }
[StructLayout(LayoutKind.Sequential)] public struct RetH3F { public float A, B, C; }
[StructLayout(LayoutKind.Sequential)] public struct RetH4F { public float A, B, C, D; }
[StructLayout(LayoutKind.Sequential)] public struct RetH1D { public double A; }
[StructLayout(LayoutKind.Sequential)] public struct RetH2D { public double A, B; }
[StructLayout(LayoutKind.Sequential)] public struct RetH3D { public double A, B, C; }
[StructLayout(LayoutKind.Sequential)] public struct RetH4D { public double A, B, C, D; }

public static unsafe class Abi
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong F32(float value) => BitConverter.SingleToUInt32Bits(value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong F64(double value) => BitConverter.DoubleToUInt64Bits(value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float ToF32(ulong bits) => BitConverter.UInt32BitsToSingle((uint)bits);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double ToF64(ulong bits) => BitConverter.UInt64BitsToDouble(bits);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T FromX<T>(long a, long b = 0) where T : unmanaged
    {
        RetS16 raw;
        raw.A = a;
        raw.B = b;
        return *(T*)&raw;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T FromF<T>(float a, float b = 0, float c = 0, float d = 0) where T : unmanaged
    {
        RetH4F raw;
        raw.A = a;
        raw.B = b;
        raw.C = c;
        raw.D = d;
        return *(T*)&raw;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T FromD<T>(double a, double b = 0, double c = 0, double d = 0) where T : unmanaged
    {
        RetH4D raw;
        raw.A = a;
        raw.B = b;
        raw.C = c;
        raw.D = d;
        return *(T*)&raw;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TRaw As<T, TRaw>(T value) where T : unmanaged where TRaw : unmanaged
    {
        TRaw raw = default;
        *(T*)&raw = value;
        return raw;
    }
}
