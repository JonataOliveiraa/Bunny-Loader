using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;

namespace ExampleMod.Common.Players;

public class ExampleShiftClickSlotPlayer : ModPlayer
{
    public override bool ShiftClickSlot(Item[] inventory, int context, int slot)
    {
        if (context == ItemSlot.Context.InventoryItem && inventory[slot].type == ItemID.Gel)
        {
            inventory[slot].color = Main.DiscoColor;
            inventory[slot].rare = Main.rand.Next(ItemRarityID.Count);
            SoundEngine.PlaySound(SoundID.Item4);
            return true;
        }
        return base.ShiftClickSlot(inventory, context, slot);
    }

    public override bool HoverSlot(Item[] inventory, int context, int slot)
    {
        if (context == ItemSlot.Context.InventoryItem && inventory[slot].type == ItemID.Gel)
        {
            if (ItemSlot.ShiftInUse)
            {
                Main.cursorOverride = CursorOverrideID.FavoriteStar;
                return true;
            }
        }
        return base.HoverSlot(inventory, context, slot);
    }
}
