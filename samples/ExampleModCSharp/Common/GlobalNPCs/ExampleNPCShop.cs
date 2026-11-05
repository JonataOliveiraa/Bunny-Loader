using ExampleMod.Content.Currencies;
using ExampleMod.Content.Items;
using ExampleMod.Content.Items.Ammo;
using ExampleMod.Content.Items.Consumables;
using ExampleMod.Content.Items.Mounts;
using ExampleMod.Content.Items.Tools;
using ExampleMod.Content.NPCs;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace ExampleMod.Common.GlobalNPCs
{
    class ExampleNPCShop : GlobalNPC
    {
        public override void ModifyShop(NPCShop shop) {
            if (shop.NpcType == NPCID.Dryad) {

                shop.Add<ExampleMountItem>();

                shop.Add(new Item(ModContent.ItemType<ExampleMountItem>()) {
                    shopCustomPrice = 2,
                    shopSpecialCurrency = CustomCurrencyID.DefenderMedals 
                });

                shop.Add(new Item(ModContent.ItemType<ExampleMountItem>()) {
                    shopCustomPrice = 2,
                    shopSpecialCurrency = ExampleCustomCurrencies.ExampleItemCurrency,
                });
            }
            else if (shop.NpcType == NPCID.Wizard) {
                
            }
            else if (shop.NpcType == NPCID.Stylist) {
                if (ModContent.TryFind<ModItem>("ExampleMod/ExampleHairDye", out var hairDye)) shop.Add(hairDye.Type);
            }
            else if (shop.NpcType == NPCID.BestiaryGirl) {
                if (ModContent.TryFind<ModItem>("ExampleMod/ExampleTownPetLicense", out var license)) shop.Add(license.Type, Condition.BestiaryFilledPercent(50));
            }
            else if (shop.NpcType == NPCID.Cyborg) {
                shop.Add<ExampleRocket>(Condition.NpcIsPresent(ModContent.NPCType<ExamplePerson>()));
            }
            else if (shop.NpcType == NPCID.Golfer) {
                shop.Add<ExampleGolfClub>(Condition.GolfScoreOver(250));
            }

            if (shop.FullName != NPCShopDatabase.GetShopName(NPCID.Merchant, "Shop"))
                return;

            if (shop.NpcType != NPCID.Merchant || shop.Name != "Shop")
                return;

            if (shop.NpcType != NPCID.Merchant)
                return;

            if (ModContent.TryFind<ModItem>("ExampleMod/ExampleTorch", out var torch)) shop.InsertAfter(ItemID.Torch, torch.Type, Condition.TimeDay);

            shop.GetEntry(ItemID.CopperAxe).Disable();

            if (shop.TryGetEntry(ItemID.CopperPickaxe, out NPCShop.Entry entry)) {
                entry.Disable();
            }

            shop.GetEntry(ItemID.BlueFlare).AddCondition(Condition.InSnow);

            shop.Add<ExampleItem>(Condition.HappyWindyDay, Condition.CurrentPriceAdjustmentUnder(0.9f));

            var redPotCondition = new Condition("Mods.ExampleMod.Conditions.NotSellingExampleItem", () => !Condition.HappyWindyDay.IsMet() || !Condition.CurrentPriceAdjustmentUnder(0.9f).IsMet());
            
            shop.Add(ItemID.RedPotion, redPotCondition, Condition.ForTheWorthyWorld);
        }
    }
}
