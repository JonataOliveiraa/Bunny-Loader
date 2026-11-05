using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace ExampleMod.Content.Items.Armor.Vanity
{
    [AutoloadEquip(EquipType.Head)]
    public class MinionBossMask : ModItem
    {
        public override void SetDefaults() {
            Item.width = 22;
            Item.height = 28;

            Item.rare = ItemRarityID.Blue;
            Item.value = Item.sellPrice(silver: 75);
            Item.vanity = true;
            Item.maxStack = 1;
        }
    }
}
