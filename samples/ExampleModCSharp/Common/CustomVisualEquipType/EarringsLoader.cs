using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using ReLogic.Utilities;
using System.Collections.Generic;
using Terraria.ID;
using Terraria.ModLoader;

namespace ExampleMod.Common.CustomVisualEquipType
{

    public class EarringsLoader : ModSystem
    {

        internal static readonly Dictionary<int, Asset<Texture2D>> earringItemToTexture = new();

        public override void ResizeArrays() {

            for (int i = ItemID.Count; i < ItemLoader.ItemCount; i++) {
                ModItem modItem = ItemLoader.GetItem(i);
                if (modItem != null && System.Reflection.CustomAttributeExtensions.GetCustomAttribute<AutoloadEquip_EarringAttribute>(modItem.GetType()) != null) {
                    AddEarringTexture(modItem.Type, $"{modItem.Texture}_Earrings");
                }
            }

            AddEarringTexture(ItemID.AnglerEarring, "ExampleMod/Common/CustomVisualEquipType/AnglerEarring_Earrings");
        }

        public static void AddEarringTexture(int type, string texture) {
            var asset = ModContent.Request<Texture2D>(texture);
            earringItemToTexture[type] = asset;
        }
    }
}
