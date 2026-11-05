using ExampleMod.Common.Systems;
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

namespace ExampleMod.Content.NPCs.TownPets
{
    [AutoloadHead]
    public class ExampleTownPet : ModNPC
    {
        internal static int HeadIndex1;
        internal static int HeadIndex2;
        internal static int HeadIndex3;
        internal static int HeadIndex4;
        internal static int HeadIndex5;
        internal static int HeadIndex6;

        private static ITownNPCProfile NPCProfile;

        public override void Load() {
            HeadIndex1 = Mod.AddNPCHeadTexture(Type, $"{Texture}_1_Head");
            HeadIndex2 = Mod.AddNPCHeadTexture(Type, $"{Texture}_2_Head");
            HeadIndex3 = Mod.AddNPCHeadTexture(Type, $"{Texture}_3_Head");
            HeadIndex4 = Mod.AddNPCHeadTexture(Type, $"{Texture}_4_Head");
            HeadIndex5 = Mod.AddNPCHeadTexture(Type, $"{Texture}_5_Head");
            HeadIndex6 = Mod.AddNPCHeadTexture(Type, $"{Texture}_6_Head");
        }

        public override void SetStaticDefaults() {
            Main.npcFrameCount[Type] = 27;
            NPCID.Sets.ExtraFramesCount[Type] = 20;
            NPCID.Sets.AttackFrameCount[Type] = 0;
            NPCID.Sets.DangerDetectRange[Type] = 250;
            NPCID.Sets.AttackType[Type] = -1;
            NPCID.Sets.AttackTime[Type] = -1;
            NPCID.Sets.AttackAverageChance[Type] = 1;
            NPCID.Sets.HatOffsetY[Type] = -2;
            NPCID.Sets.ShimmerTownTransform[Type] = false;
            NPCID.Sets.SpecificDebuffImmunity[Type][BuffID.Shimmer] = true;
            NPCID.Sets.SpecificDebuffImmunity[Type][BuffID.Confused] = true;
            NPCID.Sets.ExtraTextureCount[Type] = 0;
            NPCID.Sets.NPCFramingGroup[Type] = 8;

            NPCID.Sets.IsTownPet[Type] = true;
            NPCID.Sets.CannotSitOnFurniture[Type] = false;
            NPCID.Sets.TownNPCBestiaryPriority.Add(Type);
            NPCID.Sets.PlayerDistanceWhilePetting[Type] = 32;
            NPCID.Sets.IsPetSmallForPetting[Type] = true;

            NPCID.Sets.NPCBestiaryDrawModifiers drawModifiers = new() {
                Velocity = 0.25f,
            };

            NPCID.Sets.NPCBestiaryDrawOffset.Add(Type, drawModifiers);

            NPCProfile = new ExampleTownPetProfile();

            NPCID.Sets.NPCPortraits.Add(Type, NPCID.Sets.PrioritizedPortrait()
                .With(NPCID.Sets.VariantPortraitCondition(0), NPCID.Sets.BasicPortrait($"{Texture}_Portrait"))
                .With(NPCID.Sets.VariantPortraitCondition(1), NPCID.Sets.BasicPortrait($"{Texture}_1_Portrait"))
                .With(NPCID.Sets.VariantPortraitCondition(2), NPCID.Sets.BasicPortrait($"{Texture}_2_Portrait"))
                .With(NPCID.Sets.VariantPortraitCondition(3), NPCID.Sets.BasicPortrait($"{Texture}_3_Portrait"))
                .With(NPCID.Sets.VariantPortraitCondition(4), NPCID.Sets.BasicPortrait($"{Texture}_4_Portrait"))
                .With(NPCID.Sets.VariantPortraitCondition(5), NPCID.Sets.BasicPortrait($"{Texture}_5_Portrait"))
                .With(NPCID.Sets.VariantPortraitCondition(6), NPCID.Sets.BasicPortrait($"{Texture}_6_Portrait"))
                .Default(NPCID.Sets.BasicPortrait($"{Texture}_Portrait")));
            NPCID.Sets.NPCPortraitsCloseUpOffsets.Add(Type, new Vector2(-24f, 16f));
        }

        public override void SetDefaults() {
            NPC.townNPC = true;
            NPC.friendly = true;
            NPC.width = 20;
            NPC.height = 20;
            NPC.aiStyle = NPCAIStyleID.Passive;
            NPC.damage = 10;
            NPC.defense = 15;
            NPC.lifeMax = 250;
            NPC.HitSound = SoundID.NPCHit1;
            NPC.DeathSound = SoundID.NPCDeath6;
            NPC.knockBackResist = 0.5f;
            NPC.housingCategory = 1;
            AnimationType = NPCID.TownBunny;
        }
        public override void SetBestiary(BestiaryDatabase database, BestiaryEntry bestiaryEntry) {
            bestiaryEntry.Info.AddRange(
            [
                BestiaryDatabaseNPCsPopulator.CommonTags.SpawnConditions.Biomes.Surface,
                new FlavorTextBestiaryInfoElement("Mods.ExampleMod.Bestiary.ExampleTownPet")
            ]);
        }

        public override bool CanTownNPCSpawn(int numTownNPCs) {
            if (ExampleTownPetSystem.boughtExampleTownPet) {
                return true;
            }
            return false;
        }

        public override ITownNPCProfile TownNPCProfile() {
            return NPCProfile;
        }

        public readonly List<string> NameList0 = new() {
            "Monochromatic", "Grayscale", "Unpainted"
        };
        public readonly List<string> NameList1 = new() {
            "Red", "Crimson", "Cinnabar", "Maroon", "Scarlet"
        };
        public readonly List<string> NameList3 = new() {
            "Blue", "Azure", "Sky", "Ultramarine", "Navy"
        };
        public readonly List<string> NameList4 = new() {
            "Cyan", "Turquoise", "Teal", "Aqua", "Keppel"
        };
        public readonly List<string> NameList5 = new() {
            "Yellow", "Gold", "Cream", "Lemon", "Solar"
        };
        public readonly List<string> NameList6 = new() {
            "Magenta", "Pink", "Purple", "Violet", "Fuchsia"
        };

        public override List<string> SetNPCNameList() {
            return NPC.townNpcVariationIndex switch {
                0 => NameList0,
                1 => NameList1,

                2 => Language.FindAll(Lang.CreateDialogFilter(this.GetLocalizationKey("Names.Green."))).Select(x => x.Value).ToList(),
                3 => NameList3,
                4 => NameList4,
                5 => NameList5,
                6 => NameList6,
                _ => NameList0
            };
        }

        public override string GetChat() {
            return Language.SelectRandom(Lang.CreateDialogFilter("Mods.ExampleMod.Dialogue.ExampleTownPet")).Value;
        }

        public override bool PreAI() {
            if (NPC.ai[0] == 5f) {
                DrawOffsetY = -10;
            }
            else {
                DrawOffsetY = 0;
            }

            return base.PreAI();
        }

        public override void ChatBubblePosition(ref Vector2 position, ref SpriteEffects spriteEffects) {
            if (NPC.ai[0] == 5f) {
                position.Y -= 18f;
            }
        }

        public override void PartyHatPosition(ref Vector2 position, ref SpriteEffects spriteEffects) {
            int frame = NPC.frame.Y / NPC.frame.Height;
            int xOffset = 8;

            switch (frame) {
                case 1:
                case 2:
                case 7:
                case 9:
                    xOffset -= 2;
                    break;
                case 3:
                case 8:
                    xOffset -= 4;
                    break;
                case 11:
                case 15:
                case 16:
                case 17:
                case 26:
                    xOffset += 2;
                    break;
                case 12:
                case 13:
                case 14:
                case 18:
                case 24:
                case 25:
                    xOffset += 4;
                    break;
                case 19:
                case 20:
                case 21:
                case 22:
                case 23:
                    xOffset += 6;
                    break;
                default:
                    break;
            }
            position.X += xOffset * NPC.spriteDirection;

            int yOffset = 0;

            switch (frame) {
                case 3:
                case 4:
                    yOffset -= 2;
                    break;
                case 5:
                case 6:
                case 10:
                case 17:
                case 26:
                    yOffset += 2;
                    break;
                case 18:
                case 25:
                    yOffset += 4;
                    break;
                case 11:
                case 15:
                case 16:
                case 24:
                    yOffset += 6;
                    break;
                case 19:
                case 20:
                case 23:
                    yOffset += 8;
                    break;
                case 21:
                case 22:
                    yOffset += 10;
                    break;
                case 12:
                case 14:
                    yOffset += 12;
                    break;
                case 13:
                    yOffset += 14;
                    break;
                default:
                    break;
            }
            position.Y += yOffset;

            if (NPC.ai[0] == 5f) {
                position.Y += -10;
            }
        }
    }

    public class ExampleTownPetProfile : ITownNPCProfile
    {
        private static readonly string filePath = "ExampleMod/Content/NPCs/TownPets/ExampleTownPet";

        private readonly Asset<Texture2D> variant0 = ModContent.Request<Texture2D>(filePath);
        private readonly Asset<Texture2D> variant1 = ModContent.Request<Texture2D>($"{filePath}_1");
        private readonly Asset<Texture2D> variant2 = ModContent.Request<Texture2D>($"{filePath}_2");
        private readonly Asset<Texture2D> variant3 = ModContent.Request<Texture2D>($"{filePath}_3");
        private readonly Asset<Texture2D> variant4 = ModContent.Request<Texture2D>($"{filePath}_4");
        private readonly Asset<Texture2D> variant5 = ModContent.Request<Texture2D>($"{filePath}_5");
        private readonly Asset<Texture2D> variant6 = ModContent.Request<Texture2D>($"{filePath}_6");
        private readonly int headIndex0 = ModContent.GetModHeadSlot($"{filePath}_Head");

        public int RollVariation() {
            int random = Main.rand.Next(7);

            return random;
        }

        public string GetNameForVariant(NPC npc) => NPC.getNewNPCName(npc.type);

        public Asset<Texture2D> GetTextureNPCShouldUse(NPC npc) {
            return npc.townNpcVariationIndex switch {
                0 => variant0,
                1 => variant1,
                2 => variant2,
                3 => variant3,
                4 => variant4,
                5 => variant5,
                6 => variant6,
                _ => variant0
            };
        }

        public int GetHeadTextureIndex(NPC npc) {
            return npc.townNpcVariationIndex switch {
                0 => headIndex0,
                1 => ExampleTownPet.HeadIndex1,
                2 => ExampleTownPet.HeadIndex2,
                3 => ExampleTownPet.HeadIndex3,
                4 => ExampleTownPet.HeadIndex4,
                5 => ExampleTownPet.HeadIndex5,
                6 => ExampleTownPet.HeadIndex6,
                _ => headIndex0
            };
        }
    }
}
