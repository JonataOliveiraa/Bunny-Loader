using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BunnyLoader.Interop;
using ReLogic.Reflection;

namespace Terraria.ID;

internal static class ConstantSearch
{
    static readonly List<GameHandle> handles = new();

    internal static unsafe IdDictionary Build(Type owner, Type constant)
    {
        var fields = owner.GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.IsLiteral && field.FieldType == constant).ToArray();
        int Value(FieldInfo field) => Convert.ToInt32(field.GetRawConstantValue());
        IdDictionary search = IdDictionary.New(fields.Max(Value) + 1);
        handles.Add(new GameHandle(search.Pointer));
        var prototype = NPCID.Search._idToName;
        if (prototype.IsNull) throw new InvalidOperationException($"{owner.Name}: the reverse index type is unavailable");
        nint indexClass = *(nint*)prototype.Pointer;
        nint index = BunnyLoader.Host.Native()->NewObject(indexClass);
        if (index == 0) throw new OutOfMemoryException($"{owner.Name}: the reverse index could not be allocated");
        using (var indexHandle = new GameHandle(index))
        {
            byte* signature = BunnyLoader.Host.Utf8("void .ctor()");
            nint method;
            try { method = BunnyLoader.Host.Native()->FindMethodBySignature(indexClass, signature); }
            finally { BunnyLoader.Host.FreeUtf8(signature); }
            if (method == 0) throw new MissingMethodException($"{owner.Name}: the reverse index has no constructor");
            var call = GameCall.Returning(RetKind.Int);
            call.Arg(index);
            call.Arg(method);
            call.Invoke(BunnyLoader.Host.Native()->MethodPointer(method));
            search._idToName = new GameObject(index);
        }
        foreach (FieldInfo field in fields) search.Add(field.Name, (short)Value(field));
        return search;
    }
}

public readonly partial struct ItemUseStyleID
{
    public static readonly IdDictionary Search = ConstantSearch.Build(typeof(ItemUseStyleID), typeof(int));
}

public readonly partial struct ItemHoldStyleID
{
    public static readonly IdDictionary Search = ConstantSearch.Build(typeof(ItemHoldStyleID), typeof(int));
}

public readonly partial struct PrefixID
{
    public static readonly IdDictionary Search = ConstantSearch.Build(typeof(PrefixID), typeof(int));
}
