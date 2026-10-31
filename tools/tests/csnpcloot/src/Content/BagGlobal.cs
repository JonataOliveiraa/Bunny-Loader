using Terraria;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.ModLoader;

namespace CsNpcLoot.Content;

public sealed class BagGlobal : GlobalItem
{
    public static int Calls;
    public override bool AppliesToEntity(Item entity, bool lateInstantiation) => entity.type == ItemID.KingSlimeBossBag;
    public override void ModifyItemLoot(Item item, ItemLoot itemLoot) { Calls++; itemLoot.Add(ItemDropRule.Common(ItemID.Sapphire, 1, 4, 4)); }
}
