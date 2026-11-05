using ExampleMod.Content.Buffs;
using ExampleMod.Content.Items.Accessories;
using ExampleMod.Content.Items.Weapons;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace ExampleMod.Common.Players
{
    internal class ExampleDamageModificationPlayer : ModPlayer
    {
        public float AdditiveCritDamageBonus;

        public bool exampleDodge;

        public int exampleDodgeCooldown;

        public int exampleDodgeVisualCounter;

        public bool hasAbsorbTeamDamageEffect;

        public bool defendedByAbsorbTeamDamageEffect;

        public bool exampleDefenseDebuff;

        public override void PreUpdate() {

            if (exampleDodgeCooldown > 0) {
                exampleDodgeCooldown--;
            }
        }

        public override void ResetEffects() {
            AdditiveCritDamageBonus = 0f;

            exampleDodge = false;

            hasAbsorbTeamDamageEffect = false;
            defendedByAbsorbTeamDamageEffect = false;

            exampleDefenseDebuff = false;
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            if (AdditiveCritDamageBonus > 0) {
                modifiers.CritDamage += AdditiveCritDamageBonus;
            }
        }

        public override void PostUpdateEquips() {

            if (exampleDodge && Player.HeldItem.type != ModContent.ItemType<HitModifiersShowcase>()) {
                Player.ClearBuff(ModContent.BuffType<ExampleDodgeBuff>());
            }

            exampleDodgeVisualCounter = Math.Clamp(exampleDodgeVisualCounter + (exampleDodge ? 1 : -1), 0, 30);
        }

        public override void DrawEffects(PlayerDrawSet drawInfo, ref float r, ref float g, ref float b, ref float a, ref bool fullBright) {

            if (exampleDodgeVisualCounter > 0) {
                g = Math.Max(0, g - exampleDodgeVisualCounter * 0.03f);
            }

            if (exampleDefenseDebuff) {

                g *= 0.5f;
                r *= 0.75f;
            }
        }

        public override bool ConsumableDodge(Player.HurtInfo info) {
            if (exampleDodge) {
                ExampleDodgeEffects();
                return true;
            }

            return false;
        }

        public void ExampleDodgeEffects() {
            Player.SetImmuneTimeForAllTypes(Player.longInvince ? 120 : 80);

            for (int i = 0; i < 50; i++) {
                Vector2 speed = Main.rand.NextVector2CircularEdge(1f, 1f);
                Dust d = Dust.NewDustPerfect(Player.Center + speed * 16, DustID.BlueCrystalShard, speed * 5, Scale: 1.5f);
                d.noGravity = true;
            }
            SoundEngine.PlaySound(((SoundStyle)SoundID.Shatter) with { Pitch = 0.5f });

            if (Player.whoAmI != Main.myPlayer) {
                return;
            }

            Player.ClearBuff(ModContent.BuffType<ExampleDodgeBuff>());
            exampleDodgeCooldown = 180;

            if (Main.netMode != NetmodeID.SinglePlayer) {
                SendExampleDodgeMessage(Player.whoAmI);
            }
        }

        public static void HandleExampleDodgeMessage(BinaryReader reader, int whoAmI) {
            int player = reader.ReadByte();
            if ((Main.netMode & NetmodeID.Server) != 0) {
                player = whoAmI;
            }

            if (player < 0 || player >= Main.player.Length || Main.player[player].IsNull) return;
            Main.player[player].GetModPlayer<ExampleDamageModificationPlayer>().ExampleDodgeEffects();

            if ((Main.netMode & NetmodeID.Server) != 0) {
                SendExampleDodgeMessage(player);
            }
        }

        public static void SendExampleDodgeMessage(int whoAmI) {
            ModPacket packet = ModContent.GetInstance<ExampleMod>().GetPacket();
            packet.Write((byte)ExampleMod.MessageType.ExampleDodge);
            packet.Write((byte)whoAmI);
            packet.Send(ignoreClient: whoAmI);
        }

        public override void ModifyHurt(ref Player.HurtModifiers modifiers) {
            if (defendedByAbsorbTeamDamageEffect && Player == Main.LocalPlayer && TeammateCanAbsorbDamage()) {
                modifiers.FinalDamage *= 1f - AbsorbTeamDamageAccessory.DamageAbsorptionMultiplier;
            }
        }

        public override void OnHurt(Player.HurtInfo info) {

            Player localPlayer = Main.LocalPlayer;
            if (defendedByAbsorbTeamDamageEffect && Player != localPlayer && IsClosestShieldWearerInRange(localPlayer, Player.Center, Player.team)) {

                float percent = AbsorbTeamDamageAccessory.DamageAbsorptionMultiplier;
                int damage = (int)(info.Damage * (percent / (1 - percent)));

                if (damage > 0) {
                    localPlayer.Hurt(PlayerDeathReason.ByOther(16), damage, 0);
                }
            }
        }

        private bool TeammateCanAbsorbDamage() {
            foreach (var otherPlayer in Main.ActivePlayers) {
                if (otherPlayer.whoAmI != Main.myPlayer && IsAbleToAbsorbDamageForTeammate(otherPlayer, Player.team)) {
                    return true;
                }
            }
            return false;
        }

        private static bool IsAbleToAbsorbDamageForTeammate(Player player, int team) {
            return player.active
                && !player.dead
                && !player.immune
                && player.GetModPlayer<ExampleDamageModificationPlayer>().hasAbsorbTeamDamageEffect
                && player.team == team
                && player.statLife > player.statLifeMax2 * AbsorbTeamDamageAccessory.DamageAbsorptionAbilityLifeThreshold;
        }

        private static bool IsClosestShieldWearerInRange(Player player, Vector2 target, int team) {
            if (!IsAbleToAbsorbDamageForTeammate(player, team)) {
                return false;
            }

            float distance = player.Distance(target);
            if (distance > AbsorbTeamDamageAccessory.DamageAbsorptionRange) {
                return false;
            }

            foreach (var otherPlayer in Main.ActivePlayers) {
                if (otherPlayer.whoAmI != Main.myPlayer && IsAbleToAbsorbDamageForTeammate(otherPlayer, team)) {
                    float otherPlayerDistance = otherPlayer.Distance(target);
                    if (distance > otherPlayerDistance || (distance == otherPlayerDistance && otherPlayer.whoAmI < Main.myPlayer)) {
                        return false;
                    }
                }
            }

            return true;
        }
    }
}
