using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace ExampleMod.Common.GlobalItems
{

    public class ShortswordGlobalItem : GlobalItem
    {
        
        public override bool AppliesToEntity(Item item, bool lateInstantiation) {
            return item.type == ItemID.CopperShortsword;
        }

        public override void SetDefaults(Item item) {
 

            item.damage = 50; 
        }

        public override bool Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            
            Projectile.NewProjectileDirect(source, player.Center, velocity * 5f, ProjectileID.Grenade, damage, knockback, player.whoAmI);

            return false;
        }
    }
}
