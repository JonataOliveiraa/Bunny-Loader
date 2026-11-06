using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Graphics.Shaders;
using Terraria.ID;
using Terraria.ModLoader;

namespace ExampleMod.Content.Items
{
    public class ExampleDye : ModItem
    {
        public override void SetStaticDefaults() {
            if (!Main.dedServ) {
                GameShaders.Armor.BindShader(
                    Item.type,
                    new ArmorShaderData(Main.PixelShaderRef, "ArmorColored").UseColor(0.2f, 0.9f, 0.4f)
                );
            }

            Item.ResearchUnlockCount = 3;
        }

        public override void SetDefaults() {
            int dye = Item.dye;

            Item.CloneDefaults(ItemID.GelDye);

            Item.dye = (byte)dye;
        }
    }
}
