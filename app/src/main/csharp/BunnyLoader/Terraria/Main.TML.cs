using System.Collections.Generic;
using Microsoft.Xna.Framework.Graphics;

namespace Terraria;

public readonly partial struct Main
{
    public static Color DiscoColor => new(DiscoR, DiscoG, DiscoB);

    public static void NewText(object o, Color? color = null) =>
        NewText(o is Terraria.Localization.LocalizedText text ? text.Value : o?.ToString(), color ?? new Color(255, 255, 255, 255));

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
