using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace ExampleMod.Content.Items.Consumables
{
    public class ExampleCanStackItem : ModItem
    {
        public string craftedPlayerName = string.Empty;

        public static LocalizedText CraftedPlayerNameCannotOpenText { get; private set; }
        public static LocalizedText CraftedPlayerNameOtherText { get; private set; }
        public static LocalizedText CraftedPlayerNameEmptyText { get; private set; }

        public override void SetStaticDefaults() {
            CraftedPlayerNameCannotOpenText = this.GetLocalization("CraftedPlayerNameCannotOpen");
            CraftedPlayerNameOtherText = this.GetLocalization("CraftedPlayerNameOther");
            CraftedPlayerNameEmptyText = this.GetLocalization("CraftedPlayerNameEmpty");
        }

        public override void SetDefaults() {
            Item.maxStack = Item.CommonMaxStack;
            Item.consumable = true;
            Item.width = 22;
            Item.height = 26;
            Item.rare = ItemRarityID.Blue;
        }

        public override bool CanRightClick() {
            if (craftedPlayerName == string.Empty) {
                return false;
            }

            return Main.LocalPlayer.name != craftedPlayerName;
        }

        public override bool CanStack(Item source) {
            var name1 = craftedPlayerName;
            var name2 = ((ExampleCanStackItem)source.ModItem).craftedPlayerName;

            if (name1 == string.Empty) {
                name1 = Main.LocalPlayer.name;
            }
            if (name2 == string.Empty) {
                name2 = Main.LocalPlayer.name;
            }

            return name1 == name2;
        }

        public override void OnStack(Item source, int numToTransfer) {
            if (craftedPlayerName == string.Empty) {
                craftedPlayerName = ((ExampleCanStackItem)source.ModItem).craftedPlayerName;
            }
        }

        public override void ModifyItemLoot(ItemLoot itemLoot) {
            LeadingConditionRule hardmodeCondition = new(new Conditions.IsHardmode());
            hardmodeCondition.OnSuccess(ItemDropRule.Common(ItemID.ChocolateChipCookie));
            hardmodeCondition.OnFailedConditions(ItemDropRule.Common(ItemID.Coconut));
            itemLoot.Add(hardmodeCondition);
        }

        public override void SaveData(TagCompound tag) {
            tag.Add("craftedPlayerName", craftedPlayerName);
        }

        public override void LoadData(TagCompound tag) {
            craftedPlayerName = tag.GetString("craftedPlayerName");
        }

        public override void NetSend(BinaryWriter writer) {
            writer.Write(craftedPlayerName);
        }

        public override void NetReceive(BinaryReader reader) {
            craftedPlayerName = reader.ReadString();
        }

        public override void ModifyTooltips(List<TooltipLine> tooltips) {
            if (!CanRightClick()) {
                tooltips.RemoveAll(line => line.Mod == "Terraria" && line.Name == "Tooltip0");
            }

            if (craftedPlayerName != string.Empty) {
                if (Main.LocalPlayer.name == craftedPlayerName) {
                    tooltips.Add(new TooltipLine(Mod, "CraftedPlayerNameCannotOpen", CraftedPlayerNameCannotOpenText.Value));
                }
                else {
                    tooltips.Add(new TooltipLine(Mod, "CraftedPlayerNameOther", CraftedPlayerNameOtherText.Format(craftedPlayerName)));
                }
            }
            else {
                tooltips.Add(new TooltipLine(Mod, "CraftedPlayerNameEmpty", CraftedPlayerNameEmptyText.Value));
            }
        }

        public override void OnCreated(ItemCreationContext context) {
            if (context is RecipeItemCreationContext) {
                craftedPlayerName = Main.LocalPlayer.name;
            }
        }

        public override void AddRecipes() {
            CreateRecipe().
                AddIngredient<ExampleItem>(20).
                AddTile(TileID.WorkBenches).
                Register();
        }
    }
}
