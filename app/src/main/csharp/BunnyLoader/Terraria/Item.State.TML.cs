using Terraria.ModLoader.Core;
using Terraria.Localization;

namespace Terraria;

public readonly partial struct Item
{
    public bool StopAnimationOnHurt
    {
        get => ItemUseState.StopOnHurt(this);
        set => ItemUseState.SetStopOnHurt(this, value);
    }

    public void NetStateChanged()
    {
        if (Main.netMode == 0) return;
        Player player = Main.LocalPlayer;
        if (!player.IsNull)
        {
            var inventory = player.inventory;
            for (int slot = 0; slot < inventory.Length; slot++)
            {
                if (inventory[slot] != this) continue;
                NetMessage.SendData(5, -1, -1, default(NetworkText), player.whoAmI, slot, prefix, 0, 0, 0, 0);
                return;
            }
        }
        WorldItem world = GetWorldItem();
        if (!world.IsNull) NetMessage.SendData(21, -1, -1, default(NetworkText), world.whoAmI, 0, 0, 0, 0, 0, 0);
    }
}
