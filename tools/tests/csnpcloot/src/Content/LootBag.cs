using Terraria;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.ModLoader;

namespace CsNpcLoot.Content;

public sealed class LootBag : ModItem
{
    public static int Calls;
    public override string Texture => "Content/LootBag";
    public override void SetStaticDefaults() => ItemID.Sets.BossBag[Type] = true;
    public override void SetDefaults() { Item.width = Item.height = 16; Item.maxStack = Item.CommonMaxStack; }
    public override bool CanRightClick() => true;
    public override void ModifyItemLoot(ItemLoot itemLoot)
    {
        Calls++;
        itemLoot.Add(ItemDropRule.Common(ModContent.ItemType<LootItem>(), 1, 3, 3));
        itemLoot.Add(ItemDropRule.ByCondition(new BagCondition(), ItemID.Diamond));
    }
}
