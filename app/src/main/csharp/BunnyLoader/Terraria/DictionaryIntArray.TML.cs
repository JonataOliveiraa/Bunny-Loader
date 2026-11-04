using BunnyLoader.Interop;

public unsafe partial struct DictionaryIntArray
{
    static MethodRef getItem, setItem;
    static readonly int[] GetLayout = { -1, -1 };
    static readonly int[] SetLayout = { -1, -1, -1 };

    public int this[int index]
    {
        get
        {
            ref readonly MethodRef method = ref GameInterop.Method(ref getItem, Class, "int get_Item(int index)", GetLayout);
            fixed (DictionaryIntArray* self = &this)
            {
                var call = GameCall.Returning(RetKind.Int);
                call.Arg((nint)self);
                call.Arg(index);
                call.Arg(method.Info);
                return (int)call.Invoke(method.Fn);
            }
        }
        set
        {
            ref readonly MethodRef method = ref GameInterop.Method(ref setItem, Class, "void set_Item(int index, int value)", SetLayout);
            fixed (DictionaryIntArray* self = &this)
            {
                var call = GameCall.Returning(RetKind.Int);
                call.Arg((nint)self);
                call.Arg(index);
                call.Arg(value);
                call.Arg(method.Info);
                call.Invoke(method.Fn);
            }
        }
    }
}
