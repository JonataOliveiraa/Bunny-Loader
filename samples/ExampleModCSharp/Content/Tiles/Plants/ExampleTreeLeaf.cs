using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace ExampleMod.Content.Tiles;

public class ExampleTreeLeaf : ModGore
{
    public override string Texture => "ExampleMod/Content/Tiles/Plants/ExampleTree_Leaf";

    public override void SetStaticDefaults()
    {
        ChildSafety.SafeGore[Type] = true;
        GoreID.Sets.SpecialAI[Type] = 3;
    }
}
