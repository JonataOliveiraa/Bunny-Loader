using ExampleMod.Content.Dusts;
using ExampleMod.Content.EmoteBubbles;
using ExampleMod.Content.Items;
using ExampleMod.Content.Items.Armor;
using ExampleMod.Content.Items.Placeable;
using ExampleMod.Content.Items.Placeable.Furniture;
using ExampleMod.Content.Items.Tools;
using ExampleMod.Content.Items.Weapons;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.Chat;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.GameContent.Bestiary;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.Utilities;

namespace ExampleMod.Content.NPCs
{
    [AutoloadHead]
    class ExampleTravelingMerchant : ModNPC
    {
        public const double despawnTime = 48600.0;

        public static double spawnTime = double.MaxValue;

        public readonly static List<Item> shopItems = new();

        public static ExampleTravelingMerchantShop Shop;

        private static int ShimmerHeadIndex;
        private static Profiles.StackedNPCProfile NPCProfile;

        public override bool PreAI() {
            if ((!Main.dayTime || Main.time >= despawnTime) && !IsNpcOnscreen(NPC.Center))
            {
                if (Main.netMode == NetmodeID.SinglePlayer) {
                    Main.NewText(Language.GetTextValue("LegacyMisc.35", NPC.FullName), 50, 125, 255);
                }
                else {
                    ChatHelper.BroadcastChatMessage(NetworkText.FromKey("LegacyMisc.35", NPC.GetFullNetName()), new Color(50, 125, 255));
                }
                NPC.active = false;
                NPC.life = 0;
                return false;
            }

            return true;
        }

        public override void AddShops() {
            Shop = new ExampleTravelingMerchantShop(NPC.type);

            Shop.Add<ExampleItem>();

            Shop.AddPool("Tools", slots: 2)
                .Add<ExampleDrill>()
                .Add<ExampleHamaxe>()
                .Add<ExampleFishingRod>()
                .Add<ExampleHookItem>()
                .Add<ExampleBugNet>()
                .Add<ExamplePickaxe>();

            Shop.AddPool("Weapons", slots: 4)
                .Add<ExampleSword>()
                .Add<ExampleShortsword>()
                .Add<ExampleShootingSword>()
                .Add<ExampleJavelin>()
                .Add<ExampleSpear>()
                .Add<ExampleMagicWeapon>()
                .Add<ExampleGun>()
                .Add<ExampleShotgun>()
                .Add<ExampleMinigun>()
                .Add<ExampleFlail>()
                .Add<ExampleAdvancedFlail>(Condition.Hardmode)
                .Add<ExampleWhip>()
                .Add<ExampleWhipAdvanced>(Condition.Hardmode)
                .Add<ExampleYoyo>();

            Shop.AddPool("Furniture", slots: 3)
                .Add<ExampleLamp>()
                .Add<ExampleBed>()
                .Add<ExampleChair>()
                .Add<ExampleChest>()
                .Add<ExampleClock>()
                .Add<ExampleDoor>()
                .Add<ExampleSink>()
                .Add<ExampleTable>()
                .Add<ExampleToilet>()
                .Add<ExampleWorkbench>();

            Shop.Register();
        }

        public static void UpdateTravelingMerchant() {
            bool travelerIsThere = (NPC.FindFirstNPC(ModContent.NPCType<ExampleTravelingMerchant>()) != -1);

            if (Main.dayTime && Main.time == 0) {
                if (!travelerIsThere && Main.rand.NextBool(4)) {
                    spawnTime = GetRandomSpawnTime(5400, 8100);
                }
                else {
                    spawnTime = double.MaxValue;
                }
            }

            if (!travelerIsThere && CanSpawnNow()) {
                int newTraveler = NPC.NewNPC(Terraria.Entity.GetSource_TownSpawn(), Main.spawnTileX * 16, Main.spawnTileY * 16, ModContent.NPCType<ExampleTravelingMerchant>(), 1);
                NPC traveler = Main.npc[newTraveler];
                traveler.homeless = true;
                traveler.direction = Main.spawnTileX >= WorldGen.bestX ? -1 : 1;
                traveler.netUpdate = true;

                spawnTime = double.MaxValue;

                if (Main.netMode == NetmodeID.SinglePlayer) {
                    Main.NewText(Language.GetTextValue("Announcement.HasArrived", traveler.FullName), 50, 125, 255);
                }
                else {
                    ChatHelper.BroadcastChatMessage(NetworkText.FromKey("Announcement.HasArrived", traveler.GetFullNetName()), new Color(50, 125, 255));
                }
            }
        }

        private static bool CanSpawnNow() {
            if (Main.eclipse || Main.invasionType > 0 && Main.invasionDelay == 0 && Main.invasionSize > 0)
                return false;

            if (Main.IsFastForwardingTime())
                return false;

            return Main.dayTime && Main.time >= spawnTime && Main.time < despawnTime;
        }

        private static bool IsNpcOnscreen(Vector2 center) {
            int w = NPC.sWidth + NPC.safeRangeX * 2;
            int h = NPC.sHeight + NPC.safeRangeY * 2;
            Rectangle npcScreenRect = new Rectangle((int)center.X - w / 2, (int)center.Y - h / 2, w, h);
            foreach (Player player in Main.ActivePlayers) {
                if (player.getRect().Intersects(npcScreenRect)) {
                    return true;
                }
            }
            return false;
        }

        public static double GetRandomSpawnTime(double minTime, double maxTime) {
            return (maxTime - minTime) * Main.rand.NextDouble() + minTime;
        }

        public override void Load() {
            ShimmerHeadIndex = Mod.AddNPCHeadTexture(Type, Texture + "_Shimmer_Head");
        }

        public override void SetStaticDefaults() {
            Main.npcFrameCount[Type] = 25;
            NPCID.Sets.ExtraFramesCount[Type] = 9;
            NPCID.Sets.AttackFrameCount[Type] = 4;
            NPCID.Sets.DangerDetectRange[Type] = 60;
            NPCID.Sets.AttackType[Type] = 3;
            NPCID.Sets.AttackTime[Type] = 12;
            NPCID.Sets.AttackAverageChance[Type] = 1;
            NPCID.Sets.HatOffsetY[Type] = 4;
            NPCID.Sets.ShimmerTownTransform[Type] = true;
            NPCID.Sets.NoTownNPCHappiness[Type] = true;
            NPCID.Sets.FaceEmote[Type] = ModContent.EmoteBubbleType<ExampleTravellingMerchantEmote>();

            NPCID.Sets.NPCBestiaryDrawModifiers drawModifiers = new NPCID.Sets.NPCBestiaryDrawModifiers() {
                Velocity = 2f,
                Direction = -1
            };

            NPCID.Sets.NPCBestiaryDrawOffset.Add(Type, drawModifiers);

            NPCProfile = new Profiles.StackedNPCProfile(
                new Profiles.DefaultNPCProfile(Texture, NPCHeadLoader.GetHeadSlot(HeadTexture), Texture + "_Party"),
                new Profiles.DefaultNPCProfile(Texture + "_Shimmer", ShimmerHeadIndex)
            );

            NPCID.Sets.NPCPortraits.Add(Type, NPCID.Sets.PrioritizedPortrait()
                .With(NPCID.Sets.ShimmeredPortraitCondition, NPCID.Sets.BasicPortrait($"{Texture}_Shimmer_Portrait"))
                .Default(NPCID.Sets.BasicPortrait($"{Texture}_Portrait")));
            NPCID.Sets.NPCPortraitsCloseUpOffsets.Add(Type, new Vector2(-3f, 0f));
        }

        public override void SetDefaults() {
            NPC.townNPC = true;
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
            AnimationType = NPCID.Stylist;
            TownNPCStayingHomeless = true;
        }

        public override void OnSpawn(IEntitySource source) {
            shopItems.Clear();
            shopItems.AddRange(Shop.GenerateNewInventoryList());

            if ((Main.netMode & NetmodeID.Server) != 0) {
                NetMessage.SendData(MessageID.WorldData);
            }
        }

        public override void SetBestiary(BestiaryDatabase database, BestiaryEntry bestiaryEntry) {
            bestiaryEntry.Info.AddRange([
                BestiaryDatabaseNPCsPopulator.CommonTags.SpawnConditions.Biomes.Surface
            ]);
        }

        public override void HitEffect(NPC.HitInfo hit) {
            int num = NPC.life > 0 ? 1 : 5;
            for (int k = 0; k < num; k++) {
                Dust.NewDust(NPC.position, NPC.width, NPC.height, ModContent.DustType<Sparkle>());
            }

            if (Main.netMode != NetmodeID.Server && NPC.life <= 0) {
                int hatGore = NPC.GetPartyHatGore();

                if (hatGore == 0 && !NPC.IsShimmerVariant) {
                    hatGore = Mod.Find<ModGore>($"{Name}_Gore_Hat").Type;
                }
                string variant = "";
                if (NPC.IsShimmerVariant)
                    variant += "_Shimmer";
                int headGore = Mod.Find<ModGore>($"{Name}_Gore{variant}_Head").Type;
                int armGore = Mod.Find<ModGore>($"{Name}_Gore{variant}_Arm").Type;
                int legGore = Mod.Find<ModGore>($"{Name}_Gore{variant}_Leg").Type;

                if (hatGore > 0) {
                    Gore.NewGore(NPC.GetSource_Death(), NPC.position, NPC.velocity, hatGore);
                }
                Gore.NewGore(NPC.GetSource_Death(), NPC.position, NPC.velocity, headGore);
                Gore.NewGore(NPC.GetSource_Death(), NPC.position + new Vector2(0, 20), NPC.velocity, armGore);
                Gore.NewGore(NPC.GetSource_Death(), NPC.position + new Vector2(0, 20), NPC.velocity, armGore);
                Gore.NewGore(NPC.GetSource_Death(), NPC.position + new Vector2(0, 34), NPC.velocity, legGore);
                Gore.NewGore(NPC.GetSource_Death(), NPC.position + new Vector2(0, 34), NPC.velocity, legGore);
            }
        }

        public override bool UsesPartyHat() {
            if (NPC.IsShimmerVariant) {
                return false;
            }
            return true;
        }

        public override bool CanTownNPCSpawn(int numTownNPCs) {
            return false;
        }

        public override ITownNPCProfile TownNPCProfile() {
            return NPCProfile;
        }

        public override List<string> SetNPCNameList() {
            return new List<string>() {
                "Someone Else",
                "Somebody Else",
                "Blockster",
                "Colorful"
            };
        }

        public override string GetChat() {
            LocalizedText chosenChat = Language.SelectRandom(Lang.CreateDialogFilter("Mods.ExampleMod.Dialogue.ExampleTravelingMerchant", true));

            if (chosenChat.Key == "Mods.ExampleMod.Dialogue.ExampleTravelingMerchant.HiveBackpackDialogue") {
                Main.npcChatCornerItem = ItemID.HiveBackpack;
            }

            return chosenChat.Value;
        }

        public override void RegisterChatButtons(NPCInteractionList interactions) {
            interactions.Prepend(NPCInteractions.Shop(Shop.Name));
        }

        public override void AI() {
            NPC.homeless = true;
        }

        public override void ModifyNPCLoot(NPCLoot npcLoot) {
            npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<ExampleCostume>()));
        }

        public override void TownNPCAttackStrength(ref int damage, ref float knockback) {
            damage = 20;
            knockback = 4f;
        }

        public override void TownNPCAttackCooldown(ref int cooldown, ref int randExtraCooldown) {
            cooldown = 15;
            randExtraCooldown = 8;
        }

        public override void TownNPCAttackSwing(ref int itemWidth, ref int itemHeight) {
            itemWidth = itemHeight = 40;
        }

        public override void DrawTownAttackSwing(ref Texture2D item, ref Rectangle itemFrame, ref int itemSize, ref float scale, ref Vector2 offset) {
            Main.GetItemDrawFrame(ModContent.ItemType<ExampleSword>(), out item, out itemFrame);
            itemSize = 40;

            if (NPC.ai[1] > NPCID.Sets.AttackTime[Type] * 0.66f) {
                offset.Y = 12f;
            }
        }
    }

    public class ExampleTravelingMerchantShop : AbstractNPCShop
    {
        public record Pool(string Name, int Slots, List<Entry> Entries)
        {
            public Pool Add(Item item, params Condition[] conditions) {
                Entries.Add(new Entry(item, conditions));
                return this;
            }

            public Pool Add<T>(params Condition[] conditions) where T : ModItem => Add(ModContent.ItemType<T>(), conditions);
            public Pool Add(int item, params Condition[] conditions) => Add(ContentSamples.ItemsByType[item], conditions);

            public IEnumerable<Item> PickItems() {
                var list = Entries.Where(e => !e.Disabled && e.ConditionsMet()).ToList();
                for (int i = 0; i < Slots; i++) {
                    if (list.Count == 0)
                        break;

                    int k = Main.rand.Next(list.Count);
                    yield return list[k].Item;

                    list.RemoveAt(k);
                }
            }
        }

        public List<Pool> Pools { get; } = new();

        protected override IEnumerable<Entry> AllEntries => Pools.SelectMany(p => p.Entries);

        public ExampleTravelingMerchantShop(int npcType) : base(npcType) { }

        public Pool AddPool(string name, int slots) {
            var pool = new Pool(name, slots, new List<Entry>());
            Pools.Add(pool);
            return pool;
        }

        public void Add(Item item, params Condition[] conditions) => AddPool(item.ModItem?.FullName ?? $"Terraria/{item.type}", slots: 1).Add(item, conditions);
        public void Add<T>(params Condition[] conditions) where T : ModItem => Add(ModContent.ItemType<T>(), conditions);
        public void Add(int item, params Condition[] conditions) => Add(ContentSamples.ItemsByType[item], conditions);

        public List<Item> GenerateNewInventoryList() {
            var items = new List<Item>();
            foreach (var pool in Pools) {
                items.AddRange(pool.PickItems());
            }
            return items;
        }

        public override void FillShop(ICollection<Item> items, NPC npc) {
            foreach (var item in ExampleTravelingMerchant.shopItems) {
                items.Add(item.Clone());
            }
        }

        public override void FillShop(Item[] items, NPC npc, out bool overflow) {
            overflow = false;
            int i = 0;

            foreach (var item in ExampleTravelingMerchant.shopItems) {
                if (i == items.Length - 1) {
                    overflow = true;
                    return;
                }

                items[i++] = item.Clone();
            }
        }
    }
}
