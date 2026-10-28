using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CsDraw.Common;

public class GelDraw : GlobalItem
{
    public static int WorldPre;

    public static readonly Color Tint = new(50, 60, 70, 80);

    public override Color? GetAlpha(Item item, Color lightColor) => item.type == ItemID.Gel ? Tint : null;

    public override bool PreDrawInWorld(Item item, SpriteBatch spriteBatch, Color lightColor, Color alphaColor, ref float rotation, ref float scale, int whoAmI)
    {
        if (item.type == ItemID.Gel) WorldPre++;
        return true;
    }
}
