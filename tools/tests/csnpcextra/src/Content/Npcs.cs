using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.GameContent;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CsNpcExtra.Content;

public abstract class SmallNpc : ModNPC
{
    public override string Texture => "CsNpcExtra/Content/Small";

    public override void SetDefaults()
    {
        NPC.width = NPC.height = 24;
        NPC.lifeMax = 100;
        NPC.aiStyle = -1;
        NPC.friendly = true;
        NPC.noGravity = NPC.noTileCollide = true;
    }
}

public sealed class Plain : SmallNpc
{
    public static int HoverCalls, HealthBars;
    public static byte HealthBarPosition = 99;

    public override bool? CanFallThroughPlatforms() => true;

    public override bool ModifyCollisionData(Rectangle victimHitbox, ref int immunityCooldownSlot, ref MultipliableFloat damageMultiplier, ref Rectangle npcHitbox)
    {
        immunityCooldownSlot = ImmunityCooldownID.Bosses;
        damageMultiplier *= 0.5f;
        npcHitbox.Inflate(10, 0);
        return true;
    }

    public override bool UsesPartyHat() => false;

    public override bool PreHoverInteract(bool mouseIntersects)
    {
        HoverCalls++;
        return false;
    }

    public override bool? DrawHealthBar(byte hbPosition, ref float scale, ref Vector2 position)
    {
        HealthBars++;
        HealthBarPosition = hbPosition;
        return false;
    }
}

public sealed class Critter : SmallNpc
{
    public static int Caught;
    public static bool Failed = true;

    public override void SetDefaults()
    {
        base.SetDefaults();
        NPC.catchItem = ItemID.Bunny;
    }

    public override bool? CanBeCaughtBy(Item item, Player player) => item.type == ItemID.BugNet;

    public override void OnCaughtBy(Player player, Item item, bool failed)
    {
        Caught++;
        Failed = failed;
    }
}

public sealed class Looter : SmallNpc
{
    public static int Special;

    public override void ModifyNPCLoot(NPCLoot npcLoot) => npcLoot.Add(ItemDropRule.Common(ItemID.Gel));

    public override bool SpecialOnKill()
    {
        Special++;
        return true;
    }
}

public sealed class Keeper : SmallNpc
{
    public int Data;
    public int Loaded = -1;

    public override bool NeedSaving() => true;

    public override void SaveData(TagCompound tag) => tag["data"] = Data;

    public override void LoadData(TagCompound tag) => Loaded = tag.GetInt("data");
}

[AutoloadHead]
public sealed class Townie : ModNPC
{
    public static int Hats, Bubbles, Buffs, Statues, Hovers, Deaths, Rolls, Names, Textures, Heads, MaxDefense;
    public static bool StatueKing, HoverIntersects;
    public static string DeathText = "";
    public static Color DeathColor;
    public static readonly TownieProfile Profile = new();

    public override void SetStaticDefaults()
    {
        Main.npcFrameCount[Type] = 25;
        NPCID.Sets.ExtraFramesCount[Type] = 9;
        NPCID.Sets.AttackFrameCount[Type] = 4;
    }

    public override void SetDefaults()
    {
        NPC.townNPC = NPC.friendly = true;
        NPC.width = 18;
        NPC.height = 40;
        NPC.lifeMax = 250;
        NPC.defense = 15;
        NPC.aiStyle = 7;
        AnimationType = NPCID.Guide;
    }

    public override bool PreAI() => !Checks.Freeze;

    public override void PostAI()
    {
        if (NPC.defense > MaxDefense) MaxDefense = NPC.defense;
    }

    public override bool CanTownNPCSpawn(int numTownNPCs) => false;

    public override void ModifyTypeName(ref string typeName) => typeName += "+mod";

    public override bool CanGoToStatue(bool toKingStatue) => toKingStatue;

    public override void OnGoToStatue(bool toKingStatue)
    {
        Statues++;
        StatueKing = toKingStatue;
    }

    public override void PartyHatPosition(ref Vector2 position, ref SpriteEffects spriteEffects) => Hats++;

    public override void ChatBubblePosition(ref Vector2 position, ref SpriteEffects spriteEffects) => Bubbles++;

    public override void ModifyHoverBoundingBox(ref Rectangle boundingBox)
    {
        boundingBox.Y -= 80;
        boundingBox.Height += 80;
    }

    public override bool PreHoverInteract(bool mouseIntersects)
    {
        Hovers++;
        HoverIntersects = mouseIntersects;
        return true;
    }

    public override bool ModifyDeathMessage(ref NetworkText customText, ref Color color)
    {
        Deaths++;
        customText = NetworkText.FromLiteral("csnpcextra townie fell");
        color = new Color(10, 200, 30);
        return true;
    }

    public override ITownNPCProfile TownNPCProfile() => Profile;
}

public sealed class TownieProfile : ITownNPCProfile
{
    public int RollVariation()
    {
        Townie.Rolls++;
        return 0;
    }

    public string GetNameForVariant(NPC npc)
    {
        Townie.Names++;
        return "ProfileName";
    }

    public Asset<Texture2D> GetTextureNPCShouldUse(NPC npc)
    {
        Townie.Textures++;
        return ModContent.Request<Texture2D>("CsNpcExtra/Content/Townie_Alt");
    }

    public int GetHeadTextureIndex(NPC npc)
    {
        Townie.Heads++;
        return 1;
    }
}

public sealed class ExtraGlobal : GlobalNPC
{
    public static int Profiles, Catches, Buffs, LastMark = -1, LastKeep = -1;
    public int Mark;
    public int Keep;
    public int LoadedMark = -1, LoadedKeep = -1;

    public override bool InstancePerEntity => true;

    public override bool? CanGoToStatue(NPC npc, bool toKingStatue) => npc.type < NPCID.Count ? false : null;

    public override bool? CanBeCaughtBy(NPC npc, Item item, Player player)
    {
        if (npc.type != NPCID.Bunny) return null;
        Catches++;
        return false;
    }

    public override void BuffTownNPC(ref float damageMult, ref int defense)
    {
        Buffs++;
        defense += 7;
    }

    public override bool NeedSaving(NPC npc) => Keep != 0;

    public override void SaveData(NPC npc, TagCompound tag)
    {
        if (Mark != 0) tag["mark"] = Mark;
        if (Keep != 0) tag["keep"] = Keep;
    }

    public override void LoadData(NPC npc, TagCompound tag)
    {
        if (tag.ContainsKey("mark")) LastMark = LoadedMark = tag.GetInt("mark");
        if (tag.ContainsKey("keep")) LastKeep = LoadedKeep = tag.GetInt("keep");
    }

    public override ITownNPCProfile? ModifyTownNPCProfile(NPC npc)
    {
        if (npc.type == NPCID.Guide) Profiles++;
        return null;
    }
}
