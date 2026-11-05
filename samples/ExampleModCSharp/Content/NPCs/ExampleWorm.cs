using ExampleMod.Content.Items.Placeable.Banners;
using ExampleMod.NPCs;
using Microsoft.Xna.Framework;
using System.IO;
using Terraria;
using Terraria.GameContent.Bestiary;
using Terraria.ID;
using Terraria.ModLoader;

namespace ExampleMod.Content.NPCs
{
    internal class ExampleWormHead : WormHead
    {
        public override int BodyType => ModContent.NPCType<ExampleWormBody>();

        public override int TailType => ModContent.NPCType<ExampleWormTail>();

        public override void SetStaticDefaults() {
            var drawModifier = new NPCID.Sets.NPCBestiaryDrawModifiers() {
                CustomTexturePath = "ExampleMod/Content/NPCs/ExampleWorm_Bestiary",
                Position = new Vector2(40f, 24f),
                PortraitPositionXOverride = 0f,
                PortraitPositionYOverride = 12f
            };
            NPCID.Sets.NPCBestiaryDrawOffset.Add(NPC.type, drawModifier);
        }

        public override void SetDefaults() {
            NPC.CloneDefaults(NPCID.DiggerHead);
            NPC.aiStyle = -1;

            Banner = Type;

            BannerItem = ModContent.ItemType<ExampleWormHeadBanner>();
            ItemID.Sets.KillsToBanner[BannerItem] = 25;
        }

        public override void SetBestiary(BestiaryDatabase database, BestiaryEntry bestiaryEntry) {
            bestiaryEntry.Info.AddRange([

                BestiaryDatabaseNPCsPopulator.CommonTags.SpawnConditions.Biomes.Underground,
                BestiaryDatabaseNPCsPopulator.CommonTags.SpawnConditions.Biomes.Caverns,

                new FlavorTextBestiaryInfoElement("Mods.ExampleMod.Bestiary.ExampleWormHead")
            ]);
        }

        public override void Init() {
            MinSegmentLength = 6;
            MaxSegmentLength = 12;

            CommonWormInit(this);
        }

        internal static void CommonWormInit(Worm worm) {
            worm.MoveSpeed = 5.5f;
            worm.Acceleration = 0.045f;
        }

        private int attackCounter;
        public override void SendExtraAI(BinaryWriter writer) {
            writer.Write(attackCounter);
        }

        public override void ReceiveExtraAI(BinaryReader reader) {
            attackCounter = reader.ReadInt32();
        }

        public override void AI() {
            if (Main.netMode != NetmodeID.MultiplayerClient) {
                if (attackCounter > 0) {
                    attackCounter--;
                }

                Player target = Main.player[NPC.target];

                if (attackCounter <= 0 && Vector2.Distance(NPC.Center, target.Center) < 200 && Collision.CanHit(NPC.Center, 1, 1, target.Center, 1, 1)) {
                    Vector2 direction = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitX);
                    direction = direction.RotatedByRandom(MathHelper.ToRadians(10));

                    int projectile = Projectile.NewProjectile(NPC.GetSource_FromThis(), NPC.Center, direction * 1, ProjectileID.ShadowBeamHostile, 5, 0, Main.myPlayer);
                    Main.projectile[projectile].timeLeft = 300;
                    attackCounter = 500;
                    NPC.netUpdate = true;
                }
            }
        }
    }

    internal class ExampleWormBody : WormBody
    {
        public override void SetStaticDefaults() {
            NPCID.Sets.NPCBestiaryDrawModifiers value = new NPCID.Sets.NPCBestiaryDrawModifiers() {
                Hide = true
            };
            NPCID.Sets.NPCBestiaryDrawOffset.Add(Type, value);
            new BunnyLoader.Interop.GameDictionary(NPCID.Sets.RespawnEnemyID).Set<int, int>(Type, ModContent.NPCType<ExampleWormHead>());
        }

        public override void SetDefaults() {
            NPC.CloneDefaults(NPCID.DiggerBody);
            NPC.aiStyle = -1;

            Banner = ModContent.NPCType<ExampleWormHead>();
        }

        public override void Init() {
            ExampleWormHead.CommonWormInit(this);
        }
    }

    internal class ExampleWormTail : WormTail
    {
        public override void SetStaticDefaults() {
            NPCID.Sets.NPCBestiaryDrawModifiers value = new NPCID.Sets.NPCBestiaryDrawModifiers() {
                Hide = true
            };
            NPCID.Sets.NPCBestiaryDrawOffset.Add(Type, value);
            new BunnyLoader.Interop.GameDictionary(NPCID.Sets.RespawnEnemyID).Set<int, int>(Type, ModContent.NPCType<ExampleWormHead>());
        }

        public override void SetDefaults() {
            NPC.CloneDefaults(NPCID.DiggerTail);
            NPC.aiStyle = -1;

            Banner = ModContent.NPCType<ExampleWormHead>();
        }

        public override void Init() {
            ExampleWormHead.CommonWormInit(this);
        }
    }
}
