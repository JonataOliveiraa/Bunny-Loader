using System;
using System.Collections.Generic;
using BunnyLoader.Interop;
using Terraria.DataStructures;

namespace Terraria.ID;

public readonly partial struct BuffID
{
    public readonly partial struct Sets
    {
        public static BuffTextHandlerTable BuffTextHandlers { get; } = new();

        public sealed class BuffTextHandlerTable
        {
            static GameDictionary Table => new(BuffTextHandlers_);

            public IBuffTextHandler this[int buffType]
            {
                get => TryGetValue(buffType, out IBuffTextHandler handler) ? handler : throw new KeyNotFoundException($"BuffTextHandlers has no buff {buffType}");
                set => Table.Set(buffType, value.Pointer);
            }

            public void Add(int buffType, IBuffTextHandler handler)
            {
                if (Table.ContainsKey(buffType)) throw new ArgumentException($"BuffTextHandlers already has buff {buffType}");
                Table.Set(buffType, handler.Pointer);
            }

            public bool ContainsKey(int buffType) => Table.ContainsKey(buffType);

            public bool TryGetValue(int buffType, out IBuffTextHandler handler)
            {
                bool found = Table.TryGetValue(buffType, out IntPtr pointer) && pointer != IntPtr.Zero;
                handler = new IBuffTextHandler(found ? pointer : IntPtr.Zero);
                return found;
            }

            public bool Remove(int buffType) => Table.Remove(buffType);
        }
    }
}
