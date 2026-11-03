using System.Collections.Generic;
using BunnyLoader.Interop;

namespace Terraria.GameContent.Items;

public readonly partial struct WhipTagEffect
{
    static readonly List<GameHandle> Kept = new();

    public WhipTagEffect()
    {
        WhipTagEffect effect = New();
        lock (Kept) Kept.Add(new GameHandle(effect.Pointer));
        Pointer = effect.Pointer;
    }
}
