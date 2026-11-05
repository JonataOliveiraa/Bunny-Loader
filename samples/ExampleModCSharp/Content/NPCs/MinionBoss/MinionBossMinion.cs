using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework;
using System.Collections.Generic;
using Terraria;
using Terraria.GameContent.Bestiary;
using Terraria.ID;
using Terraria.ModLoader;

namespace ExampleMod.Content.NPCs.MinionBoss
{
    public class MinionBossMinion : ModNPC
    {
        public int ParentIndex {
            get => (int)NPC.ai[0] - 1;
            set => NPC.ai[0] = value + 1;
        }

        public bool HasParent => ParentIndex > -1;

        public float PositionOffset {
            get => NPC.ai[1];
            set => NPC.ai[1] = value;
        }

        public const float RotationTimerMax = 360;
        public ref float RotationTimer => ref NPC.ai[2];

        public static int BodyType() {
            return ModContent.NPCType<MinionBossBody>();
        }

        public override void SetStaticDefaults() {
            Main.npcFrameCount[Type] = 1;

            NPCID.Sets.DontDoHardmodeScaling[Type] = true;

            NPCID.Sets.CantTakeLunchMoney[Type] = true;

            NPCID.Sets.BossBestiaryPriority.Add(Type);

            NPCID.Sets.SpecificDebuffImmunity[Type][BuffID.Poisoned] = true;
            NPCID.Sets.SpecificDebuffImmunity[Type][BuffID.Confused] = true;
        }

        public override void SetDefaults() {
            NPC.width = 30;
            NPC.height = 30;
            NPC.damage = 7;
            NPC.defense = 0;
            NPC.lifeMax = 50;
            NPC.HitSound = SoundID.NPCHit9;
            NPC.DeathSound = SoundID.NPCDeath11;
            NPC.noGravity = true;
            NPC.noTileCollide = true;
            NPC.knockBackResist = 0.8f;
            NPC.alpha = 255;
            NPC.netAlways = true;

            NPC.aiStyle = -1;
        }

        public override void SetBestiary(BestiaryDatabase database, BestiaryEntry bestiaryEntry) {
            int associatedNPCType = BodyType();
            bestiaryEntry.UIInfoProvider = CommonEnemyUICollectionInfoProvider.New(ContentSamples.BestiaryCreditId(associatedNPCType), true);

            bestiaryEntry.Info.AddRange(new List<IBestiaryInfoElement> {
                MoonLordPortraitBackgroundProviderBestiaryInfoElement.New(),
                new FlavorTextBestiaryInfoElement("Mods.ExampleMod.Bestiary.MinionBossMinion")
            });
        }

        public override Color? GetAlpha(Color drawColor) {
            if (NPC.IsABestiaryIconDummy) {
                return NPC.GetBestiaryEntryColor();
            }
            return Color.White * NPC.Opacity;
        }

        public override bool CanHitPlayer(Player target, ref int cooldownSlot) {
            cooldownSlot = ImmunityCooldownID.BossNoCheese;
            return true;
        }

        public override void OnKill() {
            Player closestPlayer = Main.player[Player.FindClosest(NPC.position, NPC.width, NPC.height)];

            if (Main.rand.NextBool(2) && closestPlayer.statLife < closestPlayer.statLifeMax2) {
                Item.NewItem(NPC.GetSource_Loot(), NPC.getRect(), ItemID.Heart);
            }
        }

        public override void HitEffect(NPC.HitInfo hit) {
            if (NPC.life <= 0) {
                int dustType = 59;

                for (int i = 0; i < 20; i++) {
                    Vector2 velocity = NPC.velocity + new Vector2(Main.rand.NextFloat(-2f, 2f), Main.rand.NextFloat(-2f, 2f));
                    Dust dust = Dust.NewDustPerfect(NPC.Center, dustType, velocity, 26, Color.White, Main.rand.NextFloat(1.5f, 2.4f));

                    dust.noLight = true;
                    dust.noGravity = true;
                    dust.fadeIn = Main.rand.NextFloat(0.3f, 0.8f);
                }
            }
        }

        public override void AI() {
            if (Despawn()) {
                return;
            }

            FadeIn();

            MoveInFormation();
        }

        private bool Despawn() {
            if (Main.netMode != NetmodeID.MultiplayerClient &&
                (!HasParent || !Main.npc[ParentIndex].active || Main.npc[ParentIndex].type != BodyType())) {
                NPC.active = false;
                NPC.life = 0;
                NetMessage.SendData(MessageID.SyncNPC, number: NPC.whoAmI);
                return true;
            }
            return false;
        }

        private void FadeIn() {
            if (NPC.alpha > 0) {
                NPC.alpha -= 10;
                if (NPC.alpha < 0) {
                    NPC.alpha = 0;
                }
            }
        }

        private void MoveInFormation() {
            NPC parentNPC = Main.npc[ParentIndex];

            float rad = (float)PositionOffset * MathHelper.TwoPi;

            RotationTimer += 0.5f;
            if (RotationTimer > RotationTimerMax) {
                RotationTimer = 0;
            }

            float continuousRotation = MathHelper.ToRadians(RotationTimer);
            rad += continuousRotation;
            if (rad > MathHelper.TwoPi) {
                rad -= MathHelper.TwoPi;
            }
            else if (rad < 0) {
                rad += MathHelper.TwoPi;
            }

            float distanceFromBody = parentNPC.width + NPC.width;

            Vector2 offset = Vector2.One.RotatedBy(rad) * distanceFromBody;

            Vector2 destination = parentNPC.Center + offset;
            Vector2 toDestination = destination - NPC.Center;
            Vector2 toDestinationNormalized = toDestination.SafeNormalize(Vector2.Zero);

            float speed = 8f;
            float inertia = 20;

            Vector2 moveTo = toDestinationNormalized * speed;
            NPC.velocity = (NPC.velocity * (inertia - 1) + moveTo) / inertia;
        }
    }
}
