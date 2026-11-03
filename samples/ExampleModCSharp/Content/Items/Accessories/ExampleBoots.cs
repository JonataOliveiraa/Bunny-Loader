using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace ExampleMod.Content.Items.Accessories;

[AutoloadEquip(EquipType.Shoes)]
public class ExampleBoots : ModItem
{
    public static readonly int MoveSpeedBonus = 8;
    public static readonly int LavaImmunityTime = 2;

    public override LocalizedText Tooltip => base.Tooltip.WithFormatArgs(MoveSpeedBonus, LavaImmunityTime);

    public override void SetDefaults()
    {
        Item.width = 22;
        Item.height = 22;

        Item.accessory = true;
        Item.rare = ItemRarityID.Red;
        Item.value = Item.buyPrice(gold: 1);
    }

    public override void UpdateAccessory(Player player, bool hideVisual)
    {
        player.moveSpeed += MoveSpeedBonus / 100f;
        player.accRunSpeed = 6.75f;
        player.rocketBoots = 2;
        player.vanityRocketBoots = 2;

        player.waterWalk2 = true;
        player.waterWalk = true;
        player.iceSkate = true;
        player.desertBoots = true;

        player.fireWalk = true;
        player.noFallDmg = true;
        player.lavaRose = true;
        player.lavaMax += LavaImmunityTime * 60;

        if (!hideVisual)
        {
            player.CancelAllBootRunVisualEffects();
            player.hellfireTreads = true;

            if (!player.mount.Active || player.mount.Type != MountID.WallOfFleshGoat)
            {
                player.DoBootsEffect(player.DoBootsEffect_PlaceFlamesOnTile);
            }
        }
    }

    public override void UpdateVanity(Player player)
    {
        player.CancelAllBootRunVisualEffects();
        player.vanityRocketBoots = 2;
        player.hellfireTreads = true;

        if (!player.mount.Active || player.mount.Type != MountID.WallOfFleshGoat)
        {
            player.DoBootsEffect(player.DoBootsEffect_PlaceFlamesOnTile);
        }
    }
}
