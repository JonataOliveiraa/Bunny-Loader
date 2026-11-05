using ExampleMod.Content.Dusts;
using ExampleMod.Content.EmoteBubbles;
using ExampleMod.Content.Items;
using ExampleMod.Content.Items.Weapons;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.GameContent;
using Terraria.GameContent.Bestiary;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.Utilities;

namespace ExampleMod.Content.NPCs
{
    public class ExampleBoneMerchant : ModNPC
    {
        private static Profiles.StackedNPCProfile NPCProfile;
        private static Asset<Texture2D> shimmerGun;

        public override void Load() {
            shimmerGun = ModContent.Request<Texture2D>(Texture + "_Shimmer_Gun");
        }

        public override void SetStaticDefaults() {
            Main.npcFrameCount[Type] = 25;

            NPCID.Sets.ExtraFramesCount[Type] = 9;
            NPCID.Sets.AttackFrameCount[Type] = 4;
            NPCID.Sets.DangerDetectRange[Type] = 700;
            NPCID.Sets.PrettySafe[Type] = 300;
            NPCID.Sets.AttackType[Type] = 1;
            NPCID.Sets.AttackTime[Type] = 60;
            NPCID.Sets.AttackAverageChance[Type] = 30;
            NPCID.Sets.HatOffsetY[Type] = 4;
            NPCID.Sets.ShimmerTownTransform[Type] = true;

            NPCID.Sets.ActsLikeTownNPC[Type] = true;

            NPCID.Sets.NoTownNPCHappiness[Type] = true;

            NPCID.Sets.SpawnsWithCustomName[Type] = true;

            NPCID.Sets.FaceEmote[Type] = ModContent.EmoteBubbleType<ExampleBoneMerchantEmote>();

            NPCID.Sets.NPCBestiaryDrawModifiers drawModifiers = new NPCID.Sets.NPCBestiaryDrawModifiers() {
                Velocity = 1f,
                Direction = 1
            };

            NPCID.Sets.NPCBestiaryDrawOffset.Add(Type, drawModifiers);

            NPCProfile = new Profiles.StackedNPCProfile(
                new Profiles.DefaultNPCProfile(Texture, -1),
                new Profiles.DefaultNPCProfile(Texture + "_Shimmer", -1)
            );

            NPCID.Sets.NPCPortraits.Add(Type, NPCID.Sets.PrioritizedPortrait()
                .With(NPCID.Sets.ShimmeredPortraitCondition, NPCID.Sets.BasicPortrait($"{Texture}_Shimmer_Portrait"))
                .Default(NPCID.Sets.BasicPortrait($"{Texture}_Portrait")));
            NPCID.Sets.NPCPortraitsCloseUpOffsets.Add(Type, new Vector2(-3f, 0f));
        }

        public override void SetDefaults() {
            NPC.friendly = true;
            NPC.width = 18;
            NPC.height = 40;
            NPC.aiStyle = NPCAIStyleID.Passive;
            NPC.damage = 10;
            NPC.defense = 15;
            NPC.lifeMax = 250;
            NPC.HitSound = SoundID.NPCHit1;
            NPC.DeathSound = SoundID.NPCDeath1;
            NPC.knockBackResist = 0.5f;

            AnimationType = NPCID.Guide;
        }

        public override bool CanChat() {
            return true;
        }

        public override void SetBestiary(BestiaryDatabase database, BestiaryEntry bestiaryEntry) {
            bestiaryEntry.Info.AddRange([

                BestiaryDatabaseNPCsPopulator.CommonTags.SpawnConditions.Biomes.Underground,

                new FlavorTextBestiaryInfoElement("Mods.ExampleMod.Bestiary.ExampleBoneMerchant_1"),

                new FlavorTextBestiaryInfoElement("Mods.ExampleMod.Bestiary.ExampleBoneMerchant_2")
            ]);
        }

        public override void HitEffect(NPC.HitInfo hit) {
            int num = NPC.life > 0 ? 1 : 5;

            for (int k = 0; k < num; k++) {
                Dust.NewDust(NPC.position, NPC.width, NPC.height, ModContent.DustType<Sparkle>());
            }

            if (Main.netMode != NetmodeID.Server && NPC.life <= 0) {
                string variant = "";
                if (NPC.IsShimmerVariant)
                    variant += "_Shimmer";
                int headGore = Mod.Find<ModGore>($"{Name}_Gore{variant}_Head").Type;
                int armGore = Mod.Find<ModGore>($"{Name}_Gore{variant}_Arm").Type;
                int legGore = Mod.Find<ModGore>($"{Name}_Gore{variant}_Leg").Type;

                Gore.NewGore(NPC.GetSource_Death(), NPC.position, NPC.velocity, headGore, 1f);
                Gore.NewGore(NPC.GetSource_Death(), NPC.position + new Vector2(0, 20), NPC.velocity, armGore);
                Gore.NewGore(NPC.GetSource_Death(), NPC.position + new Vector2(0, 20), NPC.velocity, armGore);
                Gore.NewGore(NPC.GetSource_Death(), NPC.position + new Vector2(0, 34), NPC.velocity, legGore);
                Gore.NewGore(NPC.GetSource_Death(), NPC.position + new Vector2(0, 34), NPC.velocity, legGore);
            }
        }

        public override ITownNPCProfile TownNPCProfile() {
            return NPCProfile;
        }

        public override List<string> SetNPCNameList() {
            return new List<string> {
                "Blocky Bones",
                "Someone's Ribcage",
                "Underground Blockster",
                "Darkness"
            };
        }

        public override float SpawnChance(NPC.Spawner spawner) {
            if (spawner.Player.ZoneDirtLayerHeight && spawner.Player.inventory.Any(item => item.type == ModContent.ItemType<ExampleItem>())) {
                return 0.34f;
            }

            return 0f;
        }

        public override string GetChat() {
            return Language.SelectRandom(Lang.CreateDialogFilter("Mods.ExampleMod.Dialogue.ExampleBoneMerchant", true)).Value;
        }

        public override void RegisterChatButtons(NPCInteractionList interactions) {
            interactions.InsertBefore(NPCInteractions.Shop(), NPCInteractionDatabase.CloseButton);
        }

        public override void AddShops() {
            new NPCShop(Type)
                .Add<ExampleItem>()
                .Register();
        }

        public override void TownNPCAttackStrength(ref int damage, ref float knockback) {
            damage = 20;
            knockback = 2f;
        }

        public override void TownNPCAttackCooldown(ref int cooldown, ref int randExtraCooldown) {
            cooldown = 10;
            randExtraCooldown = 1;
        }

        public override void TownNPCAttackProj(ref int projType, ref int attackDelay) {
            projType = ProjectileID.NanoBullet;
            attackDelay = 1;

            if (NPC.localAI[3] > attackDelay) {
                attackDelay = 12;
            }
            if (NPC.localAI[3] > attackDelay) {
                attackDelay = 24;
            }
        }

        public override void TownNPCAttackProjSpeed(ref float multiplier, ref float gravityCorrection, ref float randomOffset) {
            multiplier = 10f;
            randomOffset = 0.2f;
        }

        public override void TownNPCAttackShoot(ref bool inBetweenShots) {
            if (NPC.localAI[3] > 1) {
                inBetweenShots = true;
            }
        }

        public override void DrawTownAttackGun(ref Texture2D item, ref Rectangle itemFrame, ref float scale, ref int horizontalHoldoutOffset) {
            if (!NPC.IsShimmerVariant) {
                int itemType = ModContent.ItemType<ExampleCustomAmmoGun>();
                Main.GetItemDrawFrame(itemType, out item, out itemFrame);
                horizontalHoldoutOffset = (int)Main.DrawPlayerItemPos(1f, itemType).X - 12;
            }
            else {
                item = shimmerGun.Value;
                itemFrame = item.Frame();
                horizontalHoldoutOffset = -2;
            }
        }
    }
}
