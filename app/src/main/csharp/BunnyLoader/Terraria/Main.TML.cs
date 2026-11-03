using System.Collections.Generic;
using Microsoft.Xna.Framework.Graphics;

namespace Terraria;

public readonly partial struct Main
{
    public static Color DiscoColor => new(DiscoR, DiscoG, DiscoB);

    public static void NewText(object o, Color? color = null) =>
        NewText(o is Terraria.Localization.LocalizedText text ? text.Value : o?.ToString(), color ?? new Color(255, 255, 255, 255));

    public static void EntitySpriteDraw(Texture2D texture, Microsoft.Xna.Framework.Vector2 position, Microsoft.Xna.Framework.Rectangle? sourceRectangle, Color color, float rotation, Microsoft.Xna.Framework.Vector2 origin, float scale, SpriteEffects effects, float worthless = 0f) =>
        EntitySpriteDraw(texture, position, sourceRectangle ?? new Microsoft.Xna.Framework.Rectangle(0, 0, texture.Width, texture.Height), color, rotation, origin, scale, effects, worthless);

    public static void EntitySpriteDraw(Texture2D texture, Microsoft.Xna.Framework.Vector2 position, Microsoft.Xna.Framework.Rectangle? sourceRectangle, Color color, float rotation, Microsoft.Xna.Framework.Vector2 origin, Microsoft.Xna.Framework.Vector2 scale, SpriteEffects effects, float worthless = 0f) =>
        EntitySpriteDraw(texture, position, sourceRectangle ?? new Microsoft.Xna.Framework.Rectangle(0, 0, texture.Width, texture.Height), color, rotation, origin, scale, effects, worthless);

    public static IEnumerable<Projectile> ActiveProjectiles
    {
        get
        {
            for (int i = 0; i < maxProjectiles; i++)
            {
                Projectile p = projectile[i];
                if (p.active) yield return p;
            }
        }
    }
}
