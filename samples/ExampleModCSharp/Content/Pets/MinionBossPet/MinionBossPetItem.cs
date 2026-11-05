using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace ExampleMod.Content.Pets.MinionBossPet
{
    public class MinionBossPetItem : ModItem
    {
        public override void SetDefaults() {
            Item.DefaultToVanitypet(ModContent.ProjectileType<MinionBossPetProjectile>(), ModContent.BuffType<MinionBossPetBuff>());

            Item.width = 28;
            Item.height = 20;
            Item.rare = ItemRarityID.Master;
            Item.value = Item.sellPrice(0, 5);
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            player.AddBuff(Item.buffType, 2);

            return false;
        }
    }
}
