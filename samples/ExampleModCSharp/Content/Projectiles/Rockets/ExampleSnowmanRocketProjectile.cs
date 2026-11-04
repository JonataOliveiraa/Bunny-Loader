using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace ExampleMod.Content.Projectiles.Rockets
{
    public class ExampleSnowmanRocketProjectile : ModProjectile
    {
        public override void SetStaticDefaults() {
            ProjectileID.Sets.IsARocketThatDealsDoubleDamageToPrimaryEnemy[Type] = true;
            ProjectileID.Sets.CultistIsResistantTo[Type] = true;
            ProjectileID.Sets.RocketsSkipDamageForPlayers[Type] = true;

            ProjectileID.Sets.Explosive[Type] = true;
        }
        public override void SetDefaults() {
            Projectile.width = 14;
            Projectile.height = 14;
            Projectile.friendly = true;
            Projectile.penetrate = -1;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.scale = 0.9f;

        }
        public override void AI() {

            if (Projectile.owner == Main.myPlayer && Projectile.timeLeft <= 3) {
                PrepareBombToBlow();
            }
            else {
                Projectile.localAI[1]++;

                if (Projectile.localAI[1] > 6f) {
                    Projectile.alpha = 0;
                }
                else {

                    Projectile.alpha = (int)(255f - 42f * Projectile.localAI[1]) + 100;
                    if (Projectile.alpha > 255) {
                        Projectile.alpha = 255;
                    }
                }

                for (int i = 0; i < 2; i++) {

                    if (!(Projectile.localAI[1] > 9f)) {
                        continue;
                    }

                    float velocityXAdder = 0f;
                    float velocityYAdder = 0f;
                    if (i == 1) {
                        velocityXAdder = Projectile.velocity.X * 0.5f;
                        velocityYAdder = Projectile.velocity.Y * 0.5f;
                    }

                    if (Main.rand.NextBool(2)) {
                        Dust fireDust = Dust.NewDustDirect(new Vector2(Projectile.position.X + 3f + velocityXAdder, Projectile.position.Y + 3f + velocityYAdder) - Projectile.velocity * 0.5f, Projectile.width - 8, Projectile.height - 8, DustID.Torch, 0f, 0f, 100);
                        fireDust.scale *= 1.4f + Main.rand.Next(10) * 0.1f;
                        fireDust.velocity *= 0.2f;
                        fireDust.noGravity = true;

                    }

                    if (Main.rand.NextBool(2)) {
                        Dust smokeDust = Dust.NewDustDirect(new Vector2(Projectile.position.X + 3f + velocityXAdder, Projectile.position.Y + 3f + velocityYAdder) - Projectile.velocity * 0.5f, Projectile.width - 8, Projectile.height - 8, DustID.Smoke, 0f, 0f, 100, default, 0.5f);
                        smokeDust.fadeIn = 0.5f + Main.rand.Next(5) * 0.1f;
                        smokeDust.velocity *= 0.05f;
                    }
                }

                float projDestinationX = Projectile.position.X;
                float projDestinationY = Projectile.position.Y;
                float maxHomingDistance = 600f;

                bool isHoming = false;
                Projectile.ai[0]++;

                if (Projectile.ai[0] > 15f) {
                    Projectile.ai[0] = 15f;

                    for (int i = 0; i < Main.maxNPCs; i++) {
                        NPC searchNPC = Main.npc[i];

                        if (searchNPC.CanBeChasedBy(this)) {

                            float targetPosX = searchNPC.position.X + (searchNPC.width / 2);
                            float targetPosY = searchNPC.position.Y + (searchNPC.height / 2);

                            float distanceFromProjToTarget = Math.Abs(Projectile.position.X + (Projectile.width / 2) - targetPosX) + Math.Abs(Projectile.position.Y + (Projectile.height / 2) - targetPosY);

                            if (distanceFromProjToTarget < maxHomingDistance && Collision.CanHit(Projectile.position, Projectile.width, Projectile.height, searchNPC.position, searchNPC.width, searchNPC.height)) {
                                maxHomingDistance = distanceFromProjToTarget;
                                projDestinationX = targetPosX;
                                projDestinationY = targetPosY;
                                isHoming = true;
                            }
                        }
                    }
                }

                if (!isHoming) {
                    projDestinationX = Projectile.position.X + (Projectile.width / 2) + Projectile.velocity.X * 100f;
                    projDestinationY = Projectile.position.Y + (Projectile.height / 2) + Projectile.velocity.Y * 100f;
                }

                float speed = 16f;

                Vector2 finalVelocity = (new Vector2(projDestinationX, projDestinationY) - Projectile.Center).SafeNormalize(-Vector2.UnitY) * speed;
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, finalVelocity, 1f / 12f);

                if (Projectile.velocity.X < 0f) {
                    Projectile.spriteDirection = -1;
                    Projectile.rotation = (float)Math.Atan2(0f - Projectile.velocity.Y, 0f - Projectile.velocity.X) - MathHelper.PiOver2;
                }
                else {
                    Projectile.spriteDirection = 1;
                    Projectile.rotation = (float)Math.Atan2(Projectile.velocity.Y, Projectile.velocity.X) + MathHelper.PiOver2;
                }
            }
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            Projectile.velocity *= 0f;
            Projectile.timeLeft = 3;
            return false;
        }

        public override void PrepareBombToBlow() {
            Projectile.tileCollide = false;
            Projectile.alpha = 255;

            Projectile.Resize(128, 128);

            Projectile.knockBack = 8f;
        }

        public override void OnKill(int timeLeft) {

            SoundEngine.PlaySound(SoundID.Item14, Projectile.position);

            Projectile.Resize(22, 22);

            for (int i = 0; i < 30; i++) {
                Dust smoke = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.Smoke, 0f, 0f, 100, default, 1.5f);
                smoke.velocity *= 1.4f;
            }

            for (int j = 0; j < 20; j++) {
                Dust fireDust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.Torch, 0f, 0f, 100, default, 3.5f);
                fireDust.noGravity = true;
                fireDust.velocity *= 7f;
                fireDust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.Torch, 0f, 0f, 100, default, 1.5f);
                fireDust.velocity *= 3f;
            }

            for (int k = 0; k < 2; k++) {
                float speedMulti = 0.4f;
                if (k == 1) {
                    speedMulti = 0.8f;
                }

                Gore smokeGore = Gore.NewGoreDirect(Projectile.GetSource_Death(), Projectile.position, default, Main.rand.Next(GoreID.Smoke1, GoreID.Smoke3 + 1));
                smokeGore.velocity *= speedMulti;
                smokeGore.velocity += Vector2.One;
                smokeGore = Gore.NewGoreDirect(Projectile.GetSource_Death(), Projectile.position, default, Main.rand.Next(GoreID.Smoke1, GoreID.Smoke3 + 1));
                smokeGore.velocity *= speedMulti;
                smokeGore.velocity.X -= 1f;
                smokeGore.velocity.Y += 1f;
                smokeGore = Gore.NewGoreDirect(Projectile.GetSource_Death(), Projectile.position, default, Main.rand.Next(GoreID.Smoke1, GoreID.Smoke3 + 1));
                smokeGore.velocity *= speedMulti;
                smokeGore.velocity.X += 1f;
                smokeGore.velocity.Y -= 1f;
                smokeGore = Gore.NewGoreDirect(Projectile.GetSource_Death(), Projectile.position, default, Main.rand.Next(GoreID.Smoke1, GoreID.Smoke3 + 1));
                smokeGore.velocity *= speedMulti;
                smokeGore.velocity -= Vector2.One;
            }

        }
    }
}
