using ExampleMod.Common.Players;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace ExampleMod.Common.Players
{

    public class SimpleModPlayer : ModPlayer
    {

        public bool FrostBurnSummon;

        public override void ResetEffects() {
            FrostBurnSummon = false;
        }

        public override void OnHitNPCWithProj(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone) {

            if (proj.IsMinionOrSentryRelated && FrostBurnSummon && !proj.noEnchantments) {

                target.AddBuff(BuffID.Frostburn, 60 * Main.rand.Next(3, 6));
            }
        }

    }
}

namespace ExampleMod.Content.Items.Accessories
{

    [AutoloadEquip(EquipType.Neck, EquipType.Balloon)]
    public class SimpleAccessory : ModItem
    {
        public override void SetDefaults() {
            Item.width = 34;
            Item.height = 34;
            Item.accessory = true;
            Item.value = Item.buyPrice(gold: 15);
            Item.rare = ItemRarityID.Pink;
        }

        public override void UpdateAccessory(Player player, bool hideVisual) {

            player.GetModPlayer<SimpleModPlayer>().FrostBurnSummon = true;
        }
    }
}
