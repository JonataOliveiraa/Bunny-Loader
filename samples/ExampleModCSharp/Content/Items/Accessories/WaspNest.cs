using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace ExampleMod.Content.Items.Accessories;

[AutoloadEquip(EquipType.Back)]
public class WaspNest : ModItem
{
    public override void Load()
    {
        On_Player.beeType += BeeType;
    }

    private static int BeeType(On_Player.orig_beeType orig, Player self)
    {
        int type = orig(self);
        if (type == ProjectileID.GiantBee && self.GetModPlayer<WaspNestPlayer>().strongBeesUpgrade && Main.rand.NextBool(10) && Main.ProjectileUpdateLoopIndex == -1)
        {
            return ProjectileID.Beenade;
        }

        return type;
    }

    public override void SetDefaults()
    {
        sbyte realBackSlot = Item.backSlot;
        Item.CloneDefaults(ItemID.HiveBackpack);
        Item.value = Item.sellPrice(0, 5);
        Item.backSlot = realBackSlot;
    }

    public override void UpdateAccessory(Player player, bool hideVisual)
    {
        player.strongBees = true;
        WaspNestPlayer waspNestPlayer = player.GetModPlayer<WaspNestPlayer>();
        waspNestPlayer.strongBeesUpgrade = true;
        waspNestPlayer.strongBeesItem = Item;
    }

    public override bool CanAccessoryBeEquippedWith(Item equippedItem, Item incomingItem, Player player)
    {
        return incomingItem.type != ItemID.HiveBackpack;
    }
}

public class WaspNestPlayer : ModPlayer
{
    public bool strongBeesUpgrade;
    public Item strongBeesItem;

    public override void ResetEffects()
    {
        strongBeesUpgrade = false;
        strongBeesItem = default;
    }

    public override void OnHurt(Player.HurtInfo info)
    {
        if (Player.whoAmI != Main.myPlayer)
        {
            return;
        }

        if (!strongBeesItem.IsNull && Main.rand.NextBool(3))
        {
            int baseDamage = 20;
            if (strongBeesItem.ModItem is WaspNest)
            {
                baseDamage += 10;
            }

            IEntitySource projectileSource_Accessory = Player.GetSource_Accessory_OnHurt(strongBeesItem, info.DamageSource);
            Projectile.NewProjectile(projectileSource_Accessory, Player.Center, Utils.NextVector2Circular(Main.rand, 3, 3), Player.beeType(), Player.beeDamage(baseDamage), Player.beeKB(0f), Main.myPlayer);
        }
    }
}
