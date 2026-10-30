using System.Collections.Generic;
using Microsoft.Xna.Framework.Graphics;

namespace Terraria;

public readonly partial struct Main
{
    public static Color DiscoColor => new(DiscoR, DiscoG, DiscoB);

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
