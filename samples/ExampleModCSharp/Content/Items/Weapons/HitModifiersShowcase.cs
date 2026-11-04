using ExampleMod.Common.Players;
using ExampleMod.Content.Buffs;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.IO;
using System.Linq;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace ExampleMod.Content.Items.Weapons
{

    public class HitModifiersShowcase : ModItem
    {
        public override string Texture => "ExampleMod/Content/Items/Weapons/ExampleSword";

        private const int numberOfModes = 8;
        private int mode = 0;

        public const int ExtraKnockback = 50;
        public const int ExtraCriticalHitDamage = 200;
        public const int ExtraArmorPenetration = 10;
        public const int ExtraScalingArmorPenetration = 50;

        public static LocalizedText SwitchingText { get; private set; }
        public static LocalizedText CurrentModeText { get; private set; }
        public static LocalizedText[] ModeText { get; private set; }

        public override void SetStaticDefaults() {
            SwitchingText = this.GetLocalization("Switching");
            CurrentModeText = this.GetLocalization("CurrentMode");
            ModeText = Enumerable.Range(0, 8).Select(i => this.GetLocalization($"Mode_{i}")).ToArray();
        }

        public override void SetDefaults() {
            Item.width = 40;
            Item.height = 40;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.useTime = 15;
            Item.useAnimation = 15;
            Item.autoReuse = true;
            Item.UseSound = SoundID.Item1;

            Item.DamageType = DamageClass.Melee;
            Item.damage = 100;
            Item.knockBack = 5;
            Item.crit = 10;
        }

        public override void NetSend(BinaryWriter writer) {
            writer.Write((byte)mode);
        }

        public override void NetReceive(BinaryReader reader) {
            mode = reader.ReadByte();
        }

        public override bool AltFunctionUse(Player player) {
            return true;
        }

        public override bool? UseItem(Player player) {
            if (player.whoAmI != Main.myPlayer) {
                return true;
            }

            if (player.altFunctionUse == 2) {
                mode++;
                if (mode >= numberOfModes) {
                    mode = 0;
                }
                Main.NewText(SwitchingText.Format(mode, GetMessageForMode()));

                Item.NetStateChanged();
            }
            else {
                Main.NewText(CurrentModeText.Format(mode, GetMessageForMode()));
            }
            return true;
        }

        public override void MeleeEffects(Player player, Rectangle hitbox) {
            if (!Main.rand.NextBool(3))
                return;
            Dust.NewDust(new Vector2(hitbox.X, hitbox.Y), hitbox.Width, hitbox.Height, DustID.Firework_Red + mode);
        }

        private string GetMessageForMode() {
            return mode switch {
                0 => ModeText[0].Value,
                1 => ModeText[1].Value,
                2 => ModeText[2].Format(ExtraKnockback),
                3 => ModeText[3].Format(ExtraCriticalHitDamage),
                4 => ModeText[4].Format(ExtraArmorPenetration),

                5 => ModeText[5].Format(ExtraScalingArmorPenetration),
                6 => ModeText[6].Format(ExampleDefenseDebuff.DefenseReductionPercent),
                7 => ModeText[7].Value,
                _ => "Unknown mode",
            };
        }

        public override void ModifyHitNPC(Player player, NPC target, ref NPC.HitModifiers modifiers) {

            if (mode != 0) {
                modifiers.DamageVariationScale *= 0f;
            }
            if (mode == 2) {
                modifiers.Knockback += ExtraKnockback / 100f;
            }
            else if (mode == 3) {
                modifiers.CritDamage += ExtraCriticalHitDamage / 100f;
            }
            else if (mode == 4) {
                modifiers.ArmorPenetration += ExtraArmorPenetration;
            }
            else if (mode == 5) {

                modifiers.ScalingArmorPenetration += ExtraScalingArmorPenetration / 100f;
            }

        }

        public override void OnHitNPC(Player player, NPC target, NPC.HitInfo hit, int damageDone) {

            if (mode == 6) {
                target.AddBuff(ModContent.BuffType<ExampleDefenseDebuff>(), 600);
            }

        }

        public override void ModifyHitPvp(Player player, Player target, ref Player.HurtModifiers modifiers) {

            if (mode == 2) {
                modifiers.Knockback += .5f;
            }
            else if (mode == 4) {
                modifiers.ArmorPenetration += 10f;
            }
            else if (mode == 5) {
                modifiers.ScalingArmorPenetration += 0.5f;
            }
        }

        public override void OnHitPvp(Player player, Player target, Player.HurtInfo hurtInfo) {

            if (player != Main.LocalPlayer) {
                return;
            }

            if (mode == 6) {

                target.AddBuff(ModContent.BuffType<ExampleDefenseDebuff>(), 600);
            }

        }
    }
}
