using Microsoft.Xna.Framework.Graphics;
using ExampleMod.Content.Achievements;
using Microsoft.Xna.Framework;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent.Bestiary;
using Terraria.GameContent.Tile_Entities;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.Utilities;

namespace ExampleMod.Content.NPCs
{
    public class ExampleCritterNPC : ModNPC
    {
        private const int ClonedNPCID = NPCID.Frog;

        public override void SetStaticDefaults() {
            Main.npcFrameCount[Type] = Main.npcFrameCount[ClonedNPCID];
            Main.npcCatchable[Type] = true;

            NPCID.Sets.CountsAsCritter[Type] = true;
            NPCID.Sets.TakesDamageFromHostilesWithoutBeingFriendly[Type] = true;
            NPCID.Sets.TownCritter[Type] = true;

            NPCID.Sets.SpecificDebuffImmunity[Type][BuffID.Confused] = true;

            NPCID.Sets.NormalGoldCritterBestiaryPriority.Add(Type);

        }

        public override void SetDefaults() {
            NPC.CloneDefaults(ClonedNPCID);

            NPC.catchItem = (short)ModContent.ItemType<ExampleCritterItem>();
            NPC.lavaImmune = true;
            AIType = ClonedNPCID;
            AnimationType = ClonedNPCID;
        }

        public override void SetBestiary(BestiaryDatabase database, BestiaryEntry bestiaryEntry) {
            bestiaryEntry.Info.AddRange([
                BestiaryDatabaseNPCsPopulator.CommonTags.SpawnConditions.Biomes.TheUnderworld,
                new FlavorTextBestiaryInfoElement("Mods.ExampleMod.Bestiary.ExampleCritterNPC"),
            ]);
        }

        public override float SpawnChance(NPC.Spawner spawner) {
            return SpawnCondition.Underworld.Chance * 0.1f;
        }

        public override void HitEffect(NPC.HitInfo hit) {
            if (NPC.life <= 0) {
                for (int i = 0; i < 6; i++) {
                    Dust dust = Dust.NewDustDirect(NPC.position, NPC.width, NPC.height, DustID.Worm, 2 * hit.HitDirection, -2f);
                    if (Main.rand.NextBool(2)) {
                        dust.noGravity = true;
                        dust.scale = 1.2f * NPC.scale;
                    }
                    else {
                        dust.scale = 0.7f * NPC.scale;
                    }
                }
                Gore.NewGore(NPC.GetSource_Death(), NPC.position, NPC.velocity, Mod.Find<ModGore>($"{Name}_Gore_Head").Type, NPC.scale);
                Gore.NewGore(NPC.GetSource_Death(), NPC.position, NPC.velocity, Mod.Find<ModGore>($"{Name}_Gore_Leg").Type, NPC.scale);
            }
        }

        public override void OnHitByItem(Player player, Item item, NPC.HitInfo hit, int damageDone) {
            ModContent.GetInstance<AdvancedExampleAchievement>().TotalDamageCondition.Value += damageDone;
            if (item.type == ItemID.IronPickaxe) {
                ModContent.GetInstance<AdvancedExampleAchievement>().IronPickaxeCondition.Complete();
            }
        }

        public override void OnHitByProjectile(Projectile projectile, NPC.HitInfo hit, int damageDone) {
            if (Main.netMode != NetmodeID.Server && !projectile.trap && !projectile.npcProj) {
                ModContent.GetInstance<AdvancedExampleAchievement>().TotalDamageCondition.Value += damageDone;
            }
        }

        public override Color? GetAlpha(Color drawColor) {
            return drawColor with {
                R = 255,

                G = Utils.Clamp<byte>(drawColor.G, 175, 255),
                B = Math.Min(drawColor.B, (byte)75),
                A = 255
            };
        }

        public override bool PreAI() {
            if (NPC.wet && !Collision.LavaCollision(NPC.position, NPC.width, NPC.height)) {
                NPC.life = 0;
                NPC.HitEffect();
                NPC.active = false;
                SoundEngine.PlaySound(SoundID.NPCDeath16, NPC.position);
            }

            return true;
        }

        public override void OnCaughtBy(Player player, Item item, bool failed) {
            if (failed) {
                return;
            }

            Point npcTile = NPC.Center.ToTileCoordinates();

            if (!WorldGen.SolidTile(npcTile.X, npcTile.Y)) {
                ref Tile tile = ref Main.tile[npcTile];
                tile.LiquidAmount = tile.LiquidType == LiquidID.Lava ?
                    Math.Max((byte)Main.rand.Next(50, 150), tile.LiquidAmount)
                    : (byte)Main.rand.Next(50, 150);
                tile.LiquidType = LiquidID.Lava;
                WorldGen.SquareTileFrame(npcTile.X, npcTile.Y, true);
            }
        }
    }

    public class ExampleCritterItem : ModItem
    {
        public override void SetStaticDefaults() {
            ItemID.Sets.IsLavaBait[Type] = true;
            ItemID.Sets.PlaceTileOnAltUse[Type] = true;
        }

        public override void SetDefaults() {
            Item.CloneDefaults(ItemID.Frog);
            Item.makeNPC = (short)ModContent.NPCType<ExampleCritterNPC>();
            Item.value += Item.sellPrice(silver: 5);
            Item.rare = ItemRarityID.Blue;
        }
    }
}
