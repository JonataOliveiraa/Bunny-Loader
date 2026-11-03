using ExampleMod.Common.Players;
using ExampleMod.Content.Dusts;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace ExampleMod.Content.Items.Armor;

public class ExampleCostume : ModItem
{
    public override void Load()
    {
        if (Main.netMode == NetmodeID.Server)
            return;

        EquipLoader.AddEquipTexture(Mod, $"{Texture}_{EquipType.Head}", EquipType.Head, this, equipTexture: new BlockyHead());
        EquipLoader.AddEquipTexture(Mod, $"{Texture}_{EquipType.Body}", EquipType.Body, this);
        EquipLoader.AddEquipTexture(Mod, $"{Texture}_{EquipType.Legs}", EquipType.Legs, this);

        EquipLoader.AddEquipTexture(Mod, $"{Texture}Alt_{EquipType.Head}", EquipType.Head, this, name: "BlockyAlt", equipTexture: new BlockyHead());
        EquipLoader.AddEquipTexture(Mod, $"{Texture}Alt_{EquipType.Body}", EquipType.Body, this, name: "BlockyAlt");
        EquipLoader.AddEquipTexture(Mod, $"{Texture}Alt_{EquipType.Legs}", EquipType.Legs, this, name: "BlockyAlt");
    }

    private void SetupDrawing()
    {
        if (Main.netMode == NetmodeID.Server)
            return;

        int equipSlotHead = EquipLoader.GetEquipSlot(Mod, Name, EquipType.Head);
        int equipSlotBody = EquipLoader.GetEquipSlot(Mod, Name, EquipType.Body);
        int equipSlotLegs = EquipLoader.GetEquipSlot(Mod, Name, EquipType.Legs);

        int equipSlotHeadAlt = EquipLoader.GetEquipSlot(Mod, "BlockyAlt", EquipType.Head);
        int equipSlotBodyAlt = EquipLoader.GetEquipSlot(Mod, "BlockyAlt", EquipType.Body);
        int equipSlotLegsAlt = EquipLoader.GetEquipSlot(Mod, "BlockyAlt", EquipType.Legs);

        ArmorIDs.Head.Sets.DrawHead[equipSlotHead] = false;
        ArmorIDs.Head.Sets.DrawHead[equipSlotHeadAlt] = false;
        ArmorIDs.Body.Sets.HidesTopSkin[equipSlotBody] = true;
        ArmorIDs.Body.Sets.HidesArms[equipSlotBody] = true;
        ArmorIDs.Body.Sets.HidesTopSkin[equipSlotBodyAlt] = true;
        ArmorIDs.Body.Sets.HidesArms[equipSlotBodyAlt] = true;
        ArmorIDs.Legs.Sets.HidesBottomSkin[equipSlotLegs] = true;
        ArmorIDs.Legs.Sets.HidesBottomSkin[equipSlotLegsAlt] = true;
    }

    public override void SetStaticDefaults()
    {
        SetupDrawing();
    }

    public override void SetDefaults()
    {
        Item.width = 24;
        Item.height = 28;
        Item.accessory = true;
        Item.value = Item.buyPrice(gold: 15);
        Item.rare = ItemRarityID.Pink;
        Item.hasVanityEffects = true;
    }

    public override void UpdateAccessory(Player player, bool hideVisual)
    {
        var p = player.GetModPlayer<ExampleCostumePlayer>();
        p.BlockyAccessory = true;
        p.BlockyHideVanity = hideVisual;
    }

    public override void UpdateVanity(Player player)
    {
        var p = player.GetModPlayer<ExampleCostumePlayer>();
        p.BlockyHideVanity = false;
        p.BlockyForceVanity = true;
    }
}

public class BlockyHead : EquipTexture
{
    public override bool IsVanitySet(int head, int body, int legs) => true;

    public override void UpdateVanitySet(Player player)
    {
        if (Main.rand.NextBool(20))
        {
            if (Name == "ExampleCostume")
            {
                Dust.NewDust(player.position, player.width, player.height, ModContent.DustType<Sparkle>());
            }
            else
            {
                Dust.NewDust(player.position, player.width, player.height, DustID.BlueFlare);
            }
        }
    }
}
