using CsSource.Items;
using Terraria;
using Terraria.GameContent.Items;
using Terraria.ID;
using Terraria.ModLoader;

namespace CsSource;

public sealed class CsSource : Mod
{
    public override void Load()
    {
        On_Item.SetDefaults += SetDefaults;

        Item item = Item.New();
        item.SetDefaults(ItemID.Minishark, default);
        bool ok = item.useTime == MinisharkTuning.UseTime && item.damage == MinisharkTuning.Damage;
        Logger.Info($"cssource load useTime={item.useTime} damage={item.damage} {(ok ? "ok" : "FAIL")}");
    }

    static void SetDefaults(On_Item.orig_SetDefaults orig, Item self, int type, ItemVariant variant)
    {
        orig(self, type, variant);
        if (type == ItemID.Minishark) MinisharkTuning.Apply(self);
    }
}
