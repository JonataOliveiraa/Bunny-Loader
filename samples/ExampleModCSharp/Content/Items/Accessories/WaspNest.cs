using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace ExampleMod.Content.Items.Accessories;

[AutoloadEquip(EquipType.Back)]
public class WaspNest : ModItem
{
    // Only gets run once per type
    public override void Load()
    {
        On_Player.beeType += BeeType;
    }

    // tModLoader does this with an IL edit, which can't exist on the phone. An On_ hook does the same here: Player.beeType returns 566 (Giant Bee) when the
    // player has the Hive Pack and the roll succeeds, so we let the original method run first and then change its return value.
    private static int BeeType(On_Player.orig_beeType orig, Player self)
    {
        int type = orig(self);
        // Regular c# code
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
        // CloneDefaults will clear out the autoloaded Back slot, so we need to preserve it this way.
        Item.backSlot = realBackSlot;
    }

    public override void UpdateAccessory(Player player, bool hideVisual)
    {
        // The original Hive Pack sets strongBees.
        player.strongBees = true;
        // Here we add an additional effect
        WaspNestPlayer waspNestPlayer = player.GetModPlayer<WaspNestPlayer>();
        waspNestPlayer.strongBeesUpgrade = true;
        waspNestPlayer.strongBeesItem = Item;
    }

    public override bool CanAccessoryBeEquippedWith(Item equippedItem, Item incomingItem, Player player)
    {
        // Don't allow Hive Pack and Wasp Nest to be equipped at the same time.
        return incomingItem.type != ItemID.HiveBackpack;
    }
}

public class WaspNestPlayer : ModPlayer
{
    public bool strongBeesUpgrade;
    public Item strongBeesItem; // Some effects need to refer back to the accessory Item

    public override void ResetEffects()
    {
        strongBeesUpgrade = false;
        strongBeesItem = default;
    }

    // Spawn a bee when damaged, similar to the Honey Comb accessory effect
    // This is just an example of the concept of storing an Item instance for accessory effects and GetSource_Accessory or GetSource_Accessory_OnHurt,
    // if you actually are making an accessory with the existing Honey Comb effect, just set "player.honeyCombItem = Item;" in UpdateAccessory instead
    public override void OnHurt(Player.HurtInfo info)
    {
        if (Player.whoAmI != Main.myPlayer)
        {
            return;
        }

        if (!strongBeesItem.IsNull && Main.rand.NextBool(3))
        {
            int baseDamage = 20;

            // By storing the Item instance, we can create varying effects for different "tiers" of accessories. 
            if (strongBeesItem.ModItem is WaspNest)
            {
                baseDamage += 10;
            }

            /*
            if (strongBeesItem.ModItem is WaspNestV2) {
                baseDamage += 30;
            }
            */

            IEntitySource projectileSource_Accessory = Player.GetSource_Accessory_OnHurt(strongBeesItem, info.DamageSource);
            Projectile.NewProjectile(projectileSource_Accessory, Player.Center, Utils.NextVector2Circular(Main.rand, 3, 3), Player.beeType(), Player.beeDamage(baseDamage), Player.beeKB(0f), Main.myPlayer);
        }
    }
}
