using ExampleMod.Content.Buffs;
using ExampleMod.Content.Dusts;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace ExampleMod.Common.Players
{

    public class ExampleWeaponEnchantmentPlayer : ModPlayer
    {
        public bool exampleWeaponImbue = false;

        public override void ResetEffects() {
            exampleWeaponImbue = false;
        }

        public override void OnHitNPCWithItem(Item item, NPC target, NPC.HitInfo hit, int damageDone) {
            if (exampleWeaponImbue && item.DamageType.CountsAsClass<MeleeDamageClass>()) {
                target.AddBuff(ModContent.BuffType<ExampleDefenseDebuff>(), 60 * Main.rand.Next(3, 7));
            }
        }

        public override void OnHitNPCWithProj(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone) {
            if (exampleWeaponImbue && (proj.DamageType.CountsAsClass<MeleeDamageClass>() || ProjectileID.Sets.IsAWhip[proj.type]) && !proj.noEnchantments) {
                target.AddBuff(ModContent.BuffType<ExampleDefenseDebuff>(), 60 * Main.rand.Next(3, 7));
            }
        }

        public override void MeleeEffects(Item item, Rectangle hitbox) {
            if (exampleWeaponImbue && item.DamageType.CountsAsClass<MeleeDamageClass>() && !item.noMelee && !item.noUseGraphic) {
                if (Main.rand.NextBool(5)) {
                    Dust dust = Dust.NewDustDirect(new Vector2(hitbox.X, hitbox.Y), hitbox.Width, hitbox.Height, ModContent.DustType<Sparkle>());
                    dust.velocity *= 0.5f;
                }
            }
        }

        public override void EmitEnchantmentVisualsAt(Projectile projectile, Vector2 boxPosition, int boxWidth, int boxHeight) {
            if (exampleWeaponImbue && (projectile.DamageType.CountsAsClass<MeleeDamageClass>() || ProjectileID.Sets.IsAWhip[projectile.type]) && !projectile.noEnchantments) {
                if (Main.rand.NextBool(5)) {
                    Dust dust = Dust.NewDustDirect(boxPosition, boxWidth, boxHeight, ModContent.DustType<Sparkle>());
                    dust.velocity *= 0.5f;
                }
            }
        }
    }
}
