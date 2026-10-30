using Terraria;
using Terraria.ModLoader;

namespace CsNpc.Content;

public sealed class CopiedNpc : TestNpc
{
    public bool KeptInteraction, KeptInstance;

    public override void SetStaticDefaults() => Main.npcFrameCount[Type] = 1;

    public override void SetDefaults()
    {
        NPC.playerInteraction[7] = true;
        NPC.CloneDefaults(ModContent.NPCType<TestNpc>());
        KeptInteraction = NPC.playerInteraction[7];
        KeptInstance = ReferenceEquals(NPC.ModNPC, this);
        NPC.lifeMax += 10;
    }
}
