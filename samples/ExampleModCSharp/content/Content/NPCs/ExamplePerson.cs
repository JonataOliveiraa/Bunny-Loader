using System.Collections.Generic;
using ExampleMod.Content.Items;
using ExampleMod.Content.Items.Ammo;
using ExampleMod.Content.Items.Weapons;
using Terraria;
using Terraria.GameContent;
using Terraria.GameContent.Personalities;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace ExampleMod.Content.NPCs;

[AutoloadHead]
public class ExamplePerson : ModNPC
{
    public const string ShopName = "Shop";
    public const string DayOnlyShopName = "DayOnlyShop";
    public int NumberOfTimesTalkedTo;

    public override void SetStaticDefaults()
    {
        Main.npcFrameCount[Type] = 25;
        NPCID.Sets.ExtraFramesCount[Type] = 9;
        NPCID.Sets.AttackFrameCount[Type] = 4;
        NPCID.Sets.DangerDetectRange[Type] = 700;
        NPCID.Sets.AttackType[Type] = 0;
        NPCID.Sets.AttackTime[Type] = 90;
        NPCID.Sets.AttackAverageChance[Type] = 30;
        NPCID.Sets.HatOffsetY[Type] = 4;
        NPC.Happiness
            .SetBiomeAffection<ForestBiome>(AffectionLevel.Like)
            .SetBiomeAffection<SnowBiome>(AffectionLevel.Dislike)
            .SetBiomeAffection<DesertBiome>(AffectionLevel.Dislike)
            .SetNPCAffection(NPCID.Dryad, AffectionLevel.Love)
            .SetNPCAffection(NPCID.Guide, AffectionLevel.Like)
            .SetNPCAffection(NPCID.BestiaryGirl, AffectionLevel.Like)
            .SetNPCAffection(NPCID.Merchant, AffectionLevel.Dislike)
            .SetNPCAffection(NPCID.Golfer, AffectionLevel.Dislike)
            .SetNPCAffection(NPCID.Demolitionist, AffectionLevel.Hate);
    }

    public override void SetDefaults()
    {
        NPC.townNPC = NPC.friendly = true;
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

    public override bool CanTownNPCSpawn(int numTownNPCs)
    {
        int example = ModContent.ItemType<ExampleItem>();
        for (int index = 0; index < 255; index++)
        {
            Player player = Main.player[index];
            if (!player.active) continue;
            for (int i = 0; i < player.inventory.Length; i++)
                if (player.inventory[i].type == example) return true;
        }
        return false;
    }

    public override List<string> SetNPCNameList() => new() { "Someone", "Somebody", "Blocky", "Colorless" };

    public override string GetChat()
    {
        NumberOfTimesTalkedTo++;
        string key = NumberOfTimesTalkedTo >= 10 && Main.rand.Next(4) == 0 ? "TalkALot" : "StandardDialogue" + Main.rand.Next(1, 4);
        return Language.GetTextValue("Mods.ExampleMod.Dialogue.ExamplePerson." + key) ?? "";
    }

    public override void RegisterChatButtons(NPCInteractionList interactions)
    {
        interactions.InsertBefore(NPCInteractions.Shop(ShopName), NPCInteractionDatabase.CloseButton);
        interactions.InsertBefore(new DayShopButton(), NPCInteractionDatabase.HappinessButton);
    }

    public override void AddShops()
    {
        new NPCShop(Type, ShopName)
            .Add<ExampleItem>()
            .Add<ExampleSword>(Condition.TimeNight)
            .Add<ExampleBullet>(Condition.Hardmode)
            .Add(ItemID.AcornAxe)
            .Register();
        new NPCShop(Type, DayOnlyShopName)
            .Add(ItemID.Sunglasses)
            .Add(ItemID.AviatorSunglasses)
            .Add(ItemID.HiTekSunglasses)
            .Register();
    }

    public override void TownNPCAttackStrength(ref int damage, ref float knockback) { damage = 20; knockback = 4; }
    public override void TownNPCAttackCooldown(ref int cooldown, ref int randExtraCooldown) { cooldown = 30; randExtraCooldown = 30; }
    public override void TownNPCAttackProj(ref int projType, ref int attackDelay) { projType = ModContent.ProjectileType<global::ExampleMod.Content.Projectiles.SparklingBall>(); attackDelay = 1; }
    public override void TownNPCAttackProjSpeed(ref float multiplier, ref float gravityCorrection, ref float randomOffset) { multiplier = 12; gravityCorrection = 0; randomOffset = 2; }

    sealed class DayShopButton() : NPCInteractions.Actions.OpenShop(DayOnlyShopName, "Mods.ExampleMod.NPCs.ExamplePerson.Interactions.DayOnlyShopButton")
    {
        public override bool Condition() => Main.dayTime;
    }
}
