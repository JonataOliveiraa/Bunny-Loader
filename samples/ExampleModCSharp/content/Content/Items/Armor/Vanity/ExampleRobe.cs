using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace ExampleMod.Content.Items.Armor.Vanity;

[AutoloadEquip(EquipType.Body)]
public class ExampleRobe : ModItem
{
    public override void Load()
    {
        if (Main.netMode == NetmodeID.Server)
        {
            return;
        }

        EquipLoader.AddEquipTexture(Mod, $"{Texture}_{EquipType.Legs}", EquipType.Legs, this);
    }

    public override void SetStaticDefaults()
    {
        ArmorIDs.Body.Sets.HidesHands[Item.bodySlot] = false;
    }

    public override void SetDefaults()
    {
        Item.width = 18;
        Item.height = 14;
        Item.rare = ItemRarityID.Blue;
        Item.vanity = true;
    }

    public override void SetMatch(bool male, ref int equipSlot, ref bool robes)
    {
        robes = true;
        equipSlot = EquipLoader.GetEquipSlot(Mod, Name, EquipType.Legs);
    }
}
