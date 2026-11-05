using Microsoft.Xna.Framework.Graphics;
using ExampleMod.Common.Systems;
using ExampleMod.Content.BossBars;
using ExampleMod.Content.Items;
using ExampleMod.Content.Items.Armor.Vanity;
using ExampleMod.Content.Items.Consumables;
using ExampleMod.Content.Pets.MinionBossPet;
using ExampleMod.Content.Projectiles;
using ExampleMod.Content.Tiles;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent.Bestiary;
using Terraria.GameContent.ItemDropRules;
using Terraria.Graphics.CameraModifiers;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace ExampleMod.Content.NPCs.MinionBoss
{
    [AutoloadBossHead]
    public class MinionBossBody : ModNPC
    {
        public static int secondStageHeadSlot = -1;

        public bool SecondStage {
            get => NPC.ai[0] == 1f;
            set => NPC.ai[0] = value ? 1f : 0f;
        }

        public Vector2 FirstStageDestination {
            get => new Vector2(NPC.ai[1], NPC.ai[2]);
            set {
                NPC.ai[1] = value.X;
                NPC.ai[2] = value.Y;
            }
        }

        public int MinionMaxHealthTotal {
            get => (int)NPC.ai[3];
            set => NPC.ai[3] = value;
        }

        public int MinionHealthTotal { get; set; }

        public Vector2 LastFirstStageDestination { get; set; } = Vector2.Zero;

        public bool SpawnedMinions {
            get => NPC.localAI[0] == 1f;
            set => NPC.localAI[0] = value ? 1f : 0f;
        }

        private const int FirstStageTimerMax = 90;

        public ref float FirstStageTimer => ref NPC.localAI[1];

        public ref float SecondStageTimer_SpawnEyes => ref NPC.localAI[3];

        public static int MinionType() {
            return ModContent.NPCType<MinionBossMinion>();
        }

        public static int MinionCount() {
            int count = 15;

            if (Main.expertMode) {
                count += 5;
            }

            if (Main.getGoodWorld) {
                count += 5;
            }

            return count;
        }

        public override LocalizedText DeathMessage => Language.GetText("Announcement.HasBeenDefeated_Plural").WithFormatArgs(this.GetLocalization("BossFightName"));

        public override void Load() {
            string texture = BossHeadTexture + "_SecondStage";
            secondStageHeadSlot = Mod.AddBossHeadTexture(texture, -1);
        }

        public override void BossHeadSlot(ref int index) {
            int slot = secondStageHeadSlot;
            if (SecondStage && slot != -1) {
                index = slot;
            }
        }

        public override void SetStaticDefaults() {
            Main.npcFrameCount[Type] = 6;

            NPCID.Sets.MPAllowedEnemies[Type] = true;

            NPCID.Sets.BossBestiaryPriority.Add(Type);

            NPCID.Sets.SpecificDebuffImmunity[Type][BuffID.Poisoned] = true;
            NPCID.Sets.SpecificDebuffImmunity[Type][BuffID.Confused] = true;

            NPCID.Sets.NPCBestiaryDrawModifiers drawModifiers = new NPCID.Sets.NPCBestiaryDrawModifiers() {
                CustomTexturePath = "ExampleMod/Assets/Textures/Bestiary/MinionBoss_Preview",
                PortraitScale = 0.6f,
                PortraitPositionYOverride = 0f,
            };
            NPCID.Sets.NPCBestiaryDrawOffset.Add(Type, drawModifiers);
        }

        public override void SetDefaults() {
            NPC.width = 110;
            NPC.height = 110;
            NPC.damage = 12;
            NPC.defense = 10;
            NPC.lifeMax = 2000;
            NPC.HitSound = SoundID.NPCHit1;
            NPC.DeathSound = SoundID.NPCDeath1;
            NPC.knockBackResist = 0f;
            NPC.noGravity = true;
            NPC.noTileCollide = true;
            NPC.value = Item.buyPrice(gold: 5);
            NPC.boss = true;
            NPC.npcSlots = 10f;

            NPC.aiStyle = -1;

            NPC.BossBar = ModContent.GetInstance<MinionBossBossBar>();

            if (!Main.dedServ) {
                Music = MusicLoader.GetMusicSlot(Mod, "Assets/Music/Ropocalypse2");

                if (!Main.swapMusic == Main.drunkWorld && !Main.remixWorld) {
                    Music = MusicID.OtherworldBoss1;
                }
            }
        }

        public override void SetBestiary(BestiaryDatabase database, BestiaryEntry bestiaryEntry) {
            bestiaryEntry.Info.AddRange(new List<IBestiaryInfoElement> {
                MoonLordPortraitBackgroundProviderBestiaryInfoElement.New(),
                new FlavorTextBestiaryInfoElement("Mods.ExampleMod.Bestiary.MinionBossBody")
            });
        }

        public override void ModifyNPCLoot(NPCLoot npcLoot) {
            npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<Items.Placeable.Furniture.MinionBossTrophy>(), 10));

            LeadingConditionRule notExpertRule = new LeadingConditionRule(new Conditions.NotExpert());

            notExpertRule.OnSuccess(ItemDropRule.Common(ModContent.ItemType<MinionBossMask>(), 7));

            int itemType = ModContent.ItemType<ExampleItem>();
            var parameters = new DropOneByOne.Parameters() {
                ChanceNumerator = 1,
                ChanceDenominator = 1,
                MinimumStackPerChunkBase = 1,
                MaximumStackPerChunkBase = 1,
                MinimumItemDropsCount = 12,
                MaximumItemDropsCount = 15,
            };

            notExpertRule.OnSuccess(new DropOneByOne(itemType, parameters));

            npcLoot.Add(notExpertRule);

            npcLoot.Add(ItemDropRule.BossBag(ModContent.ItemType<MinionBossBag>()));

            npcLoot.Add(ItemDropRule.MasterModeCommonDrop(ModContent.ItemType<Items.Placeable.Furniture.MinionBossRelic>()));

            npcLoot.Add(ItemDropRule.MasterModeDropOnAllPlayers(ModContent.ItemType<MinionBossPetItem>(), 4));
        }

        public override void OnKill() {
            if (!DownedBossSystem.downedMinionBoss) {
                ModContent.GetInstance<ExampleOreSystem>().BlessWorldWithExampleOre();
            }

            NPC.SetEventFlagCleared(ref DownedBossSystem.downedMinionBoss, -1);
        }

        public override void BossLoot(ref int potionType, ref int potionStack, ref int heartStack) {
        }

        public override bool CanHitPlayer(Player target, ref int cooldownSlot) {
            cooldownSlot = ImmunityCooldownID.BossNoCheese;
            return true;
        }

        public override void FindFrame(int frameHeight) {
            int startFrame = 0;
            int finalFrame = 2;

            if (SecondStage) {
                startFrame = 3;
                finalFrame = Main.npcFrameCount[Type] - 1;

                if (NPC.frame.Y < startFrame * frameHeight) {
                    NPC.frame.Y = startFrame * frameHeight;
                }
            }

            int frameSpeed = 5;
            NPC.frameCounter += 0.5f;
            NPC.frameCounter += NPC.velocity.Length() / 10f;
            if (NPC.frameCounter > frameSpeed) {
                NPC.frameCounter = 0;
                NPC.frame.Y += frameHeight;

                if (NPC.frame.Y > finalFrame * frameHeight) {
                    NPC.frame.Y = startFrame * frameHeight;
                }
            }
        }

        public override void HitEffect(NPC.HitInfo hit) {
            if ((Main.netMode & NetmodeID.Server) != 0) {
                return;
            }

            if (NPC.life <= 0) {
                int backGoreType = Mod.Find<ModGore>("MinionBossBody_Back").Type;
                int frontGoreType = Mod.Find<ModGore>("MinionBossBody_Front").Type;

                var entitySource = NPC.GetSource_Death();

                for (int i = 0; i < 2; i++) {
                    Gore.NewGore(entitySource, NPC.position, new Vector2(Main.rand.Next(-6, 7), Main.rand.Next(-6, 7)), backGoreType);
                    Gore.NewGore(entitySource, NPC.position, new Vector2(Main.rand.Next(-6, 7), Main.rand.Next(-6, 7)), frontGoreType);
                }

                SoundEngine.PlaySound(SoundID.Roar, NPC.Center);

                var modifier = Terraria.Graphics.CameraModifiers.PunchCameraModifier.New(NPC.Center, (Main.rand.NextFloat() * ((float)Math.PI * 2f)).ToRotationVector2(), 20f, 6f, 20, 1000f, FullName);
                Main.instance.CameraModifiers.Add(modifier);
            }
        }

        public override void AI() {
            if (NPC.target < 0 || NPC.target == 255 || Main.player[NPC.target].dead || !Main.player[NPC.target].active) {
                NPC.TargetClosest();
            }

            Player player = Main.player[NPC.target];

            if (player.dead) {
                NPC.velocity.Y -= 0.04f;

                NPC.EncourageDespawn(10);
                return;
            }

            SpawnMinions();

            CheckSecondStage();

            NPC.dontTakeDamage = !SecondStage;

            if (SecondStage) {
                DoSecondStage(player);
            }
            else {
                DoFirstStage(player);
            }
        }

        private void SpawnMinions() {
            if (SpawnedMinions) {
                return;
            }

            SpawnedMinions = true;

            if ((Main.netMode & NetmodeID.MultiplayerClient) != 0) {
                return;
            }

            int count = MinionCount();
            var entitySource = NPC.GetSource_FromAI();

            MinionMaxHealthTotal = 0;
            for (int i = 0; i < count; i++) {
                NPC minionNPC = NPC.NewNPCDirect(entitySource, (int)NPC.Center.X, (int)NPC.Center.Y, ModContent.NPCType<MinionBossMinion>(), NPC.whoAmI);
                if (minionNPC.whoAmI == Main.maxNPCs)
                    continue;

                MinionBossMinion minion = (MinionBossMinion)minionNPC.ModNPC;
                minion.ParentIndex = NPC.whoAmI;
                minion.PositionOffset = i / (float)count;

                MinionMaxHealthTotal += minionNPC.lifeMax;

                if ((Main.netMode & NetmodeID.Server) != 0) {
                    NetMessage.SendData(MessageID.SyncNPC, number: minionNPC.whoAmI);
                }
            }

            if ((Main.netMode & NetmodeID.Server) != 0) {
                NetMessage.SendData(MessageID.SyncNPC, number: NPC.whoAmI);
            }
        }

        private void CheckSecondStage() {
            MinionHealthTotal = 0;
            if (SecondStage) {
                return;
            }

            foreach (var otherNPC in Main.ActiveNPCs) {
                if (otherNPC.type == MinionType() && otherNPC.ModNPC is MinionBossMinion minion) {
                    if (minion.ParentIndex == NPC.whoAmI) {
                        MinionHealthTotal += otherNPC.life;
                    }
                }
            }

            if (MinionHealthTotal <= 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                SecondStage = true;
                NPC.netUpdate = true;
            }
        }

        private void DoFirstStage(Player player) {
            FirstStageTimer++;
            if (FirstStageTimer > FirstStageTimerMax) {
                FirstStageTimer = 0;
            }

            float distance = 200;

            if (FirstStageTimer == 0) {
                Vector2 fromPlayer = NPC.Center - player.Center;

                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    float angle = fromPlayer.ToRotation();
                    float twelfth = MathHelper.Pi / 6;

                    angle += MathHelper.Pi + Main.rand.NextFloat(-twelfth, twelfth);
                    if (angle > MathHelper.TwoPi) {
                        angle -= MathHelper.TwoPi;
                    }
                    else if (angle < 0) {
                        angle += MathHelper.TwoPi;
                    }

                    Vector2 relativeDestination = angle.ToRotationVector2() * distance;

                    FirstStageDestination = player.Center + relativeDestination;
                    NPC.netUpdate = true;
                }
            }

            Vector2 toDestination = FirstStageDestination - NPC.Center;
            Vector2 toDestinationNormalized = toDestination.SafeNormalize(Vector2.UnitY);
            float speed = Math.Min(distance, toDestination.Length());
            NPC.velocity = toDestinationNormalized * speed / 30;

            if (FirstStageDestination != LastFirstStageDestination) {
                NPC.TargetClosest();

                if (Main.netMode != NetmodeID.Server) {
                    NPC.position += NPC.netOffset;

                    Dust.QuickDustLine(NPC.Center + toDestinationNormalized * NPC.width, FirstStageDestination, toDestination.Length() / 20f, Color.Yellow);

                    NPC.position -= NPC.netOffset;
                }
            }
            LastFirstStageDestination = FirstStageDestination;

            NPC.damage = 0;

            float remainingShields = MinionHealthTotal / (float)MinionMaxHealthTotal;
            NPC.alpha = (int)(remainingShields * 255);

            NPC.rotation = NPC.velocity.ToRotation() - MathHelper.PiOver2;
        }

        private void DoSecondStage(Player player) {
            if (NPC.life < NPC.lifeMax * 0.5f) {
                ApplySecondStageBuffImmunities();
            }

            Vector2 toPlayer = player.Center - NPC.Center;

            float offsetX = 200f;

            Vector2 abovePlayer = player.Top + new Vector2(NPC.direction * offsetX, -NPC.height);

            Vector2 toAbovePlayer = abovePlayer - NPC.Center;
            Vector2 toAbovePlayerNormalized = toAbovePlayer.SafeNormalize(Vector2.UnitY);

            float changeDirOffset = offsetX * 0.7f;

            if (NPC.direction == -1 && NPC.Center.X - changeDirOffset < abovePlayer.X ||
                NPC.direction == 1 && NPC.Center.X + changeDirOffset > abovePlayer.X) {
                NPC.direction *= -1;
            }

            float speed = 8f;
            float inertia = 40f;

            if (NPC.Top.Y > player.Bottom.Y) {
                speed = 12f;
            }

            Vector2 moveTo = toAbovePlayerNormalized * speed;
            NPC.velocity = (NPC.velocity * (inertia - 1) + moveTo) / inertia;

            DoSecondStage_SpawnEyes(player);

            NPC.damage = NPC.defDamage;

            NPC.alpha = 0;

            NPC.rotation = toPlayer.ToRotation() - MathHelper.PiOver2;
        }

        private void DoSecondStage_SpawnEyes(Player player) {
            float timerMax = Utils.Clamp((float)NPC.life / NPC.lifeMax, 0.33f, 1f) * 90;

            SecondStageTimer_SpawnEyes++;
            if (SecondStageTimer_SpawnEyes > timerMax) {
                SecondStageTimer_SpawnEyes = 0;
            }

            if (NPC.HasValidTarget && SecondStageTimer_SpawnEyes == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                float kitingOffsetX = Utils.Clamp(player.velocity.X * 16, -100, 100);
                Vector2 position = player.Bottom + new Vector2(kitingOffsetX + Main.rand.Next(-100, 100), Main.rand.Next(50, 100));

                int type = ModContent.ProjectileType<MinionBossEye>();
                int damage = NPC.damage / 2;
                var entitySource = NPC.GetSource_FromAI();

                Projectile.NewProjectile(entitySource, position, -Vector2.UnitY, type, damage, 0f, Main.myPlayer);
            }
        }

        private void ApplySecondStageBuffImmunities() {
            if (NPC.buffImmune[BuffID.OnFire]) {
                return;
            }

            NPC.BecomeImmuneTo(BuffID.OnFire);

            if (Main.netMode != NetmodeID.MultiplayerClient) {
                NPC.ClearImmuneToBuffs(out bool anyBuffsCleared);

                if (anyBuffsCleared) {
                }
            }

            for (int loops = 0; loops < 2; loops++) {
                for (int i = 0; i < 50; i++) {
                    Vector2 speed = Main.rand.NextVector2CircularEdge(1f, 1f);
                    Dust d = Dust.NewDustPerfect(NPC.Center, DustID.BlueCrystalShard, speed * 10 * (loops + 1), Scale: 1.5f);
                    d.noGravity = true;
                }
            }
        }
    }
}
