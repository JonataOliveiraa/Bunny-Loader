using ExampleMod.Content.Biomes;
using ExampleMod.Content.EmoteBubbles;
using ExampleMod.Content.Items;
using Microsoft.Xna.Framework;
using System;
using System.IO;
using Terraria;
using Terraria.GameContent;
using Terraria.GameContent.Bestiary;
using Terraria.GameContent.UI;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Terraria.ModLoader.Utilities;

namespace ExampleMod.Content.NPCs
{
    public class ExampleZombieThief : ModNPC
    {
        public int StolenItems = 0;

        public override void SetStaticDefaults() {
            Main.npcFrameCount[Type] = Main.npcFrameCount[NPCID.Zombie];

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

        public override void SetBestiary(BestiaryDatabase database, BestiaryEntry bestiaryEntry) {
            bestiaryEntry.Info.AddRange([

                BestiaryDatabaseNPCsPopulator.CommonTags.SpawnConditions.Times.NightTime,

                new FlavorTextBestiaryInfoElement("Mods.ExampleMod.Bestiary.ExampleZombieThief"),
            ]);
        }

        public override void AI() {
            if ((Main.netMode & NetmodeID.MultiplayerClient) != 0) {
                return;
            }

            Rectangle hitbox = NPC.Hitbox;
            foreach (WorldItem item in Main.item) {
                if (item.active && !item.beingGrabbed && item.type == ModContent.ItemType<ExampleItem>() && hitbox.Intersects(item.Hitbox)) {
                    StolenItems += item.stack;
                    item.TurnToAir();

                    NetMessage.SendData(MessageID.SyncItem, number: item.whoAmI);

                    EmoteBubble.NewBubble(ModContent.EmoteBubbleType<ExampleItemEmote>(), WorldUIAnchor.New((Entity)NPC), 90);
                }
            }
        }

        public override void SendExtraAI(BinaryWriter writer) {
            writer.Write(StolenItems);
        }

        public override void ReceiveExtraAI(BinaryReader reader) {
            StolenItems = reader.ReadInt32();
        }

        public override void OnKill() {
            if ((Main.netMode & NetmodeID.MultiplayerClient) != 0) {
                return;
            }

            while (StolenItems > 0) {
                int droppedAmount = Math.Min(ModContent.GetInstance<ExampleItem>().Item.maxStack, StolenItems);
                StolenItems -= droppedAmount;
                Item.NewItem(NPC.GetSource_Death(), NPC.Center, ModContent.ItemType<ExampleItem>(), droppedAmount);
            }
        }

        public override float SpawnChance(NPC.Spawner spawner) {
            if (spawner.Player.InModBiome(ModContent.GetInstance<ExampleSurfaceBiome>()) && !NPC.AnyNPCs(Type)) {
                return SpawnCondition.OverworldNightMonster.Chance * 0.1f;
            }

            return 0f;
        }

        public override bool NeedSaving() {
            return StolenItems >= 10;
        }

        public override void SaveData(TagCompound tag) {
            if (StolenItems > 0) {
                tag["StolenItems"] = StolenItems;
            }
        }

        public override void LoadData(TagCompound tag) {
            StolenItems = tag.GetInt("StolenItems");
        }
    }
}
