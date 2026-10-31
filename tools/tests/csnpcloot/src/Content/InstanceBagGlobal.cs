using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CsNpcLoot.Content;

public sealed class InstanceBagGlobal : GlobalItem
{
    int marker;
    public static bool Valid = true;
    public static int Calls;
    public override bool InstancePerEntity => true;
    public override bool AppliesToEntity(Item item, bool lateInstantiation) => item.type == ItemID.KingSlimeBossBag || item.type == ModContent.ItemType<LootBag>();
    public override void SetDefaults(Item item) => marker = 21;
    public override void ModifyItemLoot(Item item, ItemLoot itemLoot)
    {
        Calls++;
        Valid &= marker == 21 && ReferenceEquals(this, item.GetGlobalItem<InstanceBagGlobal>());
    }
}
