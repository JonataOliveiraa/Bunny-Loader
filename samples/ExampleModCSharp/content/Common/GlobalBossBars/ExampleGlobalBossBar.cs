using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace ExampleMod.Common.GlobalBossBars;

public class ExampleGlobalBossBar : GlobalBossBar
{
    public override bool PreDraw(SpriteBatch spriteBatch, NPC npc, ref BossBarDrawParams drawParams)
    {
        if (npc.type == NPCID.EyeofCthulhu)
        {
            drawParams.IconColor = Main.DiscoColor;
            drawParams.BarColor = Color.Lerp(Color.White, Color.Transparent, (float)Math.Sin(Main.GameUpdateCount / 50f));
        }
        return true;
    }

    public override void PostDraw(SpriteBatch spriteBatch, NPC npc, BossBarDrawParams drawParams)
    {
        if (npc.type != NPCID.EyeofCthulhu) return;
        string text = "GlobalBossBar Showcase";
        var font = FontAssets.MouseText.Value;
        Vector2 size = font.MeasureString(text);
        spriteBatch.DrawString(font, text, drawParams.BarCenter - size / 2 + new Vector2(0, -30), Color.White);
    }
}
