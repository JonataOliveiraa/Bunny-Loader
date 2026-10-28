using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;

namespace CsDraw.Content.Items;

public class Glow : ModItem
{
    public static int WorldPre, WorldPost, InventoryPre, InventoryPost, LastWhoAmI = -1;
    public static float LastScale;
    public static bool BatchSeen;

    public static readonly Color Tint = new(10, 20, 30, 40);

    public static void Reset()
    {
        WorldPre = WorldPost = InventoryPre = InventoryPost = 0;
        LastWhoAmI = -1;
        LastScale = 0;
        BatchSeen = false;
    }

    public override void SetDefaults()
    {
        Item.width = 20;
        Item.height = 20;
        Item.maxStack = 1;
    }

    public override Color? GetAlpha(Color lightColor) => Tint;

    public override bool PreDrawInWorld(SpriteBatch spriteBatch, Color lightColor, Color alphaColor, ref float rotation, ref float scale, int whoAmI)
    {
        WorldPre++;
        LastWhoAmI = whoAmI;
        BatchSeen |= !spriteBatch.IsNull;
        scale *= 2;
        return true;
    }

    public override void PostDrawInWorld(SpriteBatch spriteBatch, Color lightColor, Color alphaColor, float rotation, float scale, int whoAmI)
    {
        WorldPost++;
        LastScale = scale;
    }

    public override bool PreDrawInInventory(SpriteBatch spriteBatch, Vector2 position, Rectangle frame, Color drawColor, Color itemColor, Vector2 origin, float scale)
    {
        InventoryPre++;
        return false;
    }

    public override void PostDrawInInventory(SpriteBatch spriteBatch, Vector2 position, Rectangle frame, Color drawColor, Color itemColor, Vector2 origin, float scale)
    {
        InventoryPost++;
    }
}
