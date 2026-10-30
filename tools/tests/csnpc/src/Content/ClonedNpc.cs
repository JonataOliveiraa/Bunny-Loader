using Terraria;
using Terraria.ModLoader;

namespace CsNpc.Content;

public sealed class ClonedNpc : TestNpc
{
    protected override bool CloneNewInstances => true;

    public override void SetStaticDefaults()
    {
        Main.npcFrameCount[Type] = 1;
        Value = 67;
        Values.Add(67);
    }
}
