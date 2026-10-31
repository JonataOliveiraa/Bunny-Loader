using Terraria;
using Terraria.ID;

namespace CsNpc.Content;

public sealed class VanillaCloneNpc : TestNpc
{
    public override void SetStaticDefaults() => Main.npcFrameCount[Type] = Main.npcFrameCount[NPCID.Zombie];

    public override void SetDefaults() => NPC.CloneDefaults(NPCID.Zombie);
}
