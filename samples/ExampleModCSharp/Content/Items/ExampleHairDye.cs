using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent.Dyes;
using Terraria.Graphics.Shaders;
using Terraria.ID;
using Terraria.ModLoader;

namespace ExampleMod.Content.Items
{
    public class ExampleHairDye : ModItem
    {
        public override void SetStaticDefaults() {
            if (!Main.dedServ) {
                GameShaders.Hair.BindShader(
                    Item.type,
                    new LegacyHairShaderData().UseLegacyMethod((Player player, Color newColor, ref bool lighting) => Main.DiscoColor)
                );
            }

            Item.ResearchUnlockCount = 3;
        }

        public override void SetDefaults() {
            Item.width = 20;
            Item.height = 26;
            Item.maxStack = Item.CommonMaxStack;
            Item.value = Item.buyPrice(gold: 5);
            Item.rare = ItemRarityID.Green;
            Item.UseSound = SoundID.Item3;
            Item.useStyle = ItemUseStyleID.DrinkLiquid;
            Item.useTurn = true;
            Item.useAnimation = 17;
            Item.useTime = 17;
            Item.consumable = true;
        }
    }
}
