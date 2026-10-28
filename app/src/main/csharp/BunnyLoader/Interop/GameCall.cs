using System;

namespace BunnyLoader.Interop;

internal unsafe ref struct GameCall
{
    CallFrame frame;
    int x, d;

    public static GameCall Returning(RetKind kind)
    {
        var call = new GameCall();
        call.frame.Ret = kind;
        return call;
    }

    public void Arg(IntPtr value) => Arg((long)value);

    public void Arg(long value)
    {
        fixed (long* registers = frame.X) registers[x++] = value;
    }

    public void Arg(float value)
    {
        fixed (ulong* registers = frame.D) registers[d++] = Abi.F32(value);
    }

    public void Arg<T>(T value) where T : unmanaged
    {
        if (sizeof(T) > 16) throw new NotSupportedException("GameCall: structs over 16 bytes go by reference");
        fixed (long* registers = frame.X) *(T*)(registers + x) = value;
        x += (sizeof(T) + 7) / 8;
    }

    public long Invoke(IntPtr fn)
    {
        fixed (CallFrame* f = &frame)
        {
            GameInterop.Call(fn, f);
            return (long)f->Out[0];
        }
    }
}
