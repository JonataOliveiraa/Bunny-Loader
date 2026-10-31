using System.Collections.Generic;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CsNpc.Content;

public class TestNpc : ModNPC
{
    public static int StaticCalls;
    public static bool TemplateBound, StaticTablesReady;
    public int Value = 4;
    public List<int> Values = new() { 4 };

    public override string Texture => "CsNpc/Content/TestNpc";

    public override void SetStaticDefaults()
    {
        StaticCalls++;
        TemplateBound = ReferenceEquals(NPC.ModNPC, this);
        Main.npcFrameCount[Type] = Main.npcFrameCount[NPCID.Zombie];
        StaticTablesReady = Main.npcFrameCount.Length > Type && TextureAssets.Npc.Length > Type && !NPC.IsNull && NPC.lifeMax == 120;
        Value = 99;
    }

    public override void SetDefaults()
    {
        NPC.width = 24;
        NPC.height = 26;
        NPC.damage = 40;
        NPC.defense = 5;
        NPC.lifeMax = 120;
        NPC.friendly = true;
        NPC.aiStyle = -1;
        NPC.noGravity = true;
        NPC.noTileCollide = true;
    }

    public override ModNPC Clone(NPC newEntity)
    {
        var copy = (TestNpc)base.Clone(newEntity);
        copy.Values = new List<int>(Values);
        return copy;
    }
}
