using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Utilities;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace ExampleMod.Content.Projectiles
{

    public class ActiveSoundShowcaseProjectile : ModProjectile
    {
        internal enum ActiveSoundShowcaseStyle
        {

            FireAndForget,

            FireAndForgetPlusInitialPosition,

            SyncSoundToProjectilePosition,

            SoundUpdateCallbackApproach,

            LoopedSound,

            LoopedSoundAdvanced,
        }

        private ActiveSoundShowcaseStyle Style {
            get => (ActiveSoundShowcaseStyle)Projectile.ai[0];
            set => Projectile.ai[0] = (float)value;
        }

        SlotId soundSlot;
        bool played = false;

        SoundStyle soundStyleTwister = new SoundStyle(SoundID.DD2_BookStaffTwisterLoop.Pointer) with {

        };

        SoundStyle soundStyleIgniteLoop = new SoundStyle(SoundID.DD2_KoboldIgniteLoop.Pointer) with {
            IsLooped = true,

            SoundLimitBehavior = SoundLimitBehavior.ReplaceOldest

        };

        public override void SetStaticDefaults() {
            Main.projFrames[Type] = 6;
        }

        public override void SetDefaults() {
            Projectile.width = 22;
            Projectile.height = 24;
            Projectile.penetrate = 4;
            Projectile.timeLeft = 300;
        }

        public override void OnSpawn(IEntitySource source) {
            Main.NewText($"{Style}");
        }

        public override void AI() {
            Projectile.frame = (int)Style;

            switch (Style) {
                case ActiveSoundShowcaseStyle.FireAndForget:
                    if (!played) {
                        played = true;
                        SoundEngine.PlaySound(soundStyleTwister);
                    }
                    break;
                case ActiveSoundShowcaseStyle.FireAndForgetPlusInitialPosition:
                    if (!played) {
                        played = true;
                        SoundEngine.PlaySound(soundStyleTwister, Projectile.position);
                    }
                    break;
                case ActiveSoundShowcaseStyle.SyncSoundToProjectilePosition:
                    if (!SoundEngine.TryGetActiveSound(soundSlot, out var activeSoundTwister)) {
                        soundSlot = SoundEngine.PlaySound(soundStyleTwister, Projectile.position);
                    }
                    else {

                        activeSoundTwister.Position = Projectile.position;
                    }
                    break;
                case ActiveSoundShowcaseStyle.SoundUpdateCallbackApproach:
                    if (!SoundEngine.TryGetActiveSound(soundSlot, out var _)) {
                        var tracker = ProjectileAudioTracker.New(Projectile);
                        soundSlot = SoundEngine.PlaySound(soundStyleTwister, Projectile.position, soundInstance => BasicSoundUpdateCallback(tracker, soundInstance));

                    }
                    break;
                case ActiveSoundShowcaseStyle.LoopedSound:
                    if (!SoundEngine.TryGetActiveSound(soundSlot, out var _)) {
                        var tracker = ProjectileAudioTracker.New(Projectile);
                        soundSlot = SoundEngine.PlaySound(soundStyleIgniteLoop, Projectile.position, soundInstance => {

                            soundInstance.Position = Projectile.position;
                            return tracker.IsActiveAndInGame();
                        });
                    }

                    break;
                case ActiveSoundShowcaseStyle.LoopedSoundAdvanced:
                    if (!SoundEngine.TryGetActiveSound(soundSlot, out var _)) {
                        var tracker = ProjectileAudioTracker.New(Projectile);
                        soundSlot = SoundEngine.PlaySound(soundStyleIgniteLoop, Projectile.position, soundInstance => AdvancedSoundUpdateCallback(tracker, soundInstance));
                    }
                    break;
            }
        }

        private bool BasicSoundUpdateCallback(ProjectileAudioTracker tracker, ActiveSound soundInstance) {

            soundInstance.Position = Projectile.position;

            return tracker.IsActiveAndInGame();
        }

        private bool AdvancedSoundUpdateCallback(ProjectileAudioTracker tracker, ActiveSound soundInstance) {
            soundInstance.Position = Projectile.position;

            soundInstance.Pitch = (Projectile.maxPenetrate - Projectile.penetrate) * 0.15f;

            if (Projectile.wet) {
                soundInstance.Pitch -= 0.4f;
                soundInstance.Volume = MathHelper.Clamp(new LegacySoundStyle(soundInstance.Style.Pointer).Volume - 0.4f, 0f, 1f);
            }

            return tracker.IsActiveAndInGame();
        }

        public override void OnKill(int timeLeft) {

            if (Style == ActiveSoundShowcaseStyle.SyncSoundToProjectilePosition) {
                if (SoundEngine.TryGetActiveSound(soundSlot, out var activeSound)) {
                    activeSound.Stop();
                }
            }
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {

            Projectile.penetrate--;
            if (Projectile.penetrate <= 0) {
                Projectile.Kill();
            }
            else {
                Collision.HitTiles(Projectile.position, Projectile.velocity, Projectile.width, Projectile.height);
                SoundEngine.PlaySound(SoundID.Item10, Projectile.position);

                if (Math.Abs(Projectile.velocity.X - oldVelocity.X) > float.Epsilon) {
                    Projectile.velocity.X = -oldVelocity.X;
                }

                if (Math.Abs(Projectile.velocity.Y - oldVelocity.Y) > float.Epsilon) {
                    Projectile.velocity.Y = -oldVelocity.Y;
                }
            }

            return false;
        }
    }
}
