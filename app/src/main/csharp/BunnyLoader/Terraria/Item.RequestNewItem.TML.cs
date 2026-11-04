using Microsoft.Xna.Framework;
using Terraria.DataStructures;

namespace Terraria;

public readonly partial struct Item
{
    public static void RequestNewItem(IEntitySource source, Vector2 center, int type, int stack = 1, int prefix = 0, NewItemOwnership ownership = NewItemOwnership.None) =>
        RequestNewItem(source, center, type, stack, prefix, ownership, default, default(NewItemModifier));
}
