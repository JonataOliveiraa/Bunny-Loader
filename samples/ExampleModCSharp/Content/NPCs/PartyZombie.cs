using ExampleMod.Content.Biomes;
using ExampleMod.Content.Buffs;
using Terraria;
using Terraria.GameContent;
using Terraria.GameContent.Bestiary;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.Utilities;

namespace ExampleMod.Content.NPCs
{
    public class PartyZombie : ModNPC
    {
        public override void SetStaticDefaults() {
            Main.npcFrameCount[Type] = Main.npcFrameCount[NPCID.Zombie];

            NPCID.Sets.ShimmerTransformToNPC[Type] = NPCID.Skeleton;

            NPCID.Sets.NPCBestiaryDrawModifiers value = new NPCID.Sets.NPCBestiaryDrawModifiers() {
                Velocity = 1f
            };
            NPCID.Sets.NPCBestiaryDrawOffset.Add(Type, value);
        }

        public override void SetDefaults() {
            NPC.width = 18;
            NPC.height = 40;
            NPC.damage = 14;
            NPC.defense = 6;
            NPC.lifeMax = 200;
            NPC.HitSound = SoundID.NPCHit1;
            NPC.DeathSound = SoundID.NPCDeath2;
            NPC.value = 60f;
            NPC.knockBackResist = 0.5f;
            NPC.aiStyle = NPCAIStyleID.Fighter;

            AIType = NPCID.Zombie;
            AnimationType = NPCID.Zombie;
            Banner = BannerSystem.NPCtoBanner(NPCID.Zombie);
            BannerItem = BannerSystem.BannerToItem(Banner);
            SpawnModBiomes = [ModContent.GetInstance<ExampleSurfaceBiome>().Type];
        }

        public override void ModifyNPCLoot(NPCLoot npcLoot) {
            var zombieDropRules = Main.ItemDropsDB.GetRulesForNPCID(NPCID.Zombie, false);
            foreach (var zombieDropRule in zombieDropRules) {
                npcLoot.Add(zombieDropRule);
            }

            npcLoot.Add(ItemDropRule.Common(ItemID.Confetti, 100));
        }

        public override float SpawnChance(NPC.Spawner spawner) {
            return SpawnCondition.OverworldNightMonster.Chance * 0.2f;
        }

        public override void AI() {
            if (NPC.wet) {
                if (NPC.honeyWet) {
                    NPC.GravityMultiplier /= NPC.GravityWetMultipliers[LiquidID.Honey];
                    NPC.MaxFallSpeedMultiplier /= NPC.MaxFallSpeedWetMultipliers[LiquidID.Honey];
                }
                else if (!NPC.lavaWet && !NPC.shimmerWet) {
                    NPC.GravityMultiplier *= NPC.GravityWetMultipliers[LiquidID.Honey] / NPC.GravityWetMultipliers[LiquidID.Water];
                    NPC.MaxFallSpeedMultiplier *= NPC.MaxFallSpeedWetMultipliers[LiquidID.Honey] / NPC.MaxFallSpeedWetMultipliers[LiquidID.Water];
                }
            }
        }

        public override void SetBestiary(BestiaryDatabase database, BestiaryEntry bestiaryEntry) {
            bestiaryEntry.Info.AddRange([

                BestiaryDatabaseNPCsPopulator.CommonTags.SpawnConditions.Times.NightTime,

                new FlavorTextBestiaryInfoElement("Mods.ExampleMod.Bestiary.PartyZombie"),
            ]);
        }

        public override void HitEffect(NPC.HitInfo hit) {
            for (int i = 0; i < 10; i++) {
                int dustType = Main.rand.Next(139, 143);
                var dust = Dust.NewDustDirect(NPC.position, NPC.width, NPC.height, dustType);

                dust.velocity.X += Main.rand.NextFloat(-0.05f, 0.05f);
                dust.velocity.Y += Main.rand.NextFloat(-0.05f, 0.05f);

                dust.scale *= 1f + Main.rand.NextFloat(-0.03f, 0.03f);
            }
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo hurtInfo) {
            int buffType = ModContent.BuffType<AnimatedBuff>();

            int timeToAdd = 5 * 60;
            target.AddBuff(buffType, timeToAdd);
        }

        public override void ModifyIncomingHit(ref NPC.HitModifiers modifiers) {
            if (modifiers.DamageType.CountsAsClass(DamageClass.Magic)) {
                modifiers.FinalDamage *= 0.25f;
            }
        }
    }
}
