using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace ExampleMod.Content.Dusts;

public class ExampleCustomDrawDust : ModDust
{
    public override string Texture => null;

    public override void OnSpawn(Dust dust)
    {
        dust.HackFrame(DustID.Electric);
    }

    public override Color? GetAlpha(Dust dust, Color lightColor)
    {
        lightColor = Color.Lerp(lightColor, Color.White, 0.8f);
        return new Color(lightColor.R, lightColor.G, lightColor.B, 25);
    }

    public override bool PreDraw(Dust dust)
    {
        if (dust.fadeIn == 0f)
        {
            float trailLength = Math.Abs(dust.velocity.X) + Math.Abs(dust.velocity.Y);
            trailLength *= 3f;
            if (trailLength > 10f)
                trailLength = 10f;

            Color drawColor = Lighting.GetColor((int)(dust.position.X + 4) / 16, (int)(dust.position.Y + 4) / 16);
            drawColor = dust.GetAlpha(drawColor);
            for (int i = 0; i < trailLength; i++)
            {
                Vector2 trailPosition = dust.position - dust.velocity * i;
                float trailScale = dust.scale * (1f - i / 10f);
                Main.spriteBatch.Draw(Texture2D.Value, trailPosition - Main.screenPosition, dust.frame, drawColor, dust.rotation, new Vector2(4f, 4f), trailScale, SpriteEffects.None, 0f);
            }
        }

        return true;
    }
}
