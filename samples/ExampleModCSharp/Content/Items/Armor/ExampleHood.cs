using ExampleMod.Common.Players;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace ExampleMod.Content.Items.Armor
{

    [AutoloadEquip(EquipType.Head)]
    public class ExampleHood : ModItem
    {
        public static readonly int ManaCostReductionPercent = 10;

        public static LocalizedText SetBonusText { get; private set; }

        public override void SetStaticDefaults() {
            SetBonusText = this.GetLocalization("SetBonus");
        }

        public override void SetDefaults() {
            Item.width = 18;
            Item.height = 18;
            Item.value = Item.sellPrice(gold: 1);
            Item.rare = ItemRarityID.Green;
            Item.defense = 4;
        }

        public override bool IsArmorSet(Item head, Item body, Item legs) {
            return body.type == ModContent.ItemType<ExampleBreastplate>() && legs.type == ModContent.ItemType<ExampleLeggings>();
        }

        public override void UpdateArmorSet(Player player) {

            player.setBonus = SetBonusText.Format(ManaCostReductionPercent, this.GetLocalization(Main.ReversedUpDownArmorSetBonuses ? "SetBonusKeyUp" : "SetBonusKeyDown").Value);
            player.manaCost -= ManaCostReductionPercent / 100f;
            player.GetModPlayer<ExampleArmorSetBonusPlayer>().ExampleSetHood = true;
        }

        public override void ArmorSetShadows(Player player) {
            var exampleArmorSetBonusPlayer = player.GetModPlayer<ExampleArmorSetBonusPlayer>();
            if (exampleArmorSetBonusPlayer.ShadowStyle == 1) {
                player.armorEffectDrawShadow = true;
            }
            else if (exampleArmorSetBonusPlayer.ShadowStyle == 2) {
                player.armorEffectDrawOutlines = true;
            }
            else if (exampleArmorSetBonusPlayer.ShadowStyle == 3) {
                player.armorEffectDrawOutlinesForbidden = true;
            }
            else if (exampleArmorSetBonusPlayer.ShadowStyle == 4) {
                exampleArmorSetBonusPlayer.CustomShadow = true;
            }
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient<ExampleItem>()
                .AddTile<Tiles.Furniture.ExampleWorkbench>()
                .Register();
        }
    }
}
