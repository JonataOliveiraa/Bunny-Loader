using Terraria.ModLoader;

namespace CsLoad.Content.Items;

public class CloneItem : ModItem
{
    public static int SeenMarker, SeenType;

    public int Marker = 1;

    protected override bool CloneNewInstances => true;

    public override void Load() => Marker = 42;

    public override void SetDefaults()
    {
        SeenMarker = Marker;
        SeenType = Type;
        Item.width = 20;
        Item.height = 20;
    }
}
