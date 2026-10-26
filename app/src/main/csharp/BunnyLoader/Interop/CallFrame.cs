using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace BunnyLoader.Interop;

// Os registradores de uma chamada ao jogo (CallFrame em
// app/src/main/cpp/script/dotnet/DotnetCall.h). O gerador das proxies ja
// sabe, para cada metodo, em que casa vai cada argumento (AAPCS64): o codigo
// gerado so escreve ali e chama GameInterop.Call.
[StructLayout(LayoutKind.Sequential)]
public unsafe struct CallFrame
{
    // x0-x7 e as 8 primeiras casas da pilha.
    public fixed long X[16];
    // d0-d7, bits crus: um float vive nos 32 de baixo.
    public fixed ulong D[8];
    // Retorno > 16 bytes: o destino (o x8 do chamador).
    public IntPtr Indirect;
    public RetKind Ret;
    public int RetSize;
    // x0/x1, ou s0-s3/d0-d3 empacotados como o struct.
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

// Os formatos crus de retorno de um hook ([UnmanagedCallersOnly]). O Mono
// devolve cada um onde o AAPCS64 manda: S16 em x0/x1, HxF em s0-s3, HxD em
// d0-d3. O codigo gerado reinterpreta os bytes como o struct do jogo.
[StructLayout(LayoutKind.Sequential)] public struct RetS16 { public long A, B; }
[StructLayout(LayoutKind.Sequential)] public struct RetH1F { public float A; }
[StructLayout(LayoutKind.Sequential)] public struct RetH2F { public float A, B; }
[StructLayout(LayoutKind.Sequential)] public struct RetH3F { public float A, B, C; }
[StructLayout(LayoutKind.Sequential)] public struct RetH4F { public float A, B, C, D; }
[StructLayout(LayoutKind.Sequential)] public struct RetH1D { public double A; }
[StructLayout(LayoutKind.Sequential)] public struct RetH2D { public double A, B; }
[StructLayout(LayoutKind.Sequential)] public struct RetH3D { public double A, B, C; }
[StructLayout(LayoutKind.Sequential)] public struct RetH4D { public double A, B, C, D; }

// Conversoes entre valores e registradores, usadas pelo codigo gerado.
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

    // Um struct que chegou em 1 ou 2 registradores x.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T FromX<T>(long a, long b = 0) where T : unmanaged
    {
        RetS16 raw;
        raw.A = a;
        raw.B = b;
        return *(T*)&raw;
    }

    // Um HFA de float que chegou em s0-s3.
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

    // Um HFA de double que chegou em d0-d3.
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

    // O inverso, para devolver um struct pequeno de um hook.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TRaw As<T, TRaw>(T value) where T : unmanaged where TRaw : unmanaged
    {
        TRaw raw = default;
        *(T*)&raw = value;
        return raw;
    }
}
