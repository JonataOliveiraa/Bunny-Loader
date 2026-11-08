using System.Runtime.CompilerServices;
using BunnyLoader.Interop;

internal static unsafe class PageControllerLayout
{
    static readonly int[] GetterLayout = { -1 };

    internal static nint Instance(ref MethodRef slot, GameClass cls, string type)
    {
        ref readonly var m = ref GameInterop.Method(ref slot, cls, type + " get_Instance()", GetterLayout);
        CallFrame f;
        Unsafe.SkipInit(out f);
        f.X[0] = (long)m.Info;
        f.Ret = RetKind.Int;
        GameInterop.Call(m.Fn, &f);
        return (nint)f.Out[0];
    }

    internal static bool RightPageController(ref MethodRef slot, GameClass cls, nint self)
    {
        if (self == 0) throw new System.NullReferenceException($"{cls.FullName}.RightPageController: game object is null");
        ref readonly var m = ref GameInterop.Method(ref slot, cls, "bool get_RightPageController()", GetterLayout);
        CallFrame f;
        Unsafe.SkipInit(out f);
        f.X[0] = self;
        f.X[1] = (long)m.Info;
        f.Ret = RetKind.Int;
        GameInterop.Call(m.Fn, &f);
        return ((byte)f.Out[0]) != 0;
    }
}

public readonly unsafe partial struct Reforge_Layout
{
    static MethodRef instance, rightPage;

    public static Reforge_Layout Instance => new(PageControllerLayout.Instance(ref instance, Class, "Reforge_Layout"));

    public bool RightPageController => PageControllerLayout.RightPageController(ref rightPage, Class, Pointer);
}

public readonly unsafe partial struct InventorySplitStack_Layout
{
    static MethodRef instance, rightPage;

    public static InventorySplitStack_Layout Instance => new(PageControllerLayout.Instance(ref instance, Class, "InventorySplitStack_Layout"));

    public bool RightPageController => PageControllerLayout.RightPageController(ref rightPage, Class, Pointer);
}

public readonly unsafe partial struct Equipment_Layout
{
    static MethodRef instance, rightPage;

    public static Equipment_Layout Instance => new(PageControllerLayout.Instance(ref instance, Class, "Equipment_Layout"));

    public bool RightPageController => PageControllerLayout.RightPageController(ref rightPage, Class, Pointer);
}
