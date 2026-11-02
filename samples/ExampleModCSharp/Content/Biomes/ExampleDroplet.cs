using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace ExampleMod.Content.Biomes;

public class ExampleDroplet : ModGore
{
    public override void SetStaticDefaults()
    {
        ChildSafety.SafeGore[Type] = true;
        GoreID.Sets.IsDrip[Type] = true;
        UpdateType = GoreID.WaterDrip;
    }
}
