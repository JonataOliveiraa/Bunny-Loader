using Microsoft.Xna.Framework;
using Terraria.DataStructures;

namespace Terraria.GameContent.ItemDropRules;

public static partial class CommonCode
{
    public static void DropItem(DropAttemptInfo info, int item, int stack, bool scattered = false)
    {
        if (!info.npc.IsNull) DropItemFromNPC(info.npc, item, stack, scattered);
        else if (!info.player.IsNull) DropItem(info.player.Hitbox, info.player.GetItemSource_OpenItem(info.item), item, stack, scattered);
    }

    public static int DropItem(Rectangle rectangle, IEntitySource entitySource, int itemId, int stack, bool scattered)
    {
        Vector2 position = scattered
            ? new Vector2(rectangle.X + Main.rand.Next(rectangle.Width + 1), rectangle.Y + Main.rand.Next(rectangle.Height + 1))
            : new Vector2(rectangle.X + rectangle.Width / 2f, rectangle.Y + rectangle.Height / 2f);
        return DropItem(position, entitySource, itemId, stack);
    }

    public static int DropItem(Vector2 position, IEntitySource entitySource, int itemId, int stack)
    {
        int index = Item.NewItem(entitySource, position, itemId, stack, prefix: -1);
        if (Main.netMode == 1) NetMessage.SendData(21, number: index, number2: 1f);
        return index;
    }
}
