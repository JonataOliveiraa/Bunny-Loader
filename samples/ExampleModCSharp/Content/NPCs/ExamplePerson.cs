using ExampleMod.Common;
using ExampleMod.Common.Configs;
using ExampleMod.Common.Systems;
using ExampleMod.Content.Biomes;
using ExampleMod.Content.Dusts;
using ExampleMod.Content.EmoteBubbles;
using ExampleMod.Content.Items;
using ExampleMod.Content.Items.Accessories;
using ExampleMod.Content.Items.Armor;
using ExampleMod.Content.Projectiles;
using ExampleMod.Content.Tiles;
using ExampleMod.Content.Tiles.Furniture;
using ExampleMod.Content.Walls;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using BunnyLoader.Interop;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.GameContent.Bestiary;
using Terraria.GameContent.ItemDropRules;
using Terraria.GameContent.Personalities;
using Terraria.GameContent.UI;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Terraria.Utilities;

namespace ExampleMod.Content.NPCs
{
    [AutoloadHead]
    public class ExamplePerson : ModNPC
    {
        public const string ShopName = "Shop";
        public const string DayOnlyShopName = "DayOnlyShop";
        public int NumberOfTimesTalkedTo = 0;

        private static int ShimmerHeadIndex;
        private static Profiles.StackedNPCProfile NPCProfile;

        public static LocalizedText UpgradeItemButtonText { get; private set; }
        public static LocalizedText UpgradeItemResponseText { get; private set; }
        public static LocalizedText AwesomeifyButtonText { get; private set; }
        public static LocalizedText AwesomeifyResponseText { get; private set; }
        public static LocalizedText DayOnlyShopButtonText { get; private set; }

        public override LocalizedText DeathMessage => this.GetLocalization("DeathMessage");

        public override void Load() {
            ShimmerHeadIndex = Mod.AddNPCHeadTexture(Type, Texture + "_Shimmer_Head");
        }

        public override void SetStaticDefaults() {
            Main.npcFrameCount[Type] = 25;

            NPCID.Sets.ExtraFramesCount[Type] = 9;
            NPCID.Sets.AttackFrameCount[Type] = 4;
            NPCID.Sets.DangerDetectRange[Type] = 700;
            NPCID.Sets.AttackType[Type] = 0;
            NPCID.Sets.AttackTime[Type] = 90;
            NPCID.Sets.AttackAverageChance[Type] = 30;
            NPCID.Sets.HatOffsetY[Type] = 4;
            NPCID.Sets.ShimmerTownTransform[Type] = true;

            NPCID.Sets.FaceEmote[Type] = ModContent.EmoteBubbleType<ExamplePersonEmote>();

            NPCID.Sets.NPCBestiaryDrawModifiers drawModifiers = new NPCID.Sets.NPCBestiaryDrawModifiers() {
                Velocity = 1f,
                Direction = 1
            };

            NPCID.Sets.NPCBestiaryDrawOffset.Add(Type, drawModifiers);

            NPC.Happiness
                .SetBiomeAffection<ExampleSurfaceBiome>(AffectionLevel.Love)
                .SetBiomeAffection<ForestBiome>(AffectionLevel.Like)
                .SetBiomeAffection<SnowBiome>(AffectionLevel.Dislike)
                .SetBiomeAffection<DesertBiome>(AffectionLevel.Dislike)

                .SetNPCAffection(NPCID.Dryad, AffectionLevel.Love)
                .SetNPCAffection(NPCID.Guide, AffectionLevel.Like)
                .SetNPCAffection(NPCID.BestiaryGirl, AffectionLevel.Like)
                .SetNPCAffection(NPCID.Merchant, AffectionLevel.Dislike)
                .SetNPCAffection(NPCID.Golfer, AffectionLevel.Dislike)
                .SetNPCAffection(NPCID.Demolitionist, AffectionLevel.Hate)
            ;

            NPCProfile = new Profiles.StackedNPCProfile(
                new Profiles.DefaultNPCProfile(Texture, NPCHeadLoader.GetHeadSlot(HeadTexture), Texture + "_Party"),
                new Profiles.DefaultNPCProfile(Texture + "_Shimmer", ShimmerHeadIndex, Texture + "_Shimmer_Party")
            );

            NPCID.Sets.NPCPortraits.Add(Type, NPCID.Sets.PrioritizedPortrait()
                .With(NPCID.Sets.ShimmeredPortraitCondition, NPCID.Sets.BasicPortrait($"{Texture}_Shimmer_Portrait"))
                .Default(NPCID.Sets.BasicPortrait($"{Texture}_Portrait")));
            NPCID.Sets.NPCPortraitsCloseUpOffsets.Add(Type, new Vector2(-3f, 0f));

            ContentSamples.NpcBestiaryRarityStars[Type] = 3;

            UpgradeItemButtonText = this.GetLocalization("Interactions.UpgradeItemButton");
            UpgradeItemResponseText = this.GetLocalization("Interactions.UpgradeItemResponse");
            AwesomeifyButtonText = this.GetLocalization("Interactions.AwesomeifyButton");
            AwesomeifyResponseText = this.GetLocalization("Interactions.AwesomeifyResponse");
            DayOnlyShopButtonText = this.GetLocalization("Interactions.DayOnlyShopButton");
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

            AnimationType = NPCID.Guide;
        }

        public override void SetBestiary(BestiaryDatabase database, BestiaryEntry bestiaryEntry) {
            bestiaryEntry.Info.AddRange([

                BestiaryDatabaseNPCsPopulator.CommonTags.SpawnConditions.Biomes.Surface,

                new FlavorTextBestiaryInfoElement("Mods.ExampleMod.Bestiary.ExamplePerson_1"),

                new FlavorTextBestiaryInfoElement("Mods.ExampleMod.Bestiary.ExamplePerson_2")
            ]);
        }

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (NPCID.Sets.NPCBestiaryDrawOffset.TryGetValue(Type, out NPCID.Sets.NPCBestiaryDrawModifiers drawModifiers)) {
                drawModifiers.Rotation += 0.001f;

                NPCID.Sets.NPCBestiaryDrawOffset.Remove(Type);
                NPCID.Sets.NPCBestiaryDrawOffset.Add(Type, drawModifiers);
            }

            return true;
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
                if (NPC.altTexture == 1)
                    variant += "_Party";
                int hatGore = NPC.GetPartyHatGore();
                int headGore = Mod.Find<ModGore>($"{Name}_Gore{variant}_Head").Type;
                int armGore = Mod.Find<ModGore>($"{Name}_Gore{variant}_Arm").Type;
                int legGore = Mod.Find<ModGore>($"{Name}_Gore{variant}_Leg").Type;

                if (hatGore > 0) {
                    Gore.NewGore(NPC.GetSource_Death(), NPC.position, NPC.velocity, hatGore);
                }
                Gore.NewGore(NPC.GetSource_Death(), NPC.position, NPC.velocity, headGore, 1f);
                Gore.NewGore(NPC.GetSource_Death(), NPC.position + new Vector2(0, 20), NPC.velocity, armGore);
                Gore.NewGore(NPC.GetSource_Death(), NPC.position + new Vector2(0, 20), NPC.velocity, armGore);
                Gore.NewGore(NPC.GetSource_Death(), NPC.position + new Vector2(0, 34), NPC.velocity, legGore);
                Gore.NewGore(NPC.GetSource_Death(), NPC.position + new Vector2(0, 34), NPC.velocity, legGore);
            }
        }

        public override void OnSpawn(IEntitySource source) {
            if (EntitySource_SpawnNPC.Is(new GameObject(source.Pointer))) {
                TownNPCRespawnSystem.unlockedExamplePersonSpawn = true;
            }
        }

        public override bool CanTownNPCSpawn(int numTownNPCs) {
            if (TownNPCRespawnSystem.unlockedExamplePersonSpawn) {
                return true;
            }

            foreach (var player in Main.ActivePlayers) {
                if (player.inventory.Any(item => item.type == ModContent.ItemType<ExampleItem>() || item.type == ModContent.ItemType<Items.Placeable.ExampleBlock>())) {
                    return true;
                }
            }

            return false;
        }

        public override bool CheckConditions(int left, int right, int top, int bottom) {
            int score = 0;
            for (int x = left; x <= right; x++) {
                for (int y = top; y <= bottom; y++) {
                    int type = Main.tile[x, y].TileType;
                    if (type == ModContent.TileType<ExampleBlock>() || type == ModContent.TileType<ExampleChair>() || type == ModContent.TileType<ExampleWorkbench>() || type == ModContent.TileType<ExampleBed>() || type == ModContent.TileType<ExampleDoorOpen>() || type == ModContent.TileType<ExampleDoorClosed>()) {
                        score++;
                    }

                    if (Main.tile[x, y].WallType == ModContent.WallType<ExampleWall>()) {
                        score++;
                    }
                }
            }

            return score >= ((right - left) * (bottom - top)) / 2;
        }

        public override ITownNPCProfile TownNPCProfile() {
            return NPCProfile;
        }

        public override List<string> SetNPCNameList() {
            return new List<string>() {
                "Someone",
                "Somebody",
                "Blocky",
                "Colorless"
            };
        }

        public override void FindFrame(int frameHeight) {
        }

        public override string GetChat() {
            WeightedRandom<LocalizedText> chat = new WeightedRandom<LocalizedText>();

            LocalizedText[] dialogueOptions = Language.FindAll(Lang.CreateDialogFilter("Mods.ExampleMod.Dialogue.ExamplePerson", checkConditions: true));

            NumberOfTimesTalkedTo++;

            foreach (var dialogueOption in dialogueOptions) {
                if (dialogueOption.Key == "Mods.ExampleMod.Dialogue.ExamplePerson.TalkALot" && NumberOfTimesTalkedTo < 10) {
                    continue;
                }

                float weight = dialogueOption.Key.Split(".")[^1] switch {
                    "CommonDialogue" => 5f,
                    "RareDialogue" => 0.1f,
                    "PartyGirlDialogue" => 0.25f,
                    _ => 1f,
                };

                chat.Add(dialogueOption, weight);
            }

            LocalizedText chosenChat = chat.Get();

            if (chosenChat.Key == "Mods.ExampleMod.Dialogue.ExamplePerson.StandardDialogue4") {
                Main.npcChatCornerItem = ItemID.HiveBackpack;
            }

            return chosenChat.Value;
        }

        public override void RegisterChatButtons(NPCInteractionList interactions) {
            interactions.InsertBefore(NPCInteractions.Shop(ShopName), NPCInteractionDatabase.CloseButton);

            interactions.InsertBefore(new AwesomeifyButton(), NPCInteractionDatabase.HappinessButton);
            interactions.InsertBefore(new UpgradeButton(), NPCInteractionDatabase.HappinessButton);
            interactions.InsertBefore(new OpenShopOnlyAvailableDuringDay(DayOnlyShopName, DayOnlyShopButtonText.Key), NPCInteractionDatabase.HappinessButton);
        }

        public class AwesomeifyButton : NPCInteraction {
            public override string GetText() => AwesomeifyButtonText.Value;

            public override bool Condition() => true;

            public override void Interact() {
                Main.npcChatText = AwesomeifyResponseText.Value;
            }
        }

        public class UpgradeButton : NPCInteraction {
            public override string GetText() => UpgradeItemButtonText.Value;
            public override bool Condition() => LocalPlayer.HasItem(ItemID.HiveBackpack);
            public override void Interact() {
                SoundEngine.PlaySound(SoundID.Item37);

                Main.npcChatText = UpgradeItemResponseText.Value;

                Main.DoNPCPortraitHop();

                int hiveBackpackItemIndex = LocalPlayer.FindItem(ItemID.HiveBackpack);
                var entitySource = TalkNPC!.Value.GetSource_GiftOrReward();

                int stack = LocalPlayer.inventory[hiveBackpackItemIndex].stack;
                LocalPlayer.inventory[hiveBackpackItemIndex].TurnToAir();
                LocalPlayer.QuickSpawnItem(entitySource, ModContent.ItemType<WaspNest>(), stack);
            }
            public override bool ShowExclamation => true;
        }

        public class OpenShopOnlyAvailableDuringDay(string shopName, string customTextKey) : NPCInteractions.Actions.OpenShop(shopName, customTextKey) {
            public override bool Condition() {
                return base.Condition() && Main.dayTime;
            }
        }

        public override void OnChatButtonClicked(NPCInteraction interaction) {
            if (interaction is AwesomeifyButton) {
                Main.NewText($"{interaction.LocalPlayer.name} clicked on the Awesomeify button!");
            }
        }

        public override void AddShops() {
            var npcShop = new NPCShop(Type, ShopName)
                .Add<ExampleItem>()

                .Add(new Item(ModContent.ItemType<Items.Placeable.Furniture.ExampleWorkbench>()) { shopCustomPrice = Item.buyPrice(copper: 15) })
                .Add<Items.Placeable.Furniture.ExampleChair>()
                .Add<Items.Placeable.Furniture.ExampleDoor>()
                .Add<Items.Placeable.Furniture.ExampleBed>()
                .Add<Items.Placeable.Furniture.ExampleChest>()
                .Add<Items.Tools.ExamplePickaxe>()
                .Add<Items.Tools.ExampleHamaxe>()
                .Add<Items.Consumables.ExampleHealingPotion>(new Condition("Mods.ExampleMod.Conditions.PlayerHasLifeforceBuff", () => Main.LocalPlayer.HasBuff(BuffID.Lifeforce)))
                .Add<Items.Weapons.ExampleSword>(Condition.MoonPhasesQuarter0)

                .Add<Items.Ammo.ExampleBullet>(Condition.MoonPhasesQuarter1)
                .Add<Items.Weapons.ExampleStaff>(ExampleConditions.DownedMinionBoss)
                .Add<ExampleOnBuyItem>()
                .Add(ItemID.AcornAxe)
                .Add<Items.Weapons.ExampleYoyo>(Condition.IsNpcShimmered);

            if (ModContent.GetInstance<ExampleModConfig>().ExampleWingsToggle) {
                npcShop.Add<ExampleWings>(ExampleConditions.InExampleBiome);
            }
            npcShop.Add<ExampleCustomDrawWings>(ExampleConditions.InExampleBiome);

            if (ModContent.TryFind("SummonersAssociation/BloodTalisman", out ModItem bloodTalisman)) {
                npcShop.Add(bloodTalisman.Type);
            }
            npcShop.Register();

            var dayOnlyShop = new NPCShop(Type, DayOnlyShopName)
                .Add(ItemID.Sunglasses)
                .Add(ItemID.AviatorSunglasses)
                .Add(ItemID.HiTekSunglasses);
            dayOnlyShop.Register();
        }

        public override void ModifyActiveShop(string shopName, Item[] items) {
            foreach (Item item in items) {
                if (item.IsNull || item.type == ItemID.None) {
                    continue;
                }

                if (NPC.IsShimmerVariant) {
                    int value = (int?)item.shopCustomPrice ?? item.value;
                    item.shopCustomPrice = value / 2;
                }
            }
        }

        public override void ModifyNPCLoot(NPCLoot npcLoot) {
            npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<ExampleCostume>()));
        }

        public override bool CanGoToStatue(bool toKingStatue) => true;

        public override void OnGoToStatue(bool toKingStatue) {
            if ((Main.netMode & NetmodeID.Server) != 0) {
                ModPacket packet = Mod.GetPacket();
                packet.Write((byte)ExampleMod.MessageType.ExampleTeleportToStatue);
                packet.Write((byte)NPC.whoAmI);
                packet.Send();
            }
            else {
                StatueTeleport();
            }
        }

        public void StatueTeleport() {
            for (int i = 0; i < 30; i++) {
                Vector2 position = Main.rand.NextVector2Square(-20, 21);
                if (Math.Abs(position.X) > Math.Abs(position.Y)) {
                    position.X = Math.Sign(position.X) * 20;
                }
                else {
                    position.Y = Math.Sign(position.Y) * 20;
                }

                Dust.NewDustPerfect(NPC.Center + position, ModContent.DustType<Sparkle>(), Vector2.Zero).noGravity = true;
            }
        }

        public override bool ModifyDeathMessage(ref NetworkText customText, ref Color color) {
            if (NPC.IsShimmerVariant) {
                customText = NetworkText.FromKey(this.GetLocalizationKey("DeathMessageAlt"), NPC.GetFullNetName());
                color = Color.Yellow;
            }
            return true;
        }

        public override void TownNPCAttackStrength(ref int damage, ref float knockback) {
            damage = 20;
            knockback = 4f;
        }

        public override void TownNPCAttackCooldown(ref int cooldown, ref int randExtraCooldown) {
            cooldown = 30;
            randExtraCooldown = 30;
        }

        public override void TownNPCAttackProj(ref int projType, ref int attackDelay) {
            projType = ModContent.ProjectileType<SparklingBall>();
            attackDelay = 1;
        }

        public override void TownNPCAttackProjSpeed(ref float multiplier, ref float gravityCorrection, ref float randomOffset) {
            multiplier = 12f;
            randomOffset = 2f;
        }

        public override void LoadData(TagCompound tag) {
            NumberOfTimesTalkedTo = tag.GetInt("numberOfTimesTalkedTo");
        }

        public override void SaveData(TagCompound tag) {
            tag["numberOfTimesTalkedTo"] = NumberOfTimesTalkedTo;
        }

        public override int? PickEmote(Player closestPlayer, List<int> emoteList, WorldUIAnchor otherAnchor) {
            int type = ModContent.EmoteBubbleType<MinionBossEmote>();

            if (!otherAnchor.IsNull && !otherAnchor.entity.IsNull && NPC.Is(otherAnchor.entity) && new NPC(otherAnchor.entity.Pointer).type == NPCID.Demolitionist) {
                type = EmoteID.EmotionAnger;
            }

            for (int i = 0; i < 4; i++) {
                emoteList.Add(type);
            }

            return base.PickEmote(closestPlayer, emoteList, otherAnchor);
        }
    }
}
