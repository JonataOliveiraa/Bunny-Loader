using Terraria.ModLoader;

namespace CsLoad.Content.Items;

public class OrderItem : ModItem
{
    public static int SeenMarker, SeenType;
    public static bool SeenItemOk, SeenMod;

    public int Marker = 1;

    public override void Load()
    {
        Marker = 42;
        Checks.Note("OrderItem.Load");
    }

    public override void SetStaticDefaults() => Checks.Note("OrderItem.SetStaticDefaults");

    public override void SetDefaults()
    {
        SeenMarker = Marker;
        SeenType = Type;
        SeenItemOk = !Item.IsNull && Item.type == Type;
        SeenMod = Mod == ModContent.GetInstance<CsLoadMod>();
        Item.width = 20;
        Item.height = 20;
    }

    public override void AddRecipes() => Checks.Note("OrderItem.AddRecipes");
}
