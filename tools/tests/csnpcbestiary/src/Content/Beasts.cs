using System;
using Terraria;
using Terraria.GameContent.Bestiary;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.ModLoader;

namespace CsNpcBestiary.Content;

public sealed class BeastNpc : ModNPC
{
    public static int Calls;
    public static IntPtr Flavor;

    public override void SetStaticDefaults()
    {
        NPCID.Sets.NPCBestiaryDrawModifiers modifiers = new()
        {
            Velocity = 1f,
            Direction = 1,
            CustomTexturePath = "CsNpcBestiary/Content/BeastIcon",
        };
        NPCID.Sets.NPCBestiaryDrawOffset.Add(Type, modifiers);
        ContentSamples.NpcBestiaryRarityStars[Type] = 4;
    }

    public override void SetDefaults()
    {
        NPC.width = NPC.height = 24;
        NPC.lifeMax = 100;
        NPC.damage = 0;
        NPC.aiStyle = 0;
    }

    public override void SetBestiary(BestiaryDatabase database, BestiaryEntry bestiaryEntry)
    {
        Calls++;
        var flavor = new FlavorTextBestiaryInfoElement("Mods.CsNpcBestiary.Bestiary.Beast");
        Flavor = flavor.Pointer;
        bestiaryEntry.Info.AddRange([
            BestiaryDatabaseNPCsPopulator.CommonTags.SpawnConditions.Biomes.Surface,
            flavor,
        ]);
    }

    public override void ModifyNPCLoot(NPCLoot npcLoot) => npcLoot.Add(ItemDropRule.Common(ItemID.Gel));
}

public sealed class HiddenNpc : ModNPC
{
    public static int Calls;

    public override void SetStaticDefaults()
    {
        NPCID.Sets.NPCBestiaryDrawModifiers modifiers = new() { Hide = true };
        NPCID.Sets.NPCBestiaryDrawOffset.Add(Type, modifiers);
    }

    public override void SetDefaults()
    {
        NPC.width = NPC.height = 24;
        NPC.lifeMax = 100;
    }

    public override void SetBestiary(BestiaryDatabase database, BestiaryEntry bestiaryEntry) => Calls++;
}

public sealed class BunnyGlobal : GlobalNPC
{
    public static int Calls;
    public static IntPtr Flavor;

    public override bool AppliesToEntity(NPC entity, bool lateInstantiation) => entity.type == NPCID.Bunny;

    public override void SetBestiary(NPC npc, BestiaryDatabase database, BestiaryEntry bestiaryEntry)
    {
        Calls++;
        var flavor = new FlavorTextBestiaryInfoElement("Mods.CsNpcBestiary.Bestiary.Bunny");
        Flavor = flavor.Pointer;
        bestiaryEntry.Info.Add(flavor);
    }

    public override void ModifyNPCLoot(NPC npc, NPCLoot npcLoot) => npcLoot.Add(ItemDropRule.Common(ItemID.Diamond));
}
