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
    /// <summary>
    /// This file shows off a critter npc. The unique thing about critters is how you can catch them with a bug net.
    /// The important bits are: Main.npcCatchable, NPC.catchItem, and Item.makeNPC.
    /// We will also show off adding an item to an existing RecipeGroup (see ExampleRecipes.AddRecipeGroups).
    /// Additionally, this example shows an involved IL edit.
    /// </summary>
    public class ExampleCritterNPC : ModNPC
    {
        private const int ClonedNPCID = NPCID.Frog; // Easy to change type for your modder convenience

        public override void SetStaticDefaults() {
            Main.npcFrameCount[Type] = Main.npcFrameCount[ClonedNPCID]; // Copy animation frames
            Main.npcCatchable[Type] = true; // This is for certain release situations

            // These three are typical critter values
            NPCID.Sets.CountsAsCritter[Type] = true;
            NPCID.Sets.TakesDamageFromHostilesWithoutBeingFriendly[Type] = true;
            NPCID.Sets.TownCritter[Type] = true;

            // The frog is immune to confused
            NPCID.Sets.SpecificDebuffImmunity[Type][BuffID.Confused] = true;

            NPCID.Sets.NormalGoldCritterBestiaryPriority.Add(Type);

        }

        public override void SetDefaults() {
            // width = 12;
            // height = 10;
            // aiStyle = 7;
            // damage = 0;
            // defense = 0;
            // lifeMax = 5;
            // HitSound = SoundID.NPCHit1;
            // DeathSound = SoundID.NPCDeath1;
            // catchItem = 2121;
            // Sets the above
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
            // OnHitByProjectile can be called on servers, so we need to check this to make sure to only access the achievement on a client to avoid null exceptions.
            if (Main.netMode != NetmodeID.Server && !projectile.trap && !projectile.npcProj) {
                ModContent.GetInstance<AdvancedExampleAchievement>().TotalDamageCondition.Value += damageDone;
            }
        }

        public override Color? GetAlpha(Color drawColor) {
            // GetAlpha gives our Lava Frog a red glow.
            return drawColor with {
                R = 255,

                // Both these do the same in this situation, using these methods is useful.
                G = Utils.Clamp<byte>(drawColor.G, 175, 255),
                B = Math.Min(drawColor.B, (byte)75),
                A = 255
            };
        }

        public override bool PreAI() {
            // Kills the NPC if it hits water, honey or shimmer
            if (NPC.wet && !Collision.LavaCollision(NPC.position, NPC.width, NPC.height)) { // NPC.lavawet not 100% accurate for the frog
                // These 3 lines instantly kill the npc without showing damage numbers, dropping loot, or playing DeathSound. Use this for instant deaths
                NPC.life = 0;
                NPC.HitEffect();
                NPC.active = false;
                SoundEngine.PlaySound(SoundID.NPCDeath16, NPC.position); // plays a fizzle sound
            }

            return true;
        }

        public override void OnCaughtBy(Player player, Item item, bool failed) {
            if (failed) {
                return;
            }

            Point npcTile = NPC.Center.ToTileCoordinates();

            if (!WorldGen.SolidTile(npcTile.X, npcTile.Y)) { // Check if the tile the npc resides the most in is non solid
                ref Tile tile = ref Main.tile[npcTile];
                tile.LiquidAmount = tile.LiquidType == LiquidID.Lava ? // Check if the tile has lava in it
                    Math.Max((byte)Main.rand.Next(50, 150), tile.LiquidAmount) // If it does, then top up the amount
                    : (byte)Main.rand.Next(50, 150); // If it doesn't, then overwrite the amount. Technically this distinction should never be needed bc it will burn but to be safe it's here
                tile.LiquidType = LiquidID.Lava; // Set the liquid type to lava
                WorldGen.SquareTileFrame(npcTile.X, npcTile.Y, true); // Update the surrounding area in the tilemap
            }
        }
    }

    public class ExampleCritterItem : ModItem
    {
        public override void SetStaticDefaults() {
            ItemID.Sets.IsLavaBait[Type] = true; // While this item is not bait, this will require a lava bug net to catch.
            ItemID.Sets.PlaceTileOnAltUse[Type] = true; // Places a critter anchor with right click
        }

        public override void SetDefaults() {
            // useStyle = 1;
            // autoReuse = true;
            // useTurn = true;
            // useAnimation = 15;
            // useTime = 10;
            // consumable = true;
            // width = 12;
            // height = 12;
            // makeNPC = 361;
            // noUseGraphic = true;
            // createTile = TileID.CritterAnchor;
            // value = Item.sellPrice(0, 0, 10); // Sell for 10 silver 

            // Cloning ItemID.Frog sets the preceding values
            Item.CloneDefaults(ItemID.Frog);
            Item.makeNPC = (short)ModContent.NPCType<ExampleCritterNPC>();
            Item.value += Item.sellPrice(silver: 5); // Make this critter sell for 5 silver more than the normal frog
            Item.rare = ItemRarityID.Blue;
        }
    }
}
