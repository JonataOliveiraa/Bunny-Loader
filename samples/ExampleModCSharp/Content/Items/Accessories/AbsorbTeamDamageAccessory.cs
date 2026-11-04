using ExampleMod.Common.Players;
using ExampleMod.Content.Buffs;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace ExampleMod.Content.Items.Accessories
{

    [AutoloadEquip(EquipType.Shield)]
    public class AbsorbTeamDamageAccessory : ModItem
    {
        public static readonly int DamageAbsorptionAbilityLifeThresholdPercent = 50;
        public static float DamageAbsorptionAbilityLifeThreshold => DamageAbsorptionAbilityLifeThresholdPercent / 100f;

        public static readonly int DamageAbsorptionPercent = 30;
        public static float DamageAbsorptionMultiplier => DamageAbsorptionPercent / 100f;

        public static readonly int DamageAbsorptionRange = 800;

        public override LocalizedText Tooltip => base.Tooltip.WithFormatArgs(DamageAbsorptionPercent, DamageAbsorptionAbilityLifeThresholdPercent);

        public override void SetDefaults() {
            Item.width = 24;
            Item.height = 24;
            Item.accessory = true;
            Item.rare = ItemRarityID.Yellow;
            Item.defense = 6;
            Item.value = Item.buyPrice(0, 30, 0, 0);
        }

        public override void UpdateAccessory(Player player, bool hideVisual) {

            player.GetModPlayer<ExampleDamageModificationPlayer>().hasAbsorbTeamDamageEffect = true;

            if (player.whoAmI != Main.myPlayer && player.miscCounter % 10 == 0) {
                Player localPlayer = Main.LocalPlayer;
                if (localPlayer.team == player.team && player.team != 0 && player.statLife > player.statLifeMax2 * DamageAbsorptionAbilityLifeThreshold && player.Distance(localPlayer.Center) <= DamageAbsorptionRange) {

                    localPlayer.AddBuff(ModContent.BuffType<AbsorbTeamDamageBuff>(), 20);
                }
            }
        }
    }
}
