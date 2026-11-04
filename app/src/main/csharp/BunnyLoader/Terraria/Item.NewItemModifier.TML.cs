using System;
using BunnyLoader.Interop;

namespace Terraria;

public readonly partial struct Item
{
    public readonly partial struct NewItemModifier
    {
        static MethodRef invokeMethod;
        static readonly int[] InvokeLayout = { -1, -1 };
        static VirtualCache? invokeCache;

        public void Invoke(WorldItem item)
        {
            if (IsNull) throw new NullReferenceException("Item.NewItemModifier.Invoke: game delegate is null.");
            ref readonly MethodRef method = ref GameInterop.Method(ref invokeMethod, Class, "void Invoke(WorldItem item)", InvokeLayout);
            var target = GameInterop.Virtual(ref invokeCache, in method, Pointer);
            var call = GameCall.Returning(RetKind.Int);
            call.Arg(Pointer);
            call.Arg(item.Pointer);
            call.Arg(target.Info);
            call.Invoke(target.Fn);
        }
    }
}
