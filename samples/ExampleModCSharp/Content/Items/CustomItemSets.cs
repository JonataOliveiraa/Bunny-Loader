using ExampleMod.Content.Items.Consumables;
using ExampleMod.Content.Items.Weapons;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace ExampleMod.Content.Items
{
    [ReinitializeDuringResizeArrays]
    public static class CustomItemSets
    {
        public const string FlamingWeaponCustomSetKey = "FlamingWeapon";

        public static bool[] FlamingWeapon = ItemID.Sets.Factory.CreateNamedSet(FlamingWeaponCustomSetKey)
            .Description("Causes \"Hahahah, burn!\" to randomly show in chat when used")
            .RegisterBoolSet(false, ItemID.FieryGreatsword, ModContent.ItemType<ExampleSword>());
    }

    public class CustomItemSetsSystem : ModSystem
    {
        public override void Load() {
        }

        public override void ResizeArrays() {
        }

        public override void SetStaticDefaults() {
            CustomItemSets.FlamingWeapon[ItemID.FireWhip] = true;
            CustomItemSets.FlamingWeapon[ItemID.HelFire] = true;
        }
    }

    public class CustomSetsModPlayer : ModPlayer
    {
        public override void OnHitAnything(float x, float y, Entity victim) {
            if (CustomItemSets.FlamingWeapon[Player.HeldItem.type] && Main.rand.NextBool(100)) {
                CombatText.NewText(Player.getRect(), Color.Red, "Hahahah, burn!");
            }
        }
    }
}
