using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace ExampleMod.Content.Items
{
    public class ExampleInstancedItem : ModItem
    {
        public Color[] colors;

        public static LocalizedText EMText { get; private set; }

        public override string Texture => "ExampleMod/Content/Items/ExampleItem";

        public override void SetStaticDefaults() {
            EMText = this.GetLocalization("EM");
        }

        public override void SetDefaults() {
            Item.useAnimation = 30;
            Item.useStyle = ItemUseStyleID.Swing;
        }

        public override ModItem Clone(Item item) {
            ExampleInstancedItem clone = (ExampleInstancedItem)base.Clone(item);
            clone.colors = (Color[])colors?.Clone();
            return clone;
        }

        public override void OnCreated(ItemCreationContext context) {
            GenerateNewColors();
        }

        private void GenerateNewColors() {
            colors = new Color[5];
            for (int i = 0; i < 5; i++) {
                colors[i] = Main.hslToRgb(Main.rand.NextFloat(), 1f, 0.7f);
            }
        }

        public override void ModifyTooltips(List<TooltipLine> tooltips) {
            if (colors == null)
                return;

            for (int i = 0; i < colors.Length; i++) {
                TooltipLine tooltipLine = new TooltipLine(Mod, "EM" + i, EMText.Format(i)) { Color = colors[i] };
                tooltips.Add(tooltipLine);
            }
        }

        public override void UseAnimation(Player player) {
            if (colors == null) {
                GenerateNewColors();
            }
            else {
                colors = colors.Skip(1).Concat(colors.Take(1)).ToArray();
            }
        }

        public override void SaveData(TagCompound tag) {
            tag["Colors"] = colors;
        }

        public override void LoadData(TagCompound tag) {
            colors = tag.Get<Color[]>("Colors");
        }

        public override void AddRecipes() => CreateRecipe().AddIngredient<ExampleItem>(10).Register();
    }
}
