using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System.IO;
using Terraria;
using Terraria.Chat;
using Terraria.Graphics.Renderers;
using IParticle = Terraria.Graphics.Renderers.IModParticle;
using Terraria.ID;
using Terraria.ModLoader;

namespace ExampleMod.Content.CustomModType
{

    internal class VictoryPosePlayer : ModPlayer
    {

        public ModVictoryPose activeVictoryPose;

        public int ElapsedPoseTime { get; internal set; }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {

            if (!target.active && Player.whoAmI == Main.myPlayer) {

                StartRandomPose(target.boss);
            }
        }

        private void StartRandomPose(bool boss) {

            if (!boss && Main.rand.NextBool(99, 100)) {
                return;
            }

            ModVictoryPose newPose;
            if (boss) {

                newPose = Main.rand.Next(VictoryPoseLoader.victoryPoses);
            }
            else {

                int randomNonBossVictoryPoseIndex = Main.rand.Next(VictoryPoseID.Sets.NonBoss.GetTrueIndexes());
                newPose = VictoryPoseLoader.victoryPoses[randomNonBossVictoryPoseIndex];
            }

            VictoryPoseLoader.StartPose(newPose, forced: false);
        }

        internal bool StartPose(ModVictoryPose newPose, bool forced = false) {
            if (activeVictoryPose != null) {
                if (forced) {
                    EndPose();
                }
                else {
                    return false;
                }
            }

            ElapsedPoseTime = 0;
            activeVictoryPose = newPose;
            activeVictoryPose.OnStartPose(Player);

            if (!Main.dedServ) {
                ChatHelper.DisplayMessage(activeVictoryPose.VictoryCheer.ToNetworkText(), Color.White, (byte)Player.whoAmI);
            }

            Asset<Texture2D> texture = ModContent.Request<Texture2D>(activeVictoryPose.Texture);
            Rectangle? frame = activeVictoryPose.GetTextureFrame(texture);
            Main.ParticleSystem_World_BehindPlayers.Add(new PoseIconParticle(texture, frame, new Vector2(Main.rand.NextFloat(-2, 2), -5f), Player.Center - new Vector2(0, 80), Main.rand.NextFloat(-0.03f, 0.03f)) {
                AccelerationPerFrame = new Vector2(0f, 0.16350001f),
                ScaleOffsetPerFrame = 1f / 60f,
            });

            return true;
        }

        private void StartPoseDirect(ModVictoryPose newPose) {

            StartPose(newPose, forced: true);
        }

        internal void EndPose() {
            activeVictoryPose?.OnEndPose(Player);
            activeVictoryPose = null;
            ElapsedPoseTime = -1;
        }

        public override void PostUpdate() {

            if (activeVictoryPose == null) {
                return;
            }

            activeVictoryPose.Update(Player);
            ElapsedPoseTime++;
            if (ElapsedPoseTime >= activeVictoryPose.PoseTime) {
                EndPose();
            }
        }

        public static void HandleStartVictoryPoseMessage(BinaryReader reader, int whoAmI) {
            int player = reader.ReadByte();
            if ((Main.netMode & NetmodeID.Server) != 0) {

                player = whoAmI;
            }

            int poseIndex = reader.ReadInt32();
            if (player < 0 || player >= Main.player.Length || Main.player[player].IsNull || poseIndex < 0 || poseIndex >= VictoryPoseLoader.victoryPoses.Count) return;
            ModVictoryPose pose = VictoryPoseLoader.victoryPoses[poseIndex];
            if (player != Main.myPlayer) {
                Main.player[player].GetModPlayer<VictoryPosePlayer>().StartPoseDirect(pose);
            }

            if ((Main.netMode & NetmodeID.Server) != 0) {
                SendStartVictoryPoseMessage(player, pose);
            }
        }

        public static void SendStartVictoryPoseMessage(int whoAmI, ModVictoryPose pose) {
            ModPacket packet = ModContent.GetInstance<ExampleMod>().GetPacket();
            packet.Write((byte)ExampleMod.MessageType.StartVictoryPose);
            packet.Write((byte)whoAmI);
            packet.Write(pose.Type);
            packet.Send(ignoreClient: whoAmI);
        }

        public static void HandleCancelVictoryPoseMessage(BinaryReader reader, int whoAmI) {
            int player = reader.ReadByte();
            if ((Main.netMode & NetmodeID.Server) != 0) {
                player = whoAmI;
            }

            if (player >= 0 && player < Main.player.Length && !Main.player[player].IsNull && player != Main.myPlayer) {
                Main.player[player].GetModPlayer<VictoryPosePlayer>().EndPose();
            }

            if ((Main.netMode & NetmodeID.Server) != 0) {
                SendCancelVictoryPoseMessage(player);
            }
        }

        public static void SendCancelVictoryPoseMessage(int whoAmI) {
            ModPacket packet = ModContent.GetInstance<ExampleMod>().GetPacket();
            packet.Write((byte)ExampleMod.MessageType.CancelVictoryPose);
            packet.Write((byte)whoAmI);
            packet.Send(ignoreClient: whoAmI);
        }
    }

    public class PoseIconParticle : IParticle
    {
        public Vector2 AccelerationPerFrame;
        public Vector2 Velocity;
        public float rotationRate;
        public Vector2 LocalPosition;
        public float ScaleOffsetPerFrame;
        public float StopWhenAboveXScale;
        private Asset<Texture2D> texture;
        private Rectangle frame;
        private Vector2 origin;
        private float scale;
        private float rotation;

        public bool ShouldBeRemovedFromRenderer { get; private set; }

        public PoseIconParticle(Asset<Texture2D> textureAsset, Rectangle? frame, Vector2 initialVelocity, Vector2 initialLocalPosition, float rotationRate) {
            texture = textureAsset;
            this.frame = frame ?? texture.Frame();
            origin = this.frame.Size() / 2f;
            Velocity = initialVelocity;
            LocalPosition = initialLocalPosition;
            StopWhenAboveXScale = 3f;
            ShouldBeRemovedFromRenderer = false;
            scale = 0.6f;
            this.rotationRate = rotationRate;
        }

        public void Update(ref ParticleRendererSettings settings) {
            Velocity += AccelerationPerFrame;
            LocalPosition += Velocity;
            scale += ScaleOffsetPerFrame;
            rotation += rotationRate;
            if (scale >= StopWhenAboveXScale) {
                ShouldBeRemovedFromRenderer = true;
            }
        }

        public void Draw(ref ParticleRendererSettings settings, SpriteBatch spriteBatch) {
            Color color = Color.White * Utils.Clamp(scale, 0, 1f);
            spriteBatch.Draw(texture.Value, settings.AnchorPosition + LocalPosition, frame, color, rotation, origin, scale, SpriteEffects.None, 0f);
        }
    }
}
